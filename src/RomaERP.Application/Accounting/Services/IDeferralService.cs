using RomaERP.Application.Accounting.DTOs;

namespace RomaERP.Application.Accounting.Services;

public interface IDeferralService
{
    Task<List<DeferralDto>> GetAllAsync(CancellationToken ct = default);
    Task<DeferralDto> CreateAsync(CreateDeferralDto dto, CancellationToken ct = default);
    /// <summary>Releases every month that has come due (up to and including <paramref name="upToMonth"/>, default this month). Safe to run repeatedly.</summary>
    Task<DeferralRunResultDto> RunAsync(DateTime? upToMonth = null, CancellationToken ct = default);
    /// <summary>Stops future releases. What is still on the balance sheet stays there and has to be dealt with by a manual entry.</summary>
    Task<DeferralDto> CancelAsync(Guid id, CancellationToken ct = default);
}
