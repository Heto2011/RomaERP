using Microsoft.AspNetCore.DataProtection;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.Security;

public class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("RomaERP.EInvoicing.Secrets.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
