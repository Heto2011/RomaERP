using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IAccountService
{
    Task<List<AccountDto>> GetTreeAsync(CancellationToken ct = default);
    Task<List<AccountDto>> GetFlatListAsync(CancellationToken ct = default);
    Task<AccountDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AccountDto> CreateAsync(CreateAccountDto dto, CancellationToken ct = default);
    Task<AccountDto> UpdateAsync(Guid id, UpdateAccountDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
