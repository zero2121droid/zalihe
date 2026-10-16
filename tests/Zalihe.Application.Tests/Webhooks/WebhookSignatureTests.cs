using System.Text;
using Shouldly;
using Zalihe.Application.Webhooks;

namespace Zalihe.Application.Tests.Webhooks;

public class WebhookSignatureTests
{
    private const string Secret = "tajna-za-webhook";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"id":1041,"status":"processing"}""");

    // base64(HMAC-SHA256(body, secret)), computed independently with:
    // printf '%s' '{"id":1041,"status":"processing"}' | openssl dgst -sha256 -hmac 'tajna-za-webhook' -binary | base64
    private const string Signature = "C6+A6wkhNJ0Ybn+u9LM38l6nQsR1CwuH+7Dwk2dFRB8=";

    [Fact]
    public void IsSignedBy_WooCommerceSignature_IsAccepted()
    {
        WebhookService.IsSignedBy(Secret, Body, Signature).ShouldBeTrue();
    }

    [Fact]
    public void IsSignedBy_BodyChanged_IsRejected()
    {
        var changed = Encoding.UTF8.GetBytes("""{"id":1041,"status":"cancelled"}""");

        WebhookService.IsSignedBy(Secret, changed, Signature).ShouldBeFalse();
    }

    [Fact]
    public void IsSignedBy_OtherSecret_IsRejected()
    {
        WebhookService.IsSignedBy("druga-tajna", Body, Signature).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nije base64!")]
    [InlineData("c2lnbmF0dXJl")]
    public void IsSignedBy_MissingOrMalformedSignature_IsRejected(string? signature)
    {
        WebhookService.IsSignedBy(Secret, Body, signature).ShouldBeFalse();
    }
}
