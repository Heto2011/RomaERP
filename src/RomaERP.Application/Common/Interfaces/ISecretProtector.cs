namespace RomaERP.Application.Common.Interfaces;

/// <summary>Encrypts/decrypts sensitive tenant-supplied secrets (e-invoicing client secrets, signing keys)
/// before they're persisted to the database. Never store these fields as plain text.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedText);
}
