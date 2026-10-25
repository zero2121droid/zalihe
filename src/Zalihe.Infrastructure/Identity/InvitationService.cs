using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Email;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Identity;

public record CompanyUserDto(Guid Id, string Name, string Email, UserRole Role);

/// <param name="State">Pending or expired; accepted and revoked invitations aren't listed.</param>
/// <param name="Role">The role the invited person gets on accepting.</param>
public record InvitationDto(Guid Id, string Email, DateTimeOffset SentAt, DateTimeOffset ExpiresAt, InvitationState State, UserRole Role);

/// <summary>What the invitation page shows before the invited person signs up.</summary>
public record InvitationInfoDto(string CompanyName, string Email, string InvitedBy);

public record InviteResult(InvitationDto? Invitation, AppError? Error)
{
    public bool Succeeded => Invitation is not null;
}

public record ChangeRoleResult(CompanyUserDto? User, AppError? Error, bool NotFound = false);

/// <summary>
/// The company's users, their roles and the invitations to join it (SPEC 2.7, 11.2); only admins get
/// here (the controller's policy). Accepting is in <see cref="AccountService"/>, next to registration.
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
            .Select(u => new CompanyUserDto(u.Id, u.Name, u.Email!, u.Role))
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
    /// Invites <paramref name="email"/> to the current company with <paramref name="role"/> and emails the
    /// link. Inviting the same address again sends a new link (with the new role) and the old one stops
    /// working. The email goes out before anything is saved, so a mail server that is down leaves no
    /// invitation nobody received.
    /// </summary>
    public async Task<InviteResult> InviteAsync(User inviter, string email, UserRole role, CancellationToken ct)
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
            invitation = new Invitation(tenant.TenantId, address, token, role, inviter.Id, now);
            db.Invitations.Add(invitation);
        }
        else
        {
            invitation.Renew(token, role, inviter.Id, now);
        }

        await emails.SendInvitationAsync(address, inviter.Name, inviter.Language, LinkPath(token), invitation.ExpiresAt, ct);
        await db.SaveChangesAsync(ct);
        return new InviteResult(ToDto(invitation, now), null);
    }

    public const string LastAdmin = "user.last_admin";

    /// <summary>
    /// Gives a user of the current company another role. The company always keeps an admin, so the last
    /// one can't become anything else; role changes in one company run one after another, so two admins
    /// can't take each other's role at the same moment.
    /// </summary>
    public async Task<ChangeRoleResult> ChangeRoleAsync(Guid userId, UserRole role, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "Tenants" WHERE "Id" = {tenant.TenantId} FOR UPDATE""", ct);

        // Users have no global query filter, so they are limited to the current company here.
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.TenantId == tenant.TenantId, ct);
        if (user is null)
        {
            return new ChangeRoleResult(null, null, NotFound: true);
        }

        if (user.Role == UserRole.Admin && role != UserRole.Admin
            && await db.Users.CountAsync(u => u.TenantId == tenant.TenantId && u.Role == UserRole.Admin, ct) == 1)
        {
            return new ChangeRoleResult(null, new AppError(LastAdmin, "role"));
        }

        user.Role = role;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ChangeRoleResult(new CompanyUserDto(user.Id, user.Name, user.Email!, user.Role), null);
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
        new(invitation.Id, invitation.Email, invitation.SentAt, invitation.ExpiresAt, invitation.StateAt(now), invitation.Role);
}
