namespace RomaERP.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateToken(Guid userId, string userName, string email, string companyCode, IEnumerable<string> roles);
}
