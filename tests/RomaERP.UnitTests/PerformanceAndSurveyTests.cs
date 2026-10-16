using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class PerformanceAndSurveyTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Employee Emp(string code, string name) => new() { EmployeeCode = code, FullNameAr = name, FullNameEn = name, HireDate = new DateTime(2025, 1, 1) };

    [Fact]
    public async Task Review360_CollectsSelfManagerAndPeerRatings_AndKeepsCommentsAnonymousInResults()
    {
        var ctx = CreateContext();
        var subject = Emp("E1", "أحمد"); var boss = Emp("E2", "المدير"); var peer = Emp("E3", "زميل");
        ctx.Employees.AddRange(subject, boss, peer);
        await ctx.SaveChangesAsync();
        var service = new PerformanceService(ctx);

        var cycle = await service.SaveCycleAsync(null, new ReviewCycleDto { Name = "H2 2026", StartDate = new DateTime(2026, 7, 1), EndDate = new DateTime(2026, 12, 31), Competencies = new() { "الجودة", "التعاون" } });
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SetStatusAsync(cycle.Id, ReviewCycleStatus.Active)); // nobody to review yet

        await service.AddAssignmentsAsync(cycle.Id, new AddReviewAssignmentsDto
        {
            SubjectEmployeeId = subject.Id,
            Reviewers = new()
            {
                new() { ReviewerEmployeeId = subject.Id, Relationship = ReviewRelationship.Self },
                new() { ReviewerEmployeeId = boss.Id, Relationship = ReviewRelationship.Manager },
                new() { ReviewerEmployeeId = peer.Id, Relationship = ReviewRelationship.Peer }
            }
        });
        // duplicates are skipped; a self review for someone else is refused
        Assert.Equal(0, await service.AddAssignmentsAsync(cycle.Id, new AddReviewAssignmentsDto { SubjectEmployeeId = subject.Id, Reviewers = new() { new() { ReviewerEmployeeId = boss.Id, Relationship = ReviewRelationship.Manager } } }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.AddAssignmentsAsync(cycle.Id, new AddReviewAssignmentsDto { SubjectEmployeeId = subject.Id, Reviewers = new() { new() { ReviewerEmployeeId = boss.Id, Relationship = ReviewRelationship.Self } } }));

        await service.SetStatusAsync(cycle.Id, ReviewCycleStatus.Active);

        var bossForm = (await service.GetMineAsync(boss.Id)).Single();
        Assert.Equal("أحمد", bossForm.SubjectName);
        // every competency must be rated 1-5
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(boss.Id, bossForm.Id, new SubmitReviewDto { Ratings = new() { ["الجودة"] = 4 } }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(boss.Id, bossForm.Id, new SubmitReviewDto { Ratings = new() { ["الجودة"] = 6, ["التعاون"] = 4 } }));
        // someone else's form cannot be filled in
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(peer.Id, bossForm.Id, new SubmitReviewDto { Ratings = new() { ["الجودة"] = 4, ["التعاون"] = 4 } }));

        await service.SubmitAsync(boss.Id, bossForm.Id, new SubmitReviewDto { Ratings = new() { ["الجودة"] = 4, ["التعاون"] = 5 }, Comments = "ممتاز" });
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(boss.Id, bossForm.Id, new SubmitReviewDto { Ratings = new() { ["الجودة"] = 1, ["التعاون"] = 1 } })); // only once

        var selfForm = (await service.GetMineAsync(subject.Id)).Single();
        await service.SubmitAsync(subject.Id, selfForm.Id, new SubmitReviewDto { Ratings = new() { ["الجودة"] = 5, ["التعاون"] = 5 } });
        var peerForm = (await service.GetMineAsync(peer.Id)).Single();
        await service.SubmitAsync(peer.Id, peerForm.Id, new SubmitReviewDto { Ratings = new() { ["الجودة"] = 3, ["التعاون"] = 4 }, Comments = "أحيانًا يتأخر" });

        var result = (await service.GetResultsAsync(cycle.Id)).Single();
        Assert.Equal(5.0m, result.SelfAverage);
        Assert.Equal(4.0m, result.OthersAverage); // (4.5 + 3.5) / 2
        var peers = result.Groups.Single(g => g.Relationship == ReviewRelationship.Peer);
        Assert.Equal(3.5m, peers.Average);
        Assert.Equal(3m, peers.ByCompetency["الجودة"]);
        Assert.Equal(new[] { "أحيانًا يتأخر" }, peers.Comments.ToArray());

        // a closed cycle takes no more answers
        await service.SetStatusAsync(cycle.Id, ReviewCycleStatus.Closed);
        Assert.Empty(await service.GetMineAsync(subject.Id));
    }

    [Fact]
    public async Task SelfReviews_AreCreatedForEveryActiveEmployeeOnce()
    {
        var ctx = CreateContext();
        var a = Emp("A", "أ"); var b = Emp("B", "ب"); var gone = Emp("C", "ج"); gone.EmploymentStatus = EmploymentStatus.Terminated;
        ctx.Employees.AddRange(a, b, gone);
        await ctx.SaveChangesAsync();
        var service = new PerformanceService(ctx);
        var cycle = await service.SaveCycleAsync(null, new ReviewCycleDto { Name = "x", StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(30) });
        Assert.Equal(PerformanceService.DefaultCompetencies.Length, cycle.Competencies.Count); // none given -> defaults
        Assert.Equal(2, await service.AddSelfReviewsAsync(cycle.Id));
        Assert.Equal(0, await service.AddSelfReviewsAsync(cycle.Id));
    }

    [Fact]
    public async Task AnonymousSurvey_StoresNoIdentity_HidesTextBelowThreshold_AndStopsDoubleAnswers()
    {
        var ctx = CreateContext();
        var staff = Enumerable.Range(1, 4).Select(i => Emp($"S{i}", $"موظف{i}")).ToList();
        ctx.Employees.AddRange(staff);
        await ctx.SaveChangesAsync();
        var service = new SurveyService(ctx);

        var survey = await service.SaveSurveyAsync(null, new SurveyDto
        {
            Title = "رضا الموظفين", IsAnonymous = true,
            Questions = new() { new() { Text = "راضي؟", Type = SurveyQuestionType.Rating }, new() { Text = "هتكمل؟", Type = SurveyQuestionType.YesNo }, new() { Text = "اقتراح", Type = SurveyQuestionType.Text } }
        });
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(staff[0].Id, survey.Id, new SubmitSurveyDto())); // still a draft
        await service.SetStatusAsync(survey.Id, SurveyStatus.Open);
        Assert.Single(await service.GetOpenForEmployeeAsync(staff[0].Id));

        var qs = (await service.GetSurveysAsync()).Single().Questions;
        SubmitSurveyDto Answer(int rating, int yes, string? text) => new()
        {
            Answers = new()
            {
                new() { QuestionId = qs[0].Id, NumberValue = rating }, new() { QuestionId = qs[1].Id, NumberValue = yes }, new() { QuestionId = qs[2].Id, TextValue = text }
            }
        };

        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(staff[0].Id, survey.Id, Answer(9, 1, null))); // rating out of range
        await service.SubmitAsync(staff[0].Id, survey.Id, Answer(4, 1, "زيادة الحوافز"));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SubmitAsync(staff[0].Id, survey.Id, Answer(5, 1, null))); // already answered
        Assert.True((await service.GetOpenForEmployeeAsync(staff[0].Id)).Single().Answered);

        // nobody's identity is stored on the response, and the answer is not logged against a user
        Assert.All(await ctx.SurveyResponses.ToListAsync(), r => Assert.Null(r.EmployeeId));
        Assert.Empty(await ctx.AuditLogs.Where(l => l.EntityName == nameof(SurveyResponse) || l.EntityName == nameof(SurveyAnswer)).ToListAsync());

        var early = await service.GetResultsAsync(survey.Id);
        Assert.True(early.TextHidden);
        Assert.Empty(early.Questions[2].Texts);

        await service.SubmitAsync(staff[1].Id, survey.Id, Answer(2, 0, "مرتبات"));
        await service.SubmitAsync(staff[2].Id, survey.Id, Answer(3, 1, null));
        var results = await service.GetResultsAsync(survey.Id);
        Assert.False(results.TextHidden);
        Assert.Equal(3, results.ResponseCount);
        Assert.Equal(3.0m, results.Questions[0].Average); // (4 + 2 + 3) / 3
        Assert.Equal(2, results.Questions[1].Distribution[1]);
        Assert.Equal(new[] { "زيادة الحوافز", "مرتبات" }.OrderBy(x => x, StringComparer.Ordinal).ToArray(), results.Questions[2].Texts.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}
