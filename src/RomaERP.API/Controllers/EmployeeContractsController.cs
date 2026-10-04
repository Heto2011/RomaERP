using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomaERP.Application.Common;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;

namespace RomaERP.API.Controllers;

[ApiController]
[Authorize(Policy = ModulePermissions.HRPolicy)]
[Route("api/employee-contracts")]
public class EmployeeContractsController : ControllerBase
{
    private const long MaxPdfBytes = 10 * 1024 * 1024;

    private readonly IEmployeeContractService _contractService;
    private readonly IWebHostEnvironment _environment;
    private readonly ITenantContext _tenant;

    public EmployeeContractsController(
        IEmployeeContractService contractService,
        IWebHostEnvironment environment,
        ITenantContext tenant)
    {
        _contractService = contractService;
        _environment = environment;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeeContractDto>>> GetAll(CancellationToken ct)
        => Ok(WithFileFlags(await _contractService.GetAllAsync(ct)));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<List<EmployeeContractDto>>> GetForEmployee(Guid employeeId, CancellationToken ct)
        => Ok(WithFileFlags(await _contractService.GetForEmployeeAsync(employeeId, ct)));

    [HttpPost]
    public async Task<ActionResult<EmployeeContractDto>> Create(CreateEmployeeContractDto dto, CancellationToken ct)
        => Ok(await _contractService.CreateAsync(dto, ct));

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<EmployeeContractDto>> UpdateStatus(Guid id, UpdateEmployeeContractStatusDto dto, CancellationToken ct)
        => Ok(WithFileFlag(await _contractService.UpdateStatusAsync(id, dto, ct)));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _contractService.DeleteAsync(id, ct);
        DeleteFileIfExists(id);
        return NoContent();
    }

    /// <summary>Attaches the signed contract as a PDF (replaces any earlier upload).</summary>
    [HttpPost("{id:guid}/file")]
    [RequestSizeLimit(MaxPdfBytes)]
    public async Task<IActionResult> UploadFile(Guid id, IFormFile file, CancellationToken ct)
    {
        await EnsureContractExistsAsync(id, ct);

        if (file.Length == 0)
            throw new ValidationAppException("الملف المرفوع فارغ.");
        if (file.Length > MaxPdfBytes)
            throw new ValidationAppException("حجم الملف أكبر من 10 ميجابايت.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException("ملف العقد لازم يكون PDF.");

        // Extension and content-type are client-supplied, so also check the real PDF signature.
        var header = new byte[5];
        await using (var probe = file.OpenReadStream())
        {
            var read = await probe.ReadAsync(header.AsMemory(0, 5), ct);
            if (read < 5 || System.Text.Encoding.ASCII.GetString(header) != "%PDF-")
                throw new ValidationAppException("ملف العقد لازم يكون PDF.");
        }

        var path = FilePath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var stream = System.IO.File.Create(path))
        {
            await file.CopyToAsync(stream, ct);
        }

        return NoContent();
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        await EnsureContractExistsAsync(id, ct);
        var path = FilePath(id);
        if (!System.IO.File.Exists(path))
            return NotFound();
        return PhysicalFile(path, "application/pdf", $"contract-{id}.pdf");
    }

    [HttpDelete("{id:guid}/file")]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken ct)
    {
        await EnsureContractExistsAsync(id, ct);
        DeleteFileIfExists(id);
        return NoContent();
    }

    private async Task EnsureContractExistsAsync(Guid id, CancellationToken ct)
    {
        var all = await _contractService.GetAllAsync(ct);
        if (all.All(c => c.Id != id))
            throw new NotFoundException("EmployeeContract", id);
    }

    // One folder per company so a file can never be reached through another tenant's contract id.
    private string FilePath(Guid contractId)
    {
        var tenantFolder = new string((_tenant.CompanyCode ?? string.Empty)
            .ToLowerInvariant().Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-').ToArray());
        if (tenantFolder.Length == 0)
            tenantFolder = _tenant.TenantId.ToString("N");
        return Path.Combine(_environment.ContentRootPath, "App_Data", "employee-contracts", tenantFolder, $"{contractId}.pdf");
    }

    private void DeleteFileIfExists(Guid id)
    {
        var path = FilePath(id);
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private EmployeeContractDto WithFileFlag(EmployeeContractDto dto)
    {
        dto.HasFile = System.IO.File.Exists(FilePath(dto.Id));
        return dto;
    }

    private List<EmployeeContractDto> WithFileFlags(List<EmployeeContractDto> list)
    {
        foreach (var c in list) WithFileFlag(c);
        return list;
    }
}
