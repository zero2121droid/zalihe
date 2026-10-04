namespace Zalihe.Application.Common;

/// <summary>
/// Encrypts secrets (shop API keys, webhook secrets) before they are stored, and decrypts them
/// for use. Implemented with ASP.NET Data Protection.
/// </summary>
public interface ICredentialProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
