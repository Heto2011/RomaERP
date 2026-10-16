using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

/// <summary>The company's public careers page: no login. The company is identified by the X-Company-Code header the page sends.
/// Only open jobs are visible, and applying is rate-limited per IP.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/careers")]
public class CareersController : ControllerBase
{
    private readonly IRecruitmentService _service;

    public CareersController(IRecruitmentService service)
    {
        _service = service;
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<List<PublicJobDto>>> GetJobs(CancellationToken ct) => Ok(await _service.GetOpenJobsAsync(ct));

    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<PublicJobDto>> GetJob(Guid id, CancellationToken ct) => Ok(await _service.GetOpenJobAsync(id, ct));

    [HttpPost("jobs/{id:guid}/apply")]
    [EnableRateLimiting("careers-apply")]
    [RequestSizeLimit(200 * 1024)]
    public async Task<IActionResult> Apply(Guid id, CareerApplicationDto dto, CancellationToken ct)
    {
        await _service.ApplyAsync(id, dto, ct);
        return NoContent();
    }
}
