using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.HR;
using RomaERP.Infrastructure.Persistence;
using Xunit;

namespace RomaERP.UnitTests;

public class TextLimitTests
{
    [Fact]
    public void Check_RejectsTooLong_TrimsAndNullsEmpty()
    {
        Assert.Throws<ValidationAppException>(() => TextLimit.Check(new string('a', 11), 10, "x"));
        Assert.Equal("ab", TextLimit.Check("  ab ", 10, "x"));
        Assert.Null(TextLimit.Check("   ", 10, "x"));
    }

    [Fact]
    public async Task CareersApply_LongNameIsRejectedCleanly_AndRepeatEmailIsSilent()
    {
        var ctx = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var job = new JobPosting { Title = "محاسب", Description = "وصف", Status = JobStatus.Open };
        ctx.JobPostings.Add(job);
        await ctx.SaveChangesAsync();
        var service = new RecruitmentService(ctx, new FakeClaudeExpenseParser());
        var resume = new string('س', 60);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.ApplyAsync(job.Id,
            new CareerApplicationDto { FullName = new string('x', 201), Email = "a@b.com", ResumeText = resume }));

        var ok = new CareerApplicationDto { FullName = "Sara", Email = "a@b.com", ResumeText = resume };
        await service.ApplyAsync(job.Id, ok);
        await service.ApplyAsync(job.Id, ok); // repeat: no error, no second row
        Assert.Equal(1, await ctx.Candidates.CountAsync());
    }
}
