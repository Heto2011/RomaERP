using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.DTOs;

/// <summary>One account line of a chart of accounts being imported. The customer can edit every field in the preview
/// before approving, so the same shape is read from the file, shown, corrected and sent back.</summary>
public class ChartImportRowDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public AccountType AccountType { get; set; } = AccountType.Asset;
    public AccountNature Nature { get; set; } = AccountNature.Debit;
    /// <summary>Code of the parent account, empty for a top-level account.</summary>
    public string? ParentCode { get; set; }
    public bool IsControlAccount { get; set; }

    /// <summary>Filled by the server: why this line cannot be imported as it stands (blocks approval).</summary>
    public List<string> Errors { get; set; } = new();
    /// <summary>Filled by the server: something worth a second look that does not block approval.</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>What approving the import would do to the company's current chart, so nothing is a surprise.</summary>
public class ChartImportSummaryDto
{
    public int NewAccounts { get; set; }
    public int UpdatedAccounts { get; set; }
    /// <summary>Old accounts that are not in the file and have no movements or links: they are removed.</summary>
    public int RemovedAccounts { get; set; }
    /// <summary>Old accounts not in the file that have entries (or are needed as a parent): kept, switched off, so history stays intact.</summary>
    public int DeactivatedAccounts { get; set; }
    /// <summary>Accounts the system itself posts to (cash, customers, sales, payroll...): kept as they are even if missing from the file.</summary>
    public List<string> KeptForSystem { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class ChartImportPreviewDto
{
    public List<ChartImportRowDto> Rows { get; set; } = new();
    public ChartImportSummaryDto Summary { get; set; } = new();
    public bool CanApply { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<string> FileNotes { get; set; } = new();
}

public class ChartImportApplyRequest
{
    public List<ChartImportRowDto> Rows { get; set; } = new();
}

public class ChartImportResultDto
{
    public ChartImportSummaryDto Summary { get; set; } = new();
}
