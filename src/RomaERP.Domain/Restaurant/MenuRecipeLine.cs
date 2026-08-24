using RomaERP.Domain.Common;
using RomaERP.Domain.Inventory;

namespace RomaERP.Domain.Restaurant;

/// <summary>One raw-material ingredient consumed when a menu item is sold, and how much of it one unit of
/// the menu item uses (e.g. 1 "برجر لحم" consumes 0.2 kg of "لحمة مفروم" + 1 "خبز برجر"). A menu Item with no
/// recipe lines is treated as its own raw material — decremented directly, for simple resale items (a
/// bottled drink) that don't need a bill of materials.</summary>
public class MenuRecipeLine : BaseEntity
{
    public Guid MenuItemId { get; set; }
    public Item? MenuItem { get; set; }

    public Guid RawMaterialItemId { get; set; }
    public Item? RawMaterialItem { get; set; }

    public decimal QuantityPerUnit { get; set; }
}
