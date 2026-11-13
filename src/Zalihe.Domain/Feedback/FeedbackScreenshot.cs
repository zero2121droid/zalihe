using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Feedback;

/// <summary>
/// A picture of the screen the user added to a message (SPEC 11.10), shrunk by the browser. Kept apart
/// from the message so lists never load it, and deleted once the owner marks the message done, since it
/// may show the company's data.
/// </summary>
public class FeedbackScreenshot : ITenantOwned
{
    /// <summary>Same limit as an item's picture: far above what the web app sends after shrinking.</summary>
    public const int MaxBytes = ItemImage.MaxBytes;

    public Guid FeedbackId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>"image/jpeg", "image/png" or "image/webp", from the file's content.</summary>
    public string ContentType { get; private set; }

    public byte[] Data { get; private set; }

    /// <exception cref="ArgumentException">The data is not a JPEG, PNG or WebP picture, or is too large.</exception>
    public FeedbackScreenshot(FeedbackMessage feedback, byte[] data)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, MaxBytes, nameof(data));
        ContentType = ItemImage.DetectContentType(data) ?? throw new ArgumentException("Not a JPEG, PNG or WebP picture.", nameof(data));
        FeedbackId = feedback.Id;
        TenantId = feedback.TenantId;
        Data = data;
    }

    // For EF Core.
    private FeedbackScreenshot()
    {
        ContentType = null!;
        Data = null!;
    }
}
