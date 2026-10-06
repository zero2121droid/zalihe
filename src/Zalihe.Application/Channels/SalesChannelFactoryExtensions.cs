using System.Text.Json;
using Zalihe.Application.Common;
using Zalihe.Domain.Channels;

namespace Zalihe.Application.Channels;

public static class SalesChannelFactoryExtensions
{
    /// <summary>A client for a saved shop, with its credentials decrypted only for this call.</summary>
    public static ISalesChannel Create(this ISalesChannelFactory factory, SalesChannel channel, ICredentialProtector protector)
    {
        var credentials = JsonSerializer.Deserialize<WooCommerceCredentials>(protector.Unprotect(channel.EncryptedCredentials))!;
        return factory.Create(channel.Type, channel.BaseUrl, credentials);
    }
}
