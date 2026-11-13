using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Feedback;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Platform;

/// <param name="New">Messages the owner hasn't read yet.</param>
/// <param name="CsvImportAnswers">Answers to "Was the import clear?".</param>
/// <param name="CsvImportClear">Of those, the "yes" answers.</param>
public record PlatformFeedbackSummaryDto(int New, int Suggestions, int Problems, int Done, int CsvImportAnswers, int CsvImportClear);

/// <param name="ContactEmail">The user's email, only when they agreed to be contacted.</param>
/// <param name="Client">The browser's User-Agent.</param>
/// <param name="HasScreenshot">A picture of the screen is attached; it is gone once the message is done.</param>
public record PlatformFeedbackDto(
    Guid Id,
    FeedbackKind Kind,
    FeedbackPrompt? Prompt,
    bool? PromptAnswer,
    string Message,
    string Page,
    string Client,
    FeedbackStatus Status,
    DateTimeOffset CreatedAt,
    string Company,
    string UserName,
    string? ContactEmail,
    bool HasScreenshot);

public record FeedbackScreenshotFile(string ContentType, byte[] Data);

/// <param name="Status">Only messages with this status; all when null.</param>
public record PlatformFeedbackListQuery(FeedbackStatus? Status = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>
/// The messages from all companies' users, for the platform's owner (SPEC 11.10). Reads across companies
/// like <see cref="PlatformStatsService"/>, so the tenant filter is turned off here and nowhere else.
/// </summary>
public class PlatformFeedbackService(AppDbContext db)
{
    private IQueryable<FeedbackMessage> All => db.FeedbackMessages.IgnoreQueryFilters();

    public async Task<PlatformFeedbackSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var all = All;
        var written = all.Where(f => f.Kind != FeedbackKind.Prompt);
        var csvImport = all.Where(f => f.Prompt == FeedbackPrompt.CsvImport);
        return new PlatformFeedbackSummaryDto(
            await all.CountAsync(f => f.Status == FeedbackStatus.New, ct),
            await written.CountAsync(f => f.Kind == FeedbackKind.Suggestion, ct),
            await written.CountAsync(f => f.Kind == FeedbackKind.Problem, ct),
            await all.CountAsync(f => f.Status == FeedbackStatus.Done, ct),
            await csvImport.CountAsync(ct),
            await csvImport.CountAsync(f => f.PromptAnswer == true, ct));
    }

    /// <summary>Newest first. A plain "yes" to a prompt is only counted in the summary, never listed.</summary>
    public async Task<PagedResult<PlatformFeedbackDto>> ListAsync(PlatformFeedbackListQuery query, CancellationToken ct)
    {
        var feedback = All.AsNoTracking().Where(f => !(f.Kind == FeedbackKind.Prompt && f.PromptAnswer == true && f.Message == ""));
        if (query.Status is { } status) feedback = feedback.Where(f => f.Status == status);

        var totalCount = await feedback.CountAsync(ct);
        var page = await (
                from f in feedback
                join t in db.Tenants on f.TenantId equals t.Id
                join u in db.Users on f.UserId equals u.Id
                orderby f.CreatedAt descending, f.Id
                select new PlatformFeedbackDto(
                    f.Id, f.Kind, f.Prompt, f.PromptAnswer, f.Message, f.Page, f.Client, f.Status, f.CreatedAt,
                    t.Name, u.Name, f.MayContact ? u.Email : null,
                    db.FeedbackScreenshots.IgnoreQueryFilters().Any(x => x.FeedbackId == f.Id)))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<PlatformFeedbackDto>(page, totalCount, query.Page, query.PageSize);
    }

    /// <summary>
    /// Changes a message's status. Done also deletes its screenshot: it may show the company's data, and a
    /// finished message no longer needs it (SPEC 11.10).
    /// </summary>
    /// <returns>False when there is no such message.</returns>
    public async Task<bool> SetStatusAsync(Guid id, FeedbackStatus status, CancellationToken ct)
    {
        var feedback = await All.SingleOrDefaultAsync(f => f.Id == id, ct);
        if (feedback is null) return false;

        feedback.SetStatus(status);
        await db.SaveChangesAsync(ct);
        if (status == FeedbackStatus.Done)
        {
            await db.FeedbackScreenshots.IgnoreQueryFilters().Where(x => x.FeedbackId == id).ExecuteDeleteAsync(ct);
        }

        return true;
    }

    /// <summary>The picture attached to a message; null when there is none.</summary>
    public Task<FeedbackScreenshotFile?> GetScreenshotAsync(Guid id, CancellationToken ct) =>
        db.FeedbackScreenshots.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.FeedbackId == id)
            .Select(x => new FeedbackScreenshotFile(x.ContentType, x.Data))
            .SingleOrDefaultAsync(ct);
}
