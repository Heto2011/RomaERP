using RomaERP.Application.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class ReviewCycleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ReviewCycleStatus Status { get; set; }
    public List<string> Competencies { get; set; } = new();
    public int TotalReviews { get; set; }
    public int SubmittedReviews { get; set; }
}

public class ReviewerInputDto
{
    public Guid ReviewerEmployeeId { get; set; }
    public ReviewRelationship Relationship { get; set; }
}

public class AddReviewAssignmentsDto
{
    public Guid SubjectEmployeeId { get; set; }
    public List<ReviewerInputDto> Reviewers { get; set; } = new();
}

public class ReviewAssignmentDto
{
    public Guid Id { get; set; }
    public Guid ReviewCycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public Guid SubjectEmployeeId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid ReviewerEmployeeId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public ReviewRelationship Relationship { get; set; }
    public bool IsSubmitted { get; set; }
    public decimal? OverallRating { get; set; }
    public List<string> Competencies { get; set; } = new();
    public Dictionary<string, int> Ratings { get; set; } = new();
    public string? Comments { get; set; }
}

public class SubmitReviewDto
{
    /// <summary>Competency name → 1..5. Every competency of the cycle must be rated.</summary>
    public Dictionary<string, int> Ratings { get; set; } = new();
    public string? Comments { get; set; }
}

public class ReviewGroupResultDto
{
    public ReviewRelationship Relationship { get; set; }
    public int Count { get; set; }
    public decimal? Average { get; set; }
    public Dictionary<string, decimal> ByCompetency { get; set; } = new();
    /// <summary>Written comments without the reviewer's name — peers and direct reports speak freely.</summary>
    public List<string> Comments { get; set; } = new();
}

public class ReviewSubjectResultDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public List<ReviewGroupResultDto> Groups { get; set; } = new();
    /// <summary>Average of everyone except the employee's own self-review.</summary>
    public decimal? OthersAverage { get; set; }
    public decimal? SelfAverage { get; set; }
}

public interface IPerformanceService
{
    Task<List<ReviewCycleDto>> GetCyclesAsync(CancellationToken ct = default);
    Task<ReviewCycleDto> SaveCycleAsync(Guid? id, ReviewCycleDto dto, CancellationToken ct = default);
    Task<ReviewCycleDto> SetStatusAsync(Guid id, ReviewCycleStatus status, CancellationToken ct = default);
    Task<int> AddAssignmentsAsync(Guid cycleId, AddReviewAssignmentsDto dto, CancellationToken ct = default);
    Task<int> AddSelfReviewsAsync(Guid cycleId, CancellationToken ct = default);
    Task DeleteAssignmentAsync(Guid assignmentId, CancellationToken ct = default);
    Task<List<ReviewAssignmentDto>> GetAssignmentsAsync(Guid cycleId, CancellationToken ct = default);
    Task<List<ReviewAssignmentDto>> GetMineAsync(Guid employeeId, CancellationToken ct = default);
    Task<ReviewAssignmentDto> SubmitAsync(Guid employeeId, Guid assignmentId, SubmitReviewDto dto, CancellationToken ct = default);
    Task<List<ReviewSubjectResultDto>> GetResultsAsync(Guid cycleId, CancellationToken ct = default);
}

public class PerformanceService : IPerformanceService
{
    public static readonly string[] DefaultCompetencies =
    {
        "جودة العمل", "الالتزام بالمواعيد", "التعاون مع الفريق", "التواصل", "المبادرة وحل المشكلات"
    };

    private readonly IApplicationDbContext _context;

    public PerformanceService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReviewCycleDto>> GetCyclesAsync(CancellationToken ct = default)
    {
        var cycles = await _context.ReviewCycles.AsNoTracking().OrderByDescending(c => c.StartDate).ToListAsync(ct);
        var counts = await _context.ReviewAssignments.AsNoTracking()
            .GroupBy(a => a.ReviewCycleId)
            .Select(g => new { g.Key, Total = g.Count(), Done = g.Count(a => a.IsSubmitted) })
            .ToDictionaryAsync(x => x.Key, ct);
        return cycles.Select(c => MapCycle(c, counts.TryGetValue(c.Id, out var n) ? n.Total : 0, counts.TryGetValue(c.Id, out var m) ? m.Done : 0)).ToList();
    }

    public async Task<ReviewCycleDto> SaveCycleAsync(Guid? id, ReviewCycleDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationAppException("اكتب اسم دورة التقييم.");
        if (dto.EndDate.Date < dto.StartDate.Date) throw new ValidationAppException("تاريخ النهاية قبل البداية.");
        var competencies = dto.Competencies.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().Take(12).ToList();
        if (competencies.Count == 0) competencies = DefaultCompetencies.ToList();
        if (competencies.Any(c => c.Contains('\n'))) throw new ValidationAppException("كل بند في سطر لوحده.");

        ReviewCycle cycle;
        if (id is { } existing)
        {
            cycle = await _context.ReviewCycles.FirstOrDefaultAsync(c => c.Id == existing, ct) ?? throw new NotFoundException(nameof(ReviewCycle), existing);
            if (cycle.Status != ReviewCycleStatus.Draft && string.Join("\n", competencies) != cycle.Competencies)
                throw new ValidationAppException("بنود التقييم مينفعش تتغير بعد ما الدورة تبدأ.");
        }
        else
        {
            cycle = new ReviewCycle();
            _context.ReviewCycles.Add(cycle);
        }

        cycle.Name = TextLimit.Check(dto.Name, 200, "اسم الدورة")!;
        cycle.Description = TextLimit.Check(dto.Description, 1000, "الوصف");
        cycle.StartDate = dto.StartDate.Date;
        cycle.EndDate = dto.EndDate.Date;
        cycle.Competencies = string.Join("\n", competencies);
        await _context.SaveChangesAsync(ct);
        return MapCycle(cycle, await _context.ReviewAssignments.CountAsync(a => a.ReviewCycleId == cycle.Id, ct),
            await _context.ReviewAssignments.CountAsync(a => a.ReviewCycleId == cycle.Id && a.IsSubmitted, ct));
    }

    public async Task<ReviewCycleDto> SetStatusAsync(Guid id, ReviewCycleStatus status, CancellationToken ct = default)
    {
        var cycle = await _context.ReviewCycles.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException(nameof(ReviewCycle), id);
        if (!Enum.IsDefined(status)) throw new ValidationAppException("حالة غير صحيحة.");
        if (status == ReviewCycleStatus.Active && !await _context.ReviewAssignments.AnyAsync(a => a.ReviewCycleId == id, ct))
            throw new ValidationAppException("ضيف مقيّمين الأول قبل ما تبدأ الدورة.");
        if (cycle.Status == ReviewCycleStatus.Closed && status != ReviewCycleStatus.Closed)
            throw new ValidationAppException("الدورة المقفولة مبتتفتحش تاني.");
        cycle.Status = status;
        await _context.SaveChangesAsync(ct);
        return MapCycle(cycle, await _context.ReviewAssignments.CountAsync(a => a.ReviewCycleId == id, ct),
            await _context.ReviewAssignments.CountAsync(a => a.ReviewCycleId == id && a.IsSubmitted, ct));
    }

    public async Task<int> AddAssignmentsAsync(Guid cycleId, AddReviewAssignmentsDto dto, CancellationToken ct = default)
    {
        var cycle = await _context.ReviewCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct) ?? throw new NotFoundException(nameof(ReviewCycle), cycleId);
        if (cycle.Status == ReviewCycleStatus.Closed) throw new ValidationAppException("الدورة مقفولة.");
        if (!await _context.Employees.AnyAsync(e => e.Id == dto.SubjectEmployeeId && !e.IsDeleted, ct))
            throw new NotFoundException(nameof(Employee), dto.SubjectEmployeeId);

        var added = 0;
        foreach (var r in dto.Reviewers.GroupBy(x => x.ReviewerEmployeeId).Select(g => g.First()))
        {
            if (!Enum.IsDefined(r.Relationship)) throw new ValidationAppException("نوع علاقة غير صحيح.");
            if (r.Relationship == ReviewRelationship.Self && r.ReviewerEmployeeId != dto.SubjectEmployeeId)
                throw new ValidationAppException("التقييم الذاتي لازم يكون للموظف نفسه.");
            if (r.Relationship != ReviewRelationship.Self && r.ReviewerEmployeeId == dto.SubjectEmployeeId)
                throw new ValidationAppException("الموظف مينفعش يقيّم نفسه كمدير أو زميل.");
            if (!await _context.Employees.AnyAsync(e => e.Id == r.ReviewerEmployeeId && !e.IsDeleted, ct))
                throw new NotFoundException(nameof(Employee), r.ReviewerEmployeeId);
            if (await _context.ReviewAssignments.AnyAsync(a => a.ReviewCycleId == cycleId && a.SubjectEmployeeId == dto.SubjectEmployeeId && a.ReviewerEmployeeId == r.ReviewerEmployeeId, ct))
                continue;
            _context.ReviewAssignments.Add(new ReviewAssignment
            {
                ReviewCycleId = cycleId, SubjectEmployeeId = dto.SubjectEmployeeId, ReviewerEmployeeId = r.ReviewerEmployeeId, Relationship = r.Relationship
            });
            added++;
        }
        await _context.SaveChangesAsync(ct);
        return added;
    }

    public async Task<int> AddSelfReviewsAsync(Guid cycleId, CancellationToken ct = default)
    {
        var cycle = await _context.ReviewCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct) ?? throw new NotFoundException(nameof(ReviewCycle), cycleId);
        if (cycle.Status == ReviewCycleStatus.Closed) throw new ValidationAppException("الدورة مقفولة.");
        var employeeIds = await _context.Employees.AsNoTracking().Where(e => !e.IsDeleted && e.EmploymentStatus == EmploymentStatus.Active).Select(e => e.Id).ToListAsync(ct);
        var existing = (await _context.ReviewAssignments.AsNoTracking()
            .Where(a => a.ReviewCycleId == cycleId && a.Relationship == ReviewRelationship.Self).Select(a => a.SubjectEmployeeId).ToListAsync(ct)).ToHashSet();
        var added = 0;
        foreach (var id in employeeIds.Where(i => !existing.Contains(i)))
        {
            _context.ReviewAssignments.Add(new ReviewAssignment { ReviewCycleId = cycleId, SubjectEmployeeId = id, ReviewerEmployeeId = id, Relationship = ReviewRelationship.Self });
            added++;
        }
        await _context.SaveChangesAsync(ct);
        return added;
    }

    public async Task DeleteAssignmentAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var a = await _context.ReviewAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw new NotFoundException(nameof(ReviewAssignment), assignmentId);
        if (a.IsSubmitted) throw new ValidationAppException("التقييم ده اتسلّم، مينفعش يتحذف.");
        // The unique index also covers soft-deleted rows, so the row is removed outright.
        _context.ReviewAssignments.Remove(a);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ReviewAssignmentDto>> GetAssignmentsAsync(Guid cycleId, CancellationToken ct = default)
    {
        var rows = await LoadAssignments().Where(a => a.ReviewCycleId == cycleId).ToListAsync(ct);
        return rows.OrderBy(a => a.SubjectEmployee!.FullNameAr).ThenBy(a => a.Relationship).Select(a => MapAssignment(a, includeRatings: false)).ToList();
    }

    public async Task<List<ReviewAssignmentDto>> GetMineAsync(Guid employeeId, CancellationToken ct = default)
    {
        var rows = await LoadAssignments()
            .Where(a => a.ReviewerEmployeeId == employeeId && a.ReviewCycle!.Status == ReviewCycleStatus.Active).ToListAsync(ct);
        return rows.OrderBy(a => a.IsSubmitted).ThenBy(a => a.Relationship).Select(a => MapAssignment(a, includeRatings: true)).ToList();
    }

    public async Task<ReviewAssignmentDto> SubmitAsync(Guid employeeId, Guid assignmentId, SubmitReviewDto dto, CancellationToken ct = default)
    {
        var a = await LoadAssignments().FirstOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw new NotFoundException(nameof(ReviewAssignment), assignmentId);
        if (a.ReviewerEmployeeId != employeeId) throw new ValidationAppException("التقييم ده مش مطلوب منك.");
        if (a.ReviewCycle!.Status != ReviewCycleStatus.Active) throw new ValidationAppException("دورة التقييم دي مش شغالة دلوقتي.");
        if (a.IsSubmitted) throw new ValidationAppException("إنت سلّمت التقييم ده قبل كده.");

        var competencies = Split(a.ReviewCycle.Competencies);
        var ratings = new Dictionary<string, int>();
        foreach (var c in competencies)
        {
            if (!dto.Ratings.TryGetValue(c, out var score) || score < 1 || score > 5)
                throw new ValidationAppException($"قيّم \"{c}\" من ١ لـ ٥.");
            ratings[c] = score;
        }

        a.RatingsJson = JsonSerializer.Serialize(ratings);
        a.OverallRating = Math.Round((decimal)ratings.Values.Average(), 2);
        a.Comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : (dto.Comments.Trim().Length > 2000 ? dto.Comments.Trim()[..2000] : dto.Comments.Trim());
        a.IsSubmitted = true;
        a.SubmittedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return MapAssignment(a, includeRatings: true);
    }

    public async Task<List<ReviewSubjectResultDto>> GetResultsAsync(Guid cycleId, CancellationToken ct = default)
    {
        var cycle = await _context.ReviewCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId, ct) ?? throw new NotFoundException(nameof(ReviewCycle), cycleId);
        var competencies = Split(cycle.Competencies);
        var rows = await LoadAssignments().Where(a => a.ReviewCycleId == cycleId && a.IsSubmitted).ToListAsync(ct);

        return rows.GroupBy(a => a.SubjectEmployeeId).Select(g =>
        {
            var groups = g.GroupBy(a => a.Relationship).OrderBy(x => x.Key).Select(grp =>
            {
                var parsed = grp.Select(a => Parse(a.RatingsJson)).ToList();
                return new ReviewGroupResultDto
                {
                    Relationship = grp.Key,
                    Count = grp.Count(),
                    Average = grp.Any(a => a.OverallRating.HasValue) ? Math.Round(grp.Where(a => a.OverallRating.HasValue).Average(a => a.OverallRating!.Value), 2) : null,
                    ByCompetency = competencies.Where(c => parsed.Any(p => p.ContainsKey(c)))
                        .ToDictionary(c => c, c => Math.Round((decimal)parsed.Where(p => p.ContainsKey(c)).Average(p => p[c]), 2)),
                    Comments = grp.Where(a => !string.IsNullOrWhiteSpace(a.Comments)).Select(a => a.Comments!).ToList()
                };
            }).ToList();

            var others = g.Where(a => a.Relationship != ReviewRelationship.Self && a.OverallRating.HasValue).ToList();
            var self = g.FirstOrDefault(a => a.Relationship == ReviewRelationship.Self);
            return new ReviewSubjectResultDto
            {
                EmployeeId = g.Key,
                EmployeeName = g.First().SubjectEmployee!.FullNameAr,
                Groups = groups,
                OthersAverage = others.Count > 0 ? Math.Round(others.Average(a => a.OverallRating!.Value), 2) : null,
                SelfAverage = self?.OverallRating
            };
        }).OrderBy(r => r.EmployeeName).ToList();
    }

    private IQueryable<ReviewAssignment> LoadAssignments()
        => _context.ReviewAssignments.Include(a => a.ReviewCycle).Include(a => a.SubjectEmployee).Include(a => a.ReviewerEmployee);

    private static List<string> Split(string competencies) => competencies.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static Dictionary<string, int> Parse(string? json)
        => string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();

    private static ReviewCycleDto MapCycle(ReviewCycle c, int total, int done) => new()
    {
        Id = c.Id, Name = c.Name, Description = c.Description, StartDate = c.StartDate, EndDate = c.EndDate, Status = c.Status,
        Competencies = Split(c.Competencies), TotalReviews = total, SubmittedReviews = done
    };

    private static ReviewAssignmentDto MapAssignment(ReviewAssignment a, bool includeRatings) => new()
    {
        Id = a.Id, ReviewCycleId = a.ReviewCycleId, CycleName = a.ReviewCycle?.Name ?? string.Empty,
        SubjectEmployeeId = a.SubjectEmployeeId, SubjectName = a.SubjectEmployee?.FullNameAr ?? string.Empty,
        ReviewerEmployeeId = a.ReviewerEmployeeId, ReviewerName = a.ReviewerEmployee?.FullNameAr ?? string.Empty,
        Relationship = a.Relationship, IsSubmitted = a.IsSubmitted, OverallRating = a.OverallRating,
        Competencies = a.ReviewCycle is null ? new() : Split(a.ReviewCycle.Competencies),
        Ratings = includeRatings ? Parse(a.RatingsJson) : new(),
        Comments = includeRatings ? a.Comments : null
    };
}
