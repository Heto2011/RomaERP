using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Assistant.DTOs;
using RomaERP.Application.Assistant.Services;
using RomaERP.Application.Common.Exceptions;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Accountant")]
[Route("api/[controller]")]
public class AiAssistantController : ControllerBase
{
    private readonly IExpenseAssistantService _assistantService;
    private readonly IWebHostEnvironment _environment;

    public AiAssistantController(IExpenseAssistantService assistantService, IWebHostEnvironment environment)
    {
        _assistantService = assistantService;
        _environment = environment;
    }

    [HttpPost("messages")]
    public async Task<ActionResult<ChatTurnResponseDto>> SendMessage(ChatTurnRequestDto request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        return Ok(await _assistantService.SendMessageAsync(request, userId, ct));
    }

    [HttpGet("pending-reconciliation")]
    public async Task<ActionResult<List<ExpenseCaptureDto>>> GetPendingReconciliation(CancellationToken ct)
        => Ok(await _assistantService.GetPendingReconciliationAsync(ct));

    [HttpGet("pending-approval")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ExpenseCaptureDto>>> GetPendingApproval(CancellationToken ct)
        => Ok(await _assistantService.GetPendingApprovalAsync(ct));

    [HttpPost("captures/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ExpenseCaptureDto>> Approve(Guid id, CancellationToken ct)
        => Ok(await _assistantService.ApproveAsync(id, ct));

    [HttpPost("captures/{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ExpenseCaptureDto>> Reject(Guid id, CancellationToken ct)
        => Ok(await _assistantService.RejectAsync(id, ct));

    [HttpPost("captures/from-receipt")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ChatTurnResponseDto>> StartFromReceipt(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new ValidationAppException("الملف المرفوع فارغ.");

        var mediaType = file.ContentType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => "image/jpeg",
            "image/png" => "image/png",
            "image/webp" => "image/webp",
            "image/gif" => "image/gif",
            _ => throw new ValidationAppException("صيغة الصورة غير مدعومة — لازم تكون JPEG أو PNG أو WEBP أو GIF.")
        };

        byte[] imageBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream, ct);
            imageBytes = memoryStream.ToArray();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var response = await _assistantService.StartFromReceiptImageAsync(imageBytes, mediaType, userId, ct);

        var uploadsDir = Path.Combine(_environment.ContentRootPath, "App_Data", "expense-proofs");
        Directory.CreateDirectory(uploadsDir);
        var storedFileName = $"{response.CaptureId}{Path.GetExtension(file.FileName)}";
        await using (var stream = System.IO.File.Create(Path.Combine(uploadsDir, storedFileName)))
        {
            await file.CopyToAsync(stream, ct);
        }
        await _assistantService.AttachProofAsync(response.CaptureId, file.FileName, storedFileName, ct);

        return Ok(response);
    }

    [HttpPost("captures/{id:guid}/proof")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ExpenseCaptureDto>> UploadProof(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new ValidationAppException("الملف المرفوع فارغ.");

        var uploadsDir = Path.Combine(_environment.ContentRootPath, "App_Data", "expense-proofs");
        Directory.CreateDirectory(uploadsDir);

        var storedFileName = $"{id}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(uploadsDir, storedFileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        return Ok(await _assistantService.AttachProofAsync(id, file.FileName, storedFileName, ct));
    }
}
