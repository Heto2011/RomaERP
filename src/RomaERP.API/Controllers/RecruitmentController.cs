using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.HRPolicy)]
[Route("api/recruitment")]
public class RecruitmentController : ControllerBase
{
    private readonly IRecruitmentService _service;

    public RecruitmentController(IRecruitmentService service)
    {
        _service = service;
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<List<JobPostingDto>>> GetJobs(CancellationToken ct) => Ok(await _service.GetJobsAsync(ct));

    [HttpPost("jobs")]
    public async Task<ActionResult<JobPostingDto>> CreateJob(JobPostingDto dto, CancellationToken ct) => Ok(await _service.SaveJobAsync(null, dto, ct));

    [HttpPut("jobs/{id:guid}")]
    public async Task<ActionResult<JobPostingDto>> UpdateJob(Guid id, JobPostingDto dto, CancellationToken ct) => Ok(await _service.SaveJobAsync(id, dto, ct));

    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id, CancellationToken ct)
    {
        await _service.DeleteJobAsync(id, ct);
        return NoContent();
    }

    [HttpGet("candidates")]
    public async Task<ActionResult<List<CandidateDto>>> GetCandidates([FromQuery] Guid? jobId, CancellationToken ct) => Ok(await _service.GetCandidatesAsync(jobId, ct));

    [HttpPost("candidates")]
    public async Task<ActionResult<CandidateDto>> AddCandidate(SaveCandidateDto dto, CancellationToken ct) => Ok(await _service.AddCandidateAsync(dto, "Manual", ct));

    [HttpPut("candidates/{id:guid}")]
    public async Task<ActionResult<CandidateDto>> UpdateCandidate(Guid id, UpdateCandidateDto dto, CancellationToken ct) => Ok(await _service.UpdateCandidateAsync(id, dto, ct));

    [HttpDelete("candidates/{id:guid}")]
    public async Task<IActionResult> DeleteCandidate(Guid id, CancellationToken ct)
    {
        await _service.DeleteCandidateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("candidates/{id:guid}/screen")]
    public async Task<ActionResult<CandidateDto>> Screen(Guid id, CancellationToken ct) => Ok(await _service.ScreenCandidateAsync(id, ct));
}
