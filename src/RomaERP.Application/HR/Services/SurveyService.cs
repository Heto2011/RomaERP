using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.HR;

namespace RomaERP.Application.HR.Services;

public class SurveyQuestionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public SurveyQuestionType Type { get; set; }
}

public class SurveyDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsAnonymous { get; set; } = true;
    public SurveyStatus Status { get; set; }
    public List<SurveyQuestionDto> Questions { get; set; } = new();
    public int ResponseCount { get; set; }
    /// <summary>For the signed-in employee: have they already answered?</summary>
    public bool Answered { get; set; }
}

public class SurveyAnswerInputDto
{
    public Guid QuestionId { get; set; }
    public int? NumberValue { get; set; }
    public string? TextValue { get; set; }
}

public class SubmitSurveyDto
{
    public List<SurveyAnswerInputDto> Answers { get; set; } = new();
}

public class SurveyQuestionResultDto
{
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public SurveyQuestionType Type { get; set; }
    public int Answers { get; set; }
    public decimal? Average { get; set; }
    /// <summary>Rating: count per 1..5. Yes/No: key 1 = yes, 0 = no.</summary>
    public Dictionary<int, int> Distribution { get; set; } = new();
    public List<string> Texts { get; set; } = new();
}

public class SurveyResultsDto
{
    public Guid SurveyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ResponseCount { get; set; }
    public int InvitedCount { get; set; }
    /// <summary>True when written answers are withheld because too few people answered to keep them anonymous.</summary>
    public bool TextHidden { get; set; }
    public List<SurveyQuestionResultDto> Questions { get; set; } = new();
}

public interface ISurveyService
{
    Task<List<SurveyDto>> GetSurveysAsync(CancellationToken ct = default);
    Task<SurveyDto> SaveSurveyAsync(Guid? id, SurveyDto dto, CancellationToken ct = default);
    Task<SurveyDto> SetStatusAsync(Guid id, SurveyStatus status, CancellationToken ct = default);
    Task DeleteSurveyAsync(Guid id, CancellationToken ct = default);
    Task<List<SurveyDto>> GetOpenForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task SubmitAsync(Guid employeeId, Guid surveyId, SubmitSurveyDto dto, CancellationToken ct = default);
    Task<SurveyResultsDto> GetResultsAsync(Guid surveyId, CancellationToken ct = default);
}

public class SurveyService : ISurveyService
{
    /// <summary>Written answers of an anonymous survey stay hidden until at least this many people have answered.</summary>
    public const int MinResponsesForText = 3;

    private readonly IApplicationDbContext _context;

    public SurveyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SurveyDto>> GetSurveysAsync(CancellationToken ct = default)
    {
        var surveys = await _context.Surveys.AsNoTracking().OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        var questions = await _context.SurveyQuestions.AsNoTracking().ToListAsync(ct);
        var counts = await _context.SurveyResponses.AsNoTracking().GroupBy(r => r.SurveyId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return surveys.Select(s => Map(s, questions.Where(q => q.SurveyId == s.Id), counts.GetValueOrDefault(s.Id), false)).ToList();
    }

    public async Task<SurveyDto> SaveSurveyAsync(Guid? id, SurveyDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ValidationAppException("اكتب عنوان الاستبيان.");
        var questions = dto.Questions.Where(q => !string.IsNullOrWhiteSpace(q.Text)).ToList();
        if (questions.Count == 0) throw new ValidationAppException("ضيف سؤال واحد على الأقل.");
        if (questions.Count > 30) throw new ValidationAppException("الحد الأقصى ٣٠ سؤال.");
        if (questions.Any(q => !Enum.IsDefined(q.Type))) throw new ValidationAppException("نوع سؤال غير صحيح.");

        Survey survey;
        if (id is { } existing)
        {
            survey = await _context.Surveys.FirstOrDefaultAsync(s => s.Id == existing, ct) ?? throw new NotFoundException(nameof(Survey), existing);
            if (survey.Status != SurveyStatus.Draft) throw new ValidationAppException("الاستبيان بعد ما يتفتح مينفعش يتعدّل.");
            var old = await _context.SurveyQuestions.Where(q => q.SurveyId == existing).ToListAsync(ct);
            _context.SurveyQuestions.RemoveRange(old);
        }
        else
        {
            survey = new Survey();
            _context.Surveys.Add(survey);
        }

        survey.Title = dto.Title.Trim();
        survey.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        survey.IsAnonymous = dto.IsAnonymous;
        await _context.SaveChangesAsync(ct);

        var order = 1;
        foreach (var q in questions)
            _context.SurveyQuestions.Add(new SurveyQuestion { SurveyId = survey.Id, Order = order++, Text = q.Text.Trim(), Type = q.Type });
        await _context.SaveChangesAsync(ct);

        var saved = await _context.SurveyQuestions.AsNoTracking().Where(q => q.SurveyId == survey.Id).ToListAsync(ct);
        return Map(survey, saved, 0, false);
    }

    public async Task<SurveyDto> SetStatusAsync(Guid id, SurveyStatus status, CancellationToken ct = default)
    {
        var survey = await _context.Surveys.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(nameof(Survey), id);
        if (!Enum.IsDefined(status)) throw new ValidationAppException("حالة غير صحيحة.");
        if (survey.Status == SurveyStatus.Closed && status != SurveyStatus.Closed) throw new ValidationAppException("الاستبيان المقفول مبيتفتحش تاني.");
        survey.Status = status;
        await _context.SaveChangesAsync(ct);
        var questions = await _context.SurveyQuestions.AsNoTracking().Where(q => q.SurveyId == id).ToListAsync(ct);
        return Map(survey, questions, await _context.SurveyResponses.CountAsync(r => r.SurveyId == id, ct), false);
    }

    public async Task DeleteSurveyAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _context.Surveys.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(nameof(Survey), id);
        if (await _context.SurveyResponses.AnyAsync(r => r.SurveyId == id, ct))
            throw new ValidationAppException("الاستبيان عليه ردود. اقفله بدل ما تحذفه.");
        survey.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<SurveyDto>> GetOpenForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var surveys = await _context.Surveys.AsNoTracking().Where(s => s.Status == SurveyStatus.Open).OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        var ids = surveys.Select(s => s.Id).ToList();
        var questions = await _context.SurveyQuestions.AsNoTracking().Where(q => ids.Contains(q.SurveyId)).ToListAsync(ct);
        var answered = (await _context.SurveyParticipations.AsNoTracking().Where(p => p.EmployeeId == employeeId && ids.Contains(p.SurveyId)).Select(p => p.SurveyId).ToListAsync(ct)).ToHashSet();
        return surveys.Select(s => Map(s, questions.Where(q => q.SurveyId == s.Id), 0, answered.Contains(s.Id))).ToList();
    }

    public async Task SubmitAsync(Guid employeeId, Guid surveyId, SubmitSurveyDto dto, CancellationToken ct = default)
    {
        var survey = await _context.Surveys.FirstOrDefaultAsync(s => s.Id == surveyId, ct) ?? throw new NotFoundException(nameof(Survey), surveyId);
        if (survey.Status != SurveyStatus.Open) throw new ValidationAppException("الاستبيان ده مش مفتوح.");
        if (await _context.SurveyParticipations.AnyAsync(p => p.SurveyId == surveyId && p.EmployeeId == employeeId, ct))
            throw new ValidationAppException("إنت جاوبت على الاستبيان ده قبل كده.");

        var questions = await _context.SurveyQuestions.Where(q => q.SurveyId == surveyId).OrderBy(q => q.Order).ToListAsync(ct);
        var answers = new List<SurveyAnswer>();
        // For an anonymous survey the time is cut to the day, so an answer can't be matched to the moment a person took part.
        var submittedAt = survey.IsAnonymous ? DateTime.UtcNow.Date : DateTime.UtcNow;
        var response = new SurveyResponse { SurveyId = surveyId, EmployeeId = survey.IsAnonymous ? null : employeeId, SubmittedAtUtc = submittedAt, CreatedAtUtc = submittedAt };
        foreach (var q in questions)
        {
            var input = dto.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
            switch (q.Type)
            {
                case SurveyQuestionType.Rating:
                    if (input?.NumberValue is not { } n || n < 1 || n > 5) throw new ValidationAppException($"قيّم \"{q.Text}\" من ١ لـ ٥.");
                    answers.Add(new SurveyAnswer { SurveyResponse = response, SurveyQuestionId = q.Id, NumberValue = n, CreatedAtUtc = submittedAt });
                    break;
                case SurveyQuestionType.YesNo:
                    if (input?.NumberValue is not { } yn || (yn != 0 && yn != 1)) throw new ValidationAppException($"جاوب بنعم أو لا على \"{q.Text}\".");
                    answers.Add(new SurveyAnswer { SurveyResponse = response, SurveyQuestionId = q.Id, NumberValue = yn, CreatedAtUtc = submittedAt });
                    break;
                default:
                    var text = input?.TextValue?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        answers.Add(new SurveyAnswer { SurveyResponse = response, SurveyQuestionId = q.Id, TextValue = text.Length > 2000 ? text[..2000] : text, CreatedAtUtc = submittedAt });
                    break;
            }
        }

        _context.SurveyResponses.Add(response);
        _context.SurveyAnswers.AddRange(answers);
        _context.SurveyParticipations.Add(new SurveyParticipation { SurveyId = surveyId, EmployeeId = employeeId });
        await _context.SaveChangesAsync(ct);
    }

    public async Task<SurveyResultsDto> GetResultsAsync(Guid surveyId, CancellationToken ct = default)
    {
        var survey = await _context.Surveys.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surveyId, ct) ?? throw new NotFoundException(nameof(Survey), surveyId);
        var questions = await _context.SurveyQuestions.AsNoTracking().Where(q => q.SurveyId == surveyId).OrderBy(q => q.Order).ToListAsync(ct);
        var responseIds = await _context.SurveyResponses.AsNoTracking().Where(r => r.SurveyId == surveyId).Select(r => r.Id).ToListAsync(ct);
        var answers = await _context.SurveyAnswers.AsNoTracking().Where(a => responseIds.Contains(a.SurveyResponseId)).ToListAsync(ct);
        var invited = await _context.Employees.CountAsync(e => !e.IsDeleted && e.EmploymentStatus == EmploymentStatus.Active, ct);

        var hideText = survey.IsAnonymous && responseIds.Count < MinResponsesForText;
        var results = questions.Select(q =>
        {
            var qa = answers.Where(a => a.SurveyQuestionId == q.Id).ToList();
            var numbers = qa.Where(a => a.NumberValue.HasValue).Select(a => a.NumberValue!.Value).ToList();
            return new SurveyQuestionResultDto
            {
                QuestionId = q.Id, Text = q.Text, Type = q.Type, Answers = qa.Count,
                Average = q.Type == SurveyQuestionType.Rating && numbers.Count > 0 ? Math.Round((decimal)numbers.Average(), 2) : null,
                Distribution = numbers.GroupBy(x => x).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
                Texts = q.Type == SurveyQuestionType.Text && !hideText ? qa.Where(a => !string.IsNullOrWhiteSpace(a.TextValue)).Select(a => a.TextValue!).ToList() : new()
            };
        }).ToList();

        return new SurveyResultsDto { SurveyId = surveyId, Title = survey.Title, ResponseCount = responseIds.Count, InvitedCount = invited, TextHidden = hideText, Questions = results };
    }

    private static SurveyDto Map(Survey s, IEnumerable<SurveyQuestion> questions, int responses, bool answered) => new()
    {
        Id = s.Id, Title = s.Title, Description = s.Description, IsAnonymous = s.IsAnonymous, Status = s.Status,
        Questions = questions.OrderBy(q => q.Order).Select(q => new SurveyQuestionDto { Id = q.Id, Text = q.Text, Type = q.Type }).ToList(),
        ResponseCount = responses, Answered = answered
    };
}
