using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;

namespace Zalihe.Application.Alerts;

/// <summary>
/// Tells the company's users that their shop stopped working (SPEC 7.6), once per failure: the channel
/// remembers the email until the shop works again. Called by a background job with the company set.
/// </summary>
public class ChannelFailureAlertService(IAppDbContext db, IAlertEmails emails, TimeProvider timeProvider)
{
    /// <returns>True when the email went out; false when it isn't needed (any more).</returns>
    public async Task<bool> SendAsync(Guid channelId, CancellationToken ct)
    {
        // Gone, working again, or already emailed by an earlier run: nothing to say.
        var channel = await db.SalesChannels.SingleOrDefaultAsync(c => c.Id == channelId, ct);
        if (channel is not { NeedsErrorEmail: true, FailingSince: { } since }) return false;

        await emails.SendChannelFailureAsync(new ChannelFailureEmail(channel.BaseUrl, channel.LastErrorCode, since), ct);
        channel.MarkErrorEmailed(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return true;
    }
}
