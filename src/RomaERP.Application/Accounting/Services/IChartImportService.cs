using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IChartImportService
{
    /// <summary>Reads an Excel (.xlsx) or CSV file into editable lines, checks them and says what approving would change.</summary>
    Task<ChartImportPreviewDto> PreviewFileAsync(Stream file, string fileName, CancellationToken ct = default);

    /// <summary>Checks lines the customer edited in the preview (nothing is saved).</summary>
    Task<ChartImportPreviewDto> PreviewRowsAsync(List<ChartImportRowDto> rows, CancellationToken ct = default);

    /// <summary>Makes the uploaded chart the company's chart. Old accounts with entries are kept switched off, never deleted.</summary>
    Task<ChartImportResultDto> ApplyAsync(List<ChartImportRowDto> rows, CancellationToken ct = default);

    /// <summary>An Excel file with the expected columns and a few example lines.</summary>
    byte[] BuildTemplate();
}
