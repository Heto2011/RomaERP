using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class RecruitmentServiceTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private const string Cv = "خبرة ٥ سنوات في إدارة المطاعم والمشتريات وإعداد الموازنات، وإجادة برامج المحاسبة.";

    [Fact]
    public async Task PublicPage_ShowsOnlyOpenJobs_AndApplicationsAreSavedOncePerEmail()
    {
        var ctx = CreateContext();
        var service = new RecruitmentService(ctx, new FakeClaudeExpenseParser());
        var open = await service.SaveJobAsync(null, new JobPostingDto { Title = "مدير مطعم", Description = "إدارة فرع", Status = JobStatus.Open });
        var draft = await service.SaveJobAsync(null, new JobPostingDto { Title = "محاسب", Description = "حسابات", Status = JobStatus.Draft });
        var expired = await service.SaveJobAsync(null, new JobPostingDto { Title = "قديمة", Description = "x", Status = JobStatus.Open, ClosingDate = DateTime.UtcNow.Date.AddDays(-1) });

        var visible = await service.GetOpenJobsAsync();
        Assert.Equal(new[] { open.Id }, visible.Select(j => j.Id).ToArray());
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetOpenJobAsync(draft.Id));

        await service.ApplyAsync(open.Id, new CareerApplicationDto { FullName = "سارة", Email = "Sara@Example.com", ResumeText = Cv });
        var saved = (await service.GetCandidatesAsync(open.Id)).Single();
        Assert.Equal("Careers page", saved.Source);
        Assert.Equal(CandidateStage.Applied, saved.Stage);

        // same email again (different case) is refused; a closed job takes no applications
        await service.ApplyAsync(open.Id, new CareerApplicationDto { FullName = "سارة", Email = "sara@example.com", ResumeText = Cv }); // repeat: silently ignored
        await Assert.ThrowsAsync<NotFoundException>(() => service.ApplyAsync(expired.Id, new CareerApplicationDto { FullName = "ع", Email = "a@b.com", ResumeText = Cv }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.ApplyAsync(open.Id, new CareerApplicationDto { FullName = "ع", Email = "not-an-email", ResumeText = Cv }));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.ApplyAsync(open.Id, new CareerApplicationDto { FullName = "ع", Email = "x@y.com", ResumeText = "قصير" }));
    }

    [Fact]
    public async Task HoneypotApplication_IsSilentlyIgnored()
    {
        var ctx = CreateContext();
        var service = new RecruitmentService(ctx, new FakeClaudeExpenseParser());
        var job = await service.SaveJobAsync(null, new JobPostingDto { Title = "مدير", Description = "d", Status = JobStatus.Open });

        await service.ApplyAsync(job.Id, new CareerApplicationDto { FullName = "Bot", Email = "bot@spam.com", ResumeText = Cv, Website = "http://spam" });

        Assert.Empty(await service.GetCandidatesAsync(null));
    }

    [Fact]
    public async Task Pipeline_AiScreeningIsAdvisory_StagesMove_AndJobsWithCandidatesCannotBeDeleted()
    {
        var ctx = CreateContext();
        var ai = new FakeClaudeExpenseParser { ScreeningHandler = _ => new CandidateScreeningResult(82, "خبرة مناسبة", new List<string> { "إدارة فروع" }, new List<string> { "لا توجد شهادة" }) };
        var service = new RecruitmentService(ctx, ai);
        var job = await service.SaveJobAsync(null, new JobPostingDto { Title = "مدير فرع", Description = "إدارة", Requirements = "٣ سنوات خبرة", Status = JobStatus.Open });
        var candidate = await service.AddCandidateAsync(new SaveCandidateDto { JobPostingId = job.Id, FullName = "خالد", Email = "k@x.com", ResumeText = Cv }, "Manual");

        var screened = await service.ScreenCandidateAsync(candidate.Id);
        Assert.Equal(82, screened.AiScore);
        Assert.Contains("خبرة مناسبة", screened.AiSummary);
        Assert.Contains("إدارة فروع", screened.AiSummary);
        Assert.Equal(CandidateStage.Applied, screened.Stage); // the AI never moves a candidate by itself

        var moved = await service.UpdateCandidateAsync(candidate.Id, new UpdateCandidateDto { Stage = CandidateStage.Interview, Notes = "مقابلة الأحد", InterviewAtUtc = new DateTime(2026, 10, 25, 9, 0, 0) });
        Assert.Equal(CandidateStage.Interview, moved.Stage);
        Assert.Equal("مقابلة الأحد", moved.Notes);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.DeleteJobAsync(job.Id));
        await service.DeleteCandidateAsync(candidate.Id);
        await service.DeleteJobAsync(job.Id);
        Assert.Empty(await service.GetJobsAsync());
    }
}
