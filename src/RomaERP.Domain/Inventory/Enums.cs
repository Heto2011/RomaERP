namespace RomaERP.Domain.Inventory;

public enum StockMovementType
{
    Receipt = 1,
    Issue = 2,
    /// <summary>Stock leaving a branch for another branch of the same company. No journal entry.</summary>
    TransferOut = 3,
    /// <summary>Stock arriving at a branch from another branch (or coming back after a cancelled transfer). No journal entry.</summary>
    TransferIn = 4,
}
