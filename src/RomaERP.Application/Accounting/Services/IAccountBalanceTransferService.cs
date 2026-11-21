using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IAccountBalanceTransferService
{
    Task<BalanceTransferPreviewDto> PreviewAsync(BalanceTransferRequestDto request, CancellationToken ct = default);
    Task<BalanceTransferResultDto> ApplyAsync(BalanceTransferRequestDto request, CancellationToken ct = default);
}
