using Microsoft.EntityFrameworkCore;
using RomaERP.Application.Accounting.DTOs;
using RomaERP.Application.Common.Exceptions;
using RomaERP.Application.Common.Interfaces;
using RomaERP.Domain.Accounting;

namespace RomaERP.Application.Accounting.Services;

public class FixedAssetService : IFixedAssetService
{
    private readonly IApplicationDbContext _context;

    public FixedAssetService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<FixedAssetDto>> GetAllAsync(CancellationToken ct = default)
    {
        var assets = await _context.FixedAssets
            .AsNoTracking()
            .Include(a => a.AssetAccount)
            .Include(a => a.AccumulatedDepreciationAccount)
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Code)
            .ToListAsync(ct);

        return assets.Select(Map).ToList();
    }

    public async Task<FixedAssetDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await _context.FixedAssets
            .AsNoTracking()
            .Include(a => a.AssetAccount)
            .Include(a => a.AccumulatedDepreciationAccount)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(FixedAsset), id);

        return Map(asset);
    }

    public async Task<FixedAssetDto> CreateAsync(CreateFixedAssetDto dto, CancellationToken ct = default)
    {
        var codeExists = await _context.FixedAssets.AnyAsync(a => a.Code == dto.Code && !a.IsDeleted, ct);
        if (codeExists)
            throw new ValidationAppException($"كود الأصل '{dto.Code}' مستخدم بالفعل.");

        if (dto.AcquisitionCost <= 0)
            throw new ValidationAppException("تكلفة الأصل لازم تكون أكبر من صفر.");

        if (dto.UsefulLifeYears <= 0)
            throw new ValidationAppException("العمر الإنتاجي لازم يكون سنة واحدة على الأقل.");

        if (dto.SalvageValue < 0 || dto.SalvageValue >= dto.AcquisitionCost)
            throw new ValidationAppException("قيمة الخردة لازم تكون أقل من تكلفة الأصل.");

        if (dto.DepreciationMethod == DepreciationMethod.DecliningBalance
            && dto.DecliningBalanceRate is not (> 0 and <= 100))
            throw new ValidationAppException("نسبة القسط المتناقص لازم تكون بين 0% و100%.");

        var assetAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == dto.AssetAccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), dto.AssetAccountId);
        if (assetAccount.AccountType != AccountType.Asset || assetAccount.Nature != AccountNature.Debit)
            throw new ValidationAppException("حساب الأصل لازم يكون من نوع أصول ومدين.");

        var depreciationAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == dto.AccumulatedDepreciationAccountId && !a.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Account), dto.AccumulatedDepreciationAccountId);
        if (depreciationAccount.AccountType != AccountType.Asset || depreciationAccount.Nature != AccountNature.Credit)
            throw new ValidationAppException("حساب مجمع الإهلاك لازم يكون من نوع أصول ودائن.");

        if (depreciationAccount.Id == assetAccount.Id)
            throw new ValidationAppException("حساب مجمع الإهلاك لازم يكون مختلف عن حساب الأصل.");

        var asset = new FixedAsset
        {
            Code = dto.Code.Trim(),
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            AssetAccountId = dto.AssetAccountId,
            AccumulatedDepreciationAccountId = dto.AccumulatedDepreciationAccountId,
            AcquisitionCost = dto.AcquisitionCost,
            AcquisitionDate = dto.AcquisitionDate,
            UsefulLifeYears = dto.UsefulLifeYears,
            SalvageValue = dto.SalvageValue,
            DepreciationMethod = dto.DepreciationMethod,
            DecliningBalanceRate = dto.DepreciationMethod == DepreciationMethod.DecliningBalance ? dto.DecliningBalanceRate : null,
            AccumulatedDepreciation = 0,
            Status = FixedAssetStatus.Active
        };

        _context.FixedAssets.Add(asset);
        await _context.SaveChangesAsync(ct);

        return await GetByIdAsync(asset.Id, ct);
    }

    private static FixedAssetDto Map(FixedAsset a) => new()
    {
        Id = a.Id,
        Code = a.Code,
        NameAr = a.NameAr,
        NameEn = a.NameEn,
        AssetAccountId = a.AssetAccountId,
        AssetAccountCode = a.AssetAccount?.Code ?? string.Empty,
        AssetAccountName = a.AssetAccount?.NameAr ?? string.Empty,
        AccumulatedDepreciationAccountId = a.AccumulatedDepreciationAccountId,
        AccumulatedDepreciationAccountCode = a.AccumulatedDepreciationAccount?.Code ?? string.Empty,
        AccumulatedDepreciationAccountName = a.AccumulatedDepreciationAccount?.NameAr ?? string.Empty,
        AcquisitionCost = a.AcquisitionCost,
        AcquisitionDate = a.AcquisitionDate,
        UsefulLifeYears = a.UsefulLifeYears,
        SalvageValue = a.SalvageValue,
        DepreciationMethod = a.DepreciationMethod,
        DecliningBalanceRate = a.DecliningBalanceRate,
        AccumulatedDepreciation = a.AccumulatedDepreciation,
        BookValue = a.BookValue,
        Status = a.Status
    };
}
