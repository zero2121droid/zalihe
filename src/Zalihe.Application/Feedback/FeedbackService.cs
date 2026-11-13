using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Feedback;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Feedback;

/// <param name="Page">The app's path the user was on, e.g. "/items".</param>
/// <param name="Client">The browser's User-Agent.</param>
/// <param name="Screenshot">A picture of the screen the user added, already read; null without one.</param>
public record SendFeedbackCommand(FeedbackKind Kind, string Message, string? Page, string? Client, bool MayContact,
    byte[]? Screenshot = null);

/// <param name="Positive">"Yes" (true) or "not really" (false).</param>
public record AnswerPromptCommand(FeedbackPrompt Prompt, bool Positive, string? Message, string? Page, string? Client);

/// <summary>
/// Messages from users to the platform's owner (SPEC 11.10): written from any screen, or the answer to a
/// question asked once per user. Each one that needs reading is emailed to the owner in the background.
/// </summary>
public class FeedbackService(
    IAppDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IBackgroundJobs jobs,
    TimeProvider timeProvider)
{
    /// <summary>A user's messages per hour: plenty for a person, and a stop for a script filling the owner's inbox.</summary>
    public const int PerHourLimit = 20;

    public const string TooManyCode = "feedback.too_many";
    public const string ScreenshotTooLarge = "feedback.screenshot_too_large";
    public const string ScreenshotUnsupported = "feedback.screenshot_unsupported";
    public const string ScreenshotField = "screenshot";

    /// <summary>A picture larger than this is refused before it is read.</summary>
    public static AppError? CheckScreenshotLength(long length) => length > FeedbackScreenshot.MaxBytes
        ? new AppError(ScreenshotTooLarge, ScreenshotField, new Dictionary<string, object> { ["maxMb"] = FeedbackScreenshot.MaxBytes / (1024 * 1024) })
        : null;

    /// <returns>
    /// Null when saved; otherwise <see cref="TooManyCode"/>, or an error about the screenshot (with
    /// <see cref="AppError.Field"/> "screenshot"), in which case nothing is saved.
    /// </returns>
    public async Task<AppError?> SendAsync(SendFeedbackCommand command, CancellationToken ct)
    {
        if (command.Screenshot is { } data)
        {
            if (CheckScreenshotLength(data.Length) is { } tooLarge) return tooLarge;
            if (ItemImage.DetectContentType(data) is null) return new AppError(ScreenshotUnsupported, ScreenshotField);
        }

        var userId = UserId();
        var now = timeProvider.GetUtcNow();
        if (await OverLimitAsync(userId, now, ct)) return new AppError(TooManyCode);

        var feedback = FeedbackMessage.Write(tenant.TenantId, userId, command.Kind, command.Message, command.Page,
            command.Client, command.MayContact, now);
        if (command.Screenshot is { } screenshot) db.FeedbackScreenshots.Add(new FeedbackScreenshot(feedback, screenshot));
        await SaveAsync(feedback, ct);
        return null;
    }

    /// <summary>
    /// Saves the answer to a prompt. A prompt is answered once per user: a second answer (another tab, a
    /// double click) is ignored, so the counts stay one per person.
    /// </summary>
    /// <returns>Null when saved or already answered; otherwise <see cref="TooManyCode"/>.</returns>
    public async Task<AppError?> AnswerAsync(AnswerPromptCommand command, CancellationToken ct)
    {
        var userId = UserId();
        var now = timeProvider.GetUtcNow();
        if (await db.FeedbackMessages.AnyAsync(f => f.UserId == userId && f.Prompt == command.Prompt, ct)) return null;
        if (await OverLimitAsync(userId, now, ct)) return new AppError(TooManyCode);

        var answer = FeedbackMessage.Answer(tenant.TenantId, userId, command.Prompt, command.Positive, command.Message,
            command.Page, command.Client, now);
        await SaveAsync(answer, ct);
        return null;
    }

    /// <summary>The prompts the signed-in user has answered, so the app doesn't ask them again.</summary>
    public async Task<IReadOnlyList<FeedbackPrompt>> AnsweredPromptsAsync(CancellationToken ct)
    {
        var userId = UserId();
        return await db.FeedbackMessages
            .Where(f => f.UserId == userId && f.Prompt != null)
            .Select(f => f.Prompt!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task SaveAsync(FeedbackMessage feedback, CancellationToken ct)
    {
        db.FeedbackMessages.Add(feedback);
        await db.SaveChangesAsync(ct);
        if (feedback.NeedsReading) jobs.EmailFeedback(feedback.TenantId, feedback.Id);
    }

    private async Task<bool> OverLimitAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddHours(-1);
        return await db.FeedbackMessages.CountAsync(f => f.UserId == userId && f.CreatedAt >= since, ct) >= PerHourLimit;
    }

    private Guid UserId() =>
        currentUser.UserId ?? throw new InvalidOperationException("Feedback needs a signed-in user.");
}
