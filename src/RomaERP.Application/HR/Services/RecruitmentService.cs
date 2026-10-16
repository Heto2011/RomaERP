using RomaERP.Application.Common;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class JobPostingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public JobStatus Status { get; set; }
    public DateTime? ClosingDate { get; set; }
    public int CandidateCount { get; set; }
}

public class CandidateDto
{
    public Guid Id { get; set; }
    public Guid JobPostingId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string ResumeText { get; set; } = string.Empty;
    public string? CoverNote { get; set; }
    public string Source { get; set; } = string.Empty;
    public CandidateStage Stage { get; set; }
    public string? Notes { get; set; }
    public DateTime? InterviewAtUtc { get; set; }
    public int? AiScore { get; set; }
    public string? AiSummary { get; set; }
    public DateTime? AiScreenedAtUtc { get; set; }
    public DateTime AppliedAtUtc { get; set; }
}

public class SaveCandidateDto
{
    public Guid JobPostingId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string ResumeText { get; set; } = string.Empty;
    public string? CoverNote { get; set; }
}

public class UpdateCandidateDto
{
    public CandidateStage Stage { get; set; }
    public string? Notes { get; set; }
    public DateTime? InterviewAtUtc { get; set; }
}

/// <summary>What the public careers page sends. <see cref="Website"/> is a hidden trap field: real people leave it empty, form-filling bots don't.</summary>
public class CareerApplicationDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string ResumeText { get; set; } = string.Empty;
    public string? CoverNote { get; set; }
    public string? Website { get; set; }
}

public class PublicJobDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public DateTime? ClosingDate { get; set; }
}

public interface IRecruitmentService
{
    Task<List<JobPostingDto>> GetJobsAsync(CancellationToken ct = default);
    Task<JobPostingDto> SaveJobAsync(Guid? id, JobPostingDto dto, CancellationToken ct = default);
    Task DeleteJobAsync(Guid id, CancellationToken ct = default);
    Task<List<CandidateDto>> GetCandidatesAsync(Guid? jobId, CancellationToken ct = default);
    Task<CandidateDto> AddCandidateAsync(SaveCandidateDto dto, string source, CancellationToken ct = default);
    Task<CandidateDto> UpdateCandidateAsync(Guid id, UpdateCandidateDto dto, CancellationToken ct = default);
    Task DeleteCandidateAsync(Guid id, CancellationToken ct = default);
    /// <summary>Asks the AI to score the candidate against the job. Advisory only.</summary>
    Task<CandidateDto> ScreenCandidateAsync(Guid id, CancellationToken ct = default);

    Task<List<PublicJobDto>> GetOpenJobsAsync(CancellationToken ct = default);
    Task<PublicJobDto> GetOpenJobAsync(Guid id, CancellationToken ct = default);
    Task ApplyAsync(Guid jobId, CareerApplicationDto dto, CancellationToken ct = default);
}

public class RecruitmentService : IRecruitmentService
{
    private const int MaxResumeChars = 20000;

    private readonly IApplicationDbContext _context;
    private readonly IClaudeExpenseParser _ai;

    public RecruitmentService(IApplicationDbContext context, IClaudeExpenseParser ai)
    {
        _context = context;
        _ai = ai;
    }

    // ---------- Jobs ----------

    public async Task<List<JobPostingDto>> GetJobsAsync(CancellationToken ct = default)
    {
        var jobs = await _context.JobPostings.AsNoTracking().Include(j => j.Department).OrderByDescending(j => j.CreatedAtUtc).ToListAsync(ct);
        var counts = await _context.Candidates.AsNoTracking().GroupBy(c => c.JobPostingId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return jobs.Select(j => MapJob(j, counts.GetValueOrDefault(j.Id))).ToList();
    }

    public async Task<JobPostingDto> SaveJobAsync(Guid? id, JobPostingDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ValidationAppException("اكتب مسمى الوظيفة.");
        if (string.IsNullOrWhiteSpace(dto.Description)) throw new ValidationAppException("اكتب وصف الوظيفة.");

        JobPosting job;
        if (id is { } existingId)
            job = await _context.JobPostings.Include(j => j.Department).FirstOrDefaultAsync(j => j.Id == existingId, ct) ?? throw new NotFoundException(nameof(JobPosting), existingId);
        else
        {
            job = new JobPosting();
            _context.JobPostings.Add(job);
        }

        if (dto.DepartmentId is { } deptId && !await _context.Departments.AnyAsync(d => d.Id == deptId && !d.IsDeleted, ct))
            throw new NotFoundException(nameof(Department), deptId);

        job.Title = TextLimit.Check(dto.Title, 200, "مسمى الوظيفة")!;
        job.DepartmentId = dto.DepartmentId;
        job.Location = TextLimit.Check(dto.Location, 200, "المكان");
        job.Description = TextLimit.Check(dto.Description, 4000, "وصف الوظيفة")!;
        job.Requirements = TextLimit.Check(dto.Requirements, 2000, "المتطلبات");
        job.Status = dto.Status == 0 ? JobStatus.Draft : dto.Status;
        job.ClosingDate = dto.ClosingDate?.Date;
        await _context.SaveChangesAsync(ct);

        var count = await _context.Candidates.CountAsync(c => c.JobPostingId == job.Id, ct);
        var reloaded = await _context.JobPostings.AsNoTracking().Include(j => j.Department).FirstAsync(j => j.Id == job.Id, ct);
        return MapJob(reloaded, count);
    }

    public async Task DeleteJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new NotFoundException(nameof(JobPosting), id);
        if (await _context.Candidates.AnyAsync(c => c.JobPostingId == id, ct))
            throw new ValidationAppException("الوظيفة دي عليها متقدمين. اقفلها بدل ما تحذفها.");
        job.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    // ---------- Candidates ----------

    public async Task<List<CandidateDto>> GetCandidatesAsync(Guid? jobId, CancellationToken ct = default)
    {
        var query = _context.Candidates.AsNoTracking().Include(c => c.JobPosting).AsQueryable();
        if (jobId is { } id) query = query.Where(c => c.JobPostingId == id);
        var rows = await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);
        return rows.Select(MapCandidate).ToList();
    }

    public async Task<CandidateDto> AddCandidateAsync(SaveCandidateDto dto, string source, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName)) throw new ValidationAppException("اكتب الاسم.");
        var fullName = TextLimit.Check(dto.FullName, 200, "الاسم")!;
        var phone = TextLimit.Check(dto.Phone, 40, "رقم التليفون");
        var email = (dto.Email ?? string.Empty).Trim();
        if (email.Length > 200) throw new ValidationAppException("الإيميل طويل جدًا.");
        if (!email.Contains('@') || email.Length < 5) throw new ValidationAppException("اكتب إيميل صحيح.");
        if (string.IsNullOrWhiteSpace(dto.ResumeText) || dto.ResumeText.Trim().Length < 30)
            throw new ValidationAppException("الصق نص السيرة الذاتية (٣٠ حرف على الأقل).");

        var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == dto.JobPostingId, ct)
            ?? throw new NotFoundException(nameof(JobPosting), dto.JobPostingId);

        var lower = email.ToLowerInvariant();
        if (await _context.Candidates.AnyAsync(c => c.JobPostingId == job.Id && c.Email.ToLower() == lower, ct))
            throw new ValidationAppException("الإيميل ده متقدّم للوظيفة دي قبل كده.");

        var resume = dto.ResumeText.Trim();
        var candidate = new Candidate
        {
            JobPostingId = job.Id,
            FullName = fullName,
            Email = email,
            Phone = phone,
            ResumeText = resume.Length > MaxResumeChars ? resume[..MaxResumeChars] : resume,
            CoverNote = string.IsNullOrWhiteSpace(dto.CoverNote) ? null : (dto.CoverNote.Trim().Length > 2000 ? dto.CoverNote.Trim()[..2000] : dto.CoverNote.Trim()),
            Source = source,
            Stage = CandidateStage.Applied
        };
        _context.Candidates.Add(candidate);
        await _context.SaveChangesAsync(ct);
        candidate.JobPosting = job;
        return MapCandidate(candidate);
    }

    public async Task<CandidateDto> UpdateCandidateAsync(Guid id, UpdateCandidateDto dto, CancellationToken ct = default)
    {
        var c = await _context.Candidates.Include(x => x.JobPosting).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Candidate), id);
        if (!Enum.IsDefined(dto.Stage)) throw new ValidationAppException("مرحلة غير صحيحة.");
        c.Stage = dto.Stage;
        c.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        c.InterviewAtUtc = dto.InterviewAtUtc;
        await _context.SaveChangesAsync(ct);
        return MapCandidate(c);
    }

    public async Task DeleteCandidateAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _context.Candidates.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Candidate), id);
        c.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<CandidateDto> ScreenCandidateAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _context.Candidates.Include(x => x.JobPosting).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Candidate), id);
        var job = c.JobPosting!;

        var result = await _ai.ScreenCandidateAsync(job.Title, job.Description, job.Requirements, c.ResumeText, ct);
        c.AiScore = Math.Clamp(result.Score, 0, 100);
        var lines = new List<string> { result.Summary.Trim() };
        if (result.Strengths.Count > 0) lines.Add("نقاط القوة: " + string.Join("، ", result.Strengths));
        if (result.Concerns.Count > 0) lines.Add("ملاحظات: " + string.Join("، ", result.Concerns));
        var summary = string.Join("\n", lines.Where(l => l.Length > 0));
        c.AiSummary = summary.Length > 2500 ? summary[..2500] : summary;
        c.AiScreenedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return MapCandidate(c);
    }

    // ---------- Public careers page ----------

    public async Task<List<PublicJobDto>> GetOpenJobsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var jobs = await _context.JobPostings.AsNoTracking().Include(j => j.Department)
            .Where(j => j.Status == JobStatus.Open && (j.ClosingDate == null || j.ClosingDate >= today))
            .OrderByDescending(j => j.CreatedAtUtc).ToListAsync(ct);
        return jobs.Select(MapPublic).ToList();
    }

    public async Task<PublicJobDto> GetOpenJobAsync(Guid id, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var job = await _context.JobPostings.AsNoTracking().Include(j => j.Department)
            .FirstOrDefaultAsync(j => j.Id == id && j.Status == JobStatus.Open && (j.ClosingDate == null || j.ClosingDate >= today), ct)
            ?? throw new NotFoundException(nameof(JobPosting), id);
        return MapPublic(job);
    }

    public async Task ApplyAsync(Guid jobId, CareerApplicationDto dto, CancellationToken ct = default)
    {
        // A bot that fills the hidden field is told it worked, and nothing is saved.
        if (!string.IsNullOrWhiteSpace(dto.Website)) return;

        await GetOpenJobAsync(jobId, ct);
        // A repeat application from the same email is accepted silently, so the public page cannot be used to check who has applied.
        var applicant = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (applicant.Length > 0 && await _context.Candidates.AnyAsync(c => c.JobPostingId == jobId && c.Email.ToLower() == applicant, ct)) return;
        await AddCandidateAsync(new SaveCandidateDto
        {
            JobPostingId = jobId, FullName = dto.FullName, Email = dto.Email, Phone = dto.Phone, ResumeText = dto.ResumeText, CoverNote = dto.CoverNote
        }, "Careers page", ct);
    }

    private static JobPostingDto MapJob(JobPosting j, int candidates) => new()
    {
        Id = j.Id, Title = j.Title, DepartmentId = j.DepartmentId, DepartmentName = j.Department?.NameAr, Location = j.Location,
        Description = j.Description, Requirements = j.Requirements, Status = j.Status, ClosingDate = j.ClosingDate, CandidateCount = candidates
    };

    private static PublicJobDto MapPublic(JobPosting j) => new()
    {
        Id = j.Id, Title = j.Title, DepartmentName = j.Department?.NameAr, Location = j.Location, Description = j.Description,
        Requirements = j.Requirements, ClosingDate = j.ClosingDate
    };

    private static CandidateDto MapCandidate(Candidate c) => new()
    {
        Id = c.Id, JobPostingId = c.JobPostingId, JobTitle = c.JobPosting?.Title ?? string.Empty, FullName = c.FullName, Email = c.Email, Phone = c.Phone,
        ResumeText = c.ResumeText, CoverNote = c.CoverNote, Source = c.Source, Stage = c.Stage, Notes = c.Notes, InterviewAtUtc = c.InterviewAtUtc,
        AiScore = c.AiScore, AiSummary = c.AiSummary, AiScreenedAtUtc = c.AiScreenedAtUtc, AppliedAtUtc = c.CreatedAtUtc
    };
}
