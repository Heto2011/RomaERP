namespace RomaERP.Application.Restaurant.DTOs;

public class MenuImportRowDto
{
    public string Category { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public decimal Price { get; set; }
}

public class MenuImportRequestDto
{
    public List<MenuImportRowDto> Rows { get; set; } = new();

    /// <summary>False = preview only (nothing is saved). True = save.</summary>
    public bool Apply { get; set; }

    /// <summary>When a dish already on the menu has a different price, update it. Otherwise it is left as is.</summary>
    public bool UpdateExistingPrices { get; set; }
}

public class MenuImportLineResultDto
{
    public int Row { get; set; }
    public string Category { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>"create" | "update" | "same" | "error"</summary>
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class MenuImportResultDto
{
    public bool Applied { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int NewCategories { get; set; }
    public int Errors { get; set; }
    public List<MenuImportLineResultDto> Lines { get; set; } = new();
}

public class MenuScanResultDto
{
    public List<MenuImportRowDto> Rows { get; set; } = new();

    /// <summary>Dishes the reader found but could not read a price for — they are left out so nothing is guessed.</summary>
    public int SkippedWithoutPrice { get; set; }
    public string? Note { get; set; }
}
