using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Email;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Identity;

public record CompanyUserDto(Guid Id, string Name, string Email);

/// <param name="State">Pending or expired; accepted and revoked invitations aren't listed.</param>
public record InvitationDto(Guid Id, string Email, DateTimeOffset SentAt, DateTimeOffset ExpiresAt, InvitationState State);

/// <summary>What the invitation page shows before the invited person signs up.</summary>
public record InvitationInfoDto(string CompanyName, string Email, string InvitedBy);

public record InviteResult(InvitationDto? Invitation, AppError? Error)
{
    public bool Succeeded => Invitation is not null;
}

/// <summary>
/// The company's users and the invitations to join it (SPEC 2.7). In v1 every user may invite; with
/// roles that becomes the admin's (SPEC 11.2). Accepting is in <see cref="AccountService"/>, next to
/// registration.
/// </summary>
public class InvitationService(AppDbContext db, ITenantContext tenant, AlertEmails emails, TimeProvider timeProvider)
{
    public const string AlreadyHasAccount = "invitation.email_taken";

    /// <summary>The app path the email links to; the invitation page reads the token from it.</summary>
    public static string LinkPath(string token) => $"/invite/{token}";

    public async Task<PagedResult<CompanyUserDto>> ListUsersAsync(int page, int pageSize, CancellationToken ct)
    {
        // Users have no global query filter, so they are limited to the current company here.
        var users = db.Users.AsNoTracking().Where(u => u.TenantId == tenant.TenantId);
        var total = await users.CountAsync(ct);
        var items = await users
            .OrderBy(u => u.Name).ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new CompanyUserDto(u.Id, u.Name, u.Email!))
            .ToListAsync(ct);
        return new PagedResult<CompanyUserDto>(items, total, page, pageSize);
    }

    /// <summary>Invitations not accepted or revoked, newest first; expired ones too, so they can be sent again.</summary>
    public async Task<PagedResult<InvitationDto>> ListInvitationsAsync(int page, int pageSize, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var open = db.Invitations.AsNoTracking().Where(i => i.AcceptedAt == null && i.RevokedAt == null);
        var total = await open.CountAsync(ct);
        var items = await open
            .OrderByDescending(i => i.SentAt).ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<InvitationDto>(items.Select(i => ToDto(i, now)).ToList(), total, page, pageSize);
    }

    /// <summary>
    /// Invites <paramref name="email"/> to the current company and emails the link. Inviting the same
    /// address again sends a new link and the old one stops working. The email goes out before anything is
    /// saved, so a mail server that is down leaves no invitation nobody received.
    /// </summary>
    public async Task<InviteResult> InviteAsync(User inviter, string email, CancellationToken ct)
    {
        var address = Invitation.NormalizeEmail(email);
        // Email is the sign-in name, unique across all companies (one company per user in v1).
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == address.ToUpperInvariant(), ct))
        {
            return new InviteResult(null, new AppError(AlreadyHasAccount, "email"));
        }

        var now = timeProvider.GetUtcNow();
        var token = Invitation.NewToken();
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Email == address, ct);
        if (invitation is null)
        {
            invitation = new Invitation(tenant.TenantId, address, token, inviter.Id, now);
            db.Invitations.Add(invitation);
        }
        else
        {
            invitation.Renew(token, inviter.Id, now);
        }

        await emails.SendInvitationAsync(address, inviter.Name, inviter.Language, LinkPath(token), invitation.ExpiresAt, ct);
        await db.SaveChangesAsync(ct);
        return new InviteResult(ToDto(invitation, now), null);
    }

    /// <summary>Withdraws an invitation of the current company; false when there is none.</summary>
    public async Task<bool> RevokeAsync(Guid invitationId, CancellationToken ct)
    {
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == invitationId, ct);
        if (invitation is null) return false;

        invitation.Revoke(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// The invitation a link points to, for the page the invited person opens before having an account
    /// (so before any company is known); null when it doesn't exist or can no longer be accepted.
    /// Crosses companies by token, like <see cref="AccountService.AcceptInvitationAsync"/>; nothing else may.
    /// </summary>
    public async Task<InvitationInfoDto?> GetByTokenAsync(string token, CancellationToken ct)
    {
        var hash = Invitation.HashToken(token);
        var found = await (
                from invitation in db.Invitations.IgnoreQueryFilters().AsNoTracking()
                where invitation.TokenHash == hash
                join company in db.Tenants on invitation.TenantId equals company.Id
                join inviter in db.Users on invitation.InvitedByUserId equals inviter.Id
                select new { invitation, CompanyName = company.Name, InvitedBy = inviter.Name })
            .SingleOrDefaultAsync(ct);

        return found is not null && found.invitation.StateAt(timeProvider.GetUtcNow()) == InvitationState.Pending
            ? new InvitationInfoDto(found.CompanyName, found.invitation.Email, found.InvitedBy)
            : null;
    }

    private static InvitationDto ToDto(Invitation invitation, DateTimeOffset now) =>
        new(invitation.Id, invitation.Email, invitation.SentAt, invitation.ExpiresAt, invitation.StateAt(now));
}
