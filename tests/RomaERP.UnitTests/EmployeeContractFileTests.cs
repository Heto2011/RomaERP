using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using RomaERP.API.Controllers;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Application.HR.DTOs;
using RomaERP.Application.HR.Services;
using RomaERP.Domain.Tenancy;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>Signed contracts are stored as PDFs on disk, one folder per company; anything that isn't a real PDF is refused.</summary>
public class EmployeeContractFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "roma-contract-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _contractId = Guid.NewGuid();

    public EmployeeContractFileTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class FakeContracts(Guid id) : IEmployeeContractService
    {
        public List<EmployeeContractDto> Items { get; } = [new EmployeeContractDto { Id = id }];
        public Task<List<EmployeeContractDto>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items);
        public Task<List<EmployeeContractDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken ct = default) => Task.FromResult(Items);
        public Task<EmployeeContractDto> CreateAsync(CreateEmployeeContractDto dto, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EmployeeContractDto> UpdateStatusAsync(Guid id, UpdateEmployeeContractStatusDto dto, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
        public Task<List<EmployeeContractDto>> GetExpiringAsync(int days, CancellationToken ct = default) => Task.FromResult(new List<EmployeeContractDto>());
    }

    private sealed class FakeTenant(string code) : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public string CompanyCode { get; } = code;
        public string ConnectionString => "";
        public Country Country => default;
        public ProductScope ProductScope => default;
        public bool IsResolved => true;
    }

    private sealed class FakeEnv(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "t";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private EmployeeContractsController Build(string company = "acme-1234") =>
        new(new FakeContracts(_contractId), new FakeEnv(_root), new FakeTenant(company));

    private static IFormFile Pdf(string name, byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    private static byte[] RealPdf() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF");

    [Fact]
    public async Task Upload_AcceptsRealPdf_AndFlagsContractAndServesIt()
    {
        var c = Build();
        Assert.IsType<NoContentResult>(await c.UploadFile(_contractId, Pdf("contract.pdf", RealPdf()), default));

        var list = Assert.IsType<OkObjectResult>((await c.GetAll(default)).Result);
        Assert.True(((List<EmployeeContractDto>)list.Value!).Single().HasFile);

        var download = await c.DownloadFile(_contractId, default);
        var file = Assert.IsType<PhysicalFileResult>(download);
        Assert.Equal("application/pdf", file.ContentType);
    }

    [Fact]
    public async Task Upload_RejectsNonPdfExtension()
    {
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            Build().UploadFile(_contractId, Pdf("contract.docx", RealPdf()), default));
    }

    [Fact]
    public async Task Upload_RejectsFakePdfWithWrongContent()
    {
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            Build().UploadFile(_contractId, Pdf("contract.pdf", Encoding.ASCII.GetBytes("MZ not a pdf at all")), default));
    }

    [Fact]
    public async Task Upload_RejectsEmptyAndOversizedFiles()
    {
        var c = Build();
        await Assert.ThrowsAsync<ValidationAppException>(() => c.UploadFile(_contractId, Pdf("a.pdf", []), default));
        var big = new byte[10 * 1024 * 1024 + 1];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(big, 0);
        await Assert.ThrowsAsync<ValidationAppException>(() => c.UploadFile(_contractId, Pdf("a.pdf", big), default));
    }

    [Fact]
    public async Task Upload_UnknownContract_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Build().UploadFile(Guid.NewGuid(), Pdf("a.pdf", RealPdf()), default));
    }

    [Fact]
    public async Task Files_AreIsolatedPerCompany()
    {
        await Build("acme-1234").UploadFile(_contractId, Pdf("a.pdf", RealPdf()), default);
        Assert.IsType<NotFoundResult>(await Build("other-9999").DownloadFile(_contractId, default));
    }

    [Fact]
    public async Task Delete_RemovesTheStoredFile()
    {
        var c = Build();
        await c.UploadFile(_contractId, Pdf("a.pdf", RealPdf()), default);
        await c.Delete(_contractId, default);
        Assert.Empty(Directory.GetFiles(_root, "*.pdf", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DeleteFile_RemovesJustTheAttachment()
    {
        var c = Build();
        await c.UploadFile(_contractId, Pdf("a.pdf", RealPdf()), default);
        await c.DeleteFile(_contractId, default);
        Assert.IsType<NotFoundResult>(await c.DownloadFile(_contractId, default));
    }
}
