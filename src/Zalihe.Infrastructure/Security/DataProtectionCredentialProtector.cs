using Microsoft.AspNetCore.DataProtection;
using Zalihe.Application.Common;

namespace Zalihe.Infrastructure.Security;

public class DataProtectionCredentialProtector(IDataProtectionProvider provider) : ICredentialProtector
{
    // The purpose isolates these secrets from other uses of Data Protection (e.g. sign-in cookies).
    private readonly IDataProtector _protector = provider.CreateProtector("Zalihe.SalesChannels.Secrets.v1");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
