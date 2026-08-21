namespace RomaERP.Application.Inventory.DTOs;

public class ItemCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateItemCategoryDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
}

public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateWarehouseDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
}

public class ItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid ItemCategoryId { get; set; }
    public string ItemCategoryName { get; set; } = string.Empty;
    public decimal ReorderLevel { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal AverageCost { get; set; }
    public bool IsActive { get; set; }
}

public class CreateItemDto
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid ItemCategoryId { get; set; }
    public decimal ReorderLevel { get; set; }
}
