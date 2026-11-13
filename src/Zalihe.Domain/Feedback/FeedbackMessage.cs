using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Feedback;

/// <summary>What a user writes about the app (SPEC 11.10).</summary>
public enum FeedbackKind
{
    Suggestion,
    Problem,

    /// <summary>Something unneeded or confusing.</summary>
    Confusing,

    /// <summary>An answer to a question the app asked at the right moment (<see cref="FeedbackPrompt"/>).</summary>
    Prompt,
}

/// <summary>The questions the app asks once per user, right after the moment they are about.</summary>
public enum FeedbackPrompt
{
    /// <summary>"Was the import clear?" after a CSV import.</summary>
    CsvImport,
}

/// <summary>Where the platform's owner is with a message.</summary>
public enum FeedbackStatus
{
    New,
    Read,
    Done,
}

/// <summary>
/// A message from a user to the platform's owner: a suggestion, a problem, something unneeded, or the
/// answer to a question asked in the app. It belongs to the user's company, but only the platform's
/// owner reads it. It keeps the page and the browser it was written from, never the company's data.
/// </summary>
public class FeedbackMessage : ITenantOwned
{
    public const int MessageMaxLength = 4000;
    public const int PageMaxLength = 300;
    public const int ClientMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public FeedbackKind Kind { get; private set; }

    /// <summary>The question answered; only for <see cref="FeedbackKind.Prompt"/>.</summary>
    public FeedbackPrompt? Prompt { get; private set; }

    /// <summary>"Yes" (true) or "not really" (false) to <see cref="Prompt"/>; null for written feedback.</summary>
    public bool? PromptAnswer { get; private set; }

    /// <summary>What the user wrote; empty only for an answer to a prompt.</summary>
    public string Message { get; private set; } = "";

    /// <summary>The app's path the user was on, e.g. "/items".</summary>
    public string Page { get; private set; } = "";

    /// <summary>The browser's User-Agent, so a problem can be tried in the same browser.</summary>
    public string Client { get; private set; } = "";

    /// <summary>The user agreed to be contacted by email about it.</summary>
    public bool MayContact { get; private set; }

    public FeedbackStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Something the owner should read: everything except a plain "yes" to a prompt.</summary>
    public bool NeedsReading => Status == FeedbackStatus.New;

    /// <summary>A suggestion, a problem or something confusing, written by the user.</summary>
    public static FeedbackMessage Write(Guid tenantId, Guid userId, FeedbackKind kind, string message, string? page,
        string? client, bool mayContact, DateTimeOffset now)
    {
        if (kind == FeedbackKind.Prompt) throw new ArgumentException("A prompt is answered with Answer.", nameof(kind));
        var text = message.Trim();
        if (text.Length == 0) throw new ArgumentException("A message is required.", nameof(message));

        return new FeedbackMessage(tenantId, userId, kind, text, page, client, now)
        {
            MayContact = mayContact,
            Status = FeedbackStatus.New,
        };
    }

    /// <summary>
    /// The answer to a question asked in the app. A plain "yes" is only counted, so it starts as read;
    /// "not really", or anything written, waits for the owner.
    /// </summary>
    public static FeedbackMessage Answer(Guid tenantId, Guid userId, FeedbackPrompt prompt, bool positive, string? message,
        string? page, string? client, DateTimeOffset now)
    {
        var text = message?.Trim() ?? "";
        return new FeedbackMessage(tenantId, userId, FeedbackKind.Prompt, text, page, client, now)
        {
            Prompt = prompt,
            PromptAnswer = positive,
            Status = positive && text.Length == 0 ? FeedbackStatus.Read : FeedbackStatus.New,
        };
    }

    public void SetStatus(FeedbackStatus status) => Status = status;

    private FeedbackMessage(Guid tenantId, Guid userId, FeedbackKind kind, string message, string? page, string? client,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (message.Length > MessageMaxLength) throw new ArgumentException("The message is too long.", nameof(message));

        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        Kind = kind;
        Message = message;
        // Sent by the browser, not typed: cut instead of refused, so a long address never loses the message.
        Page = Cut(page, PageMaxLength);
        Client = Cut(client, ClientMaxLength);
        CreatedAt = now;
    }

    // For EF Core.
    private FeedbackMessage()
    {
    }

    private static string Cut(string? value, int max)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
