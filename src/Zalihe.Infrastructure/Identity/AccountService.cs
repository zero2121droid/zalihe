using System.Buffers.Text;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Zalihe.Application.Common;
using Zalihe.Domain.Tenants;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Email;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Identity;

public record RegisterCommand(string CompanyName, string Name, string Email, string Password, string? Language);

public record RegistrationResult(User? User, IReadOnlyList<AppError> Errors)
{
    public bool Succeeded => User is not null;
}

public partial class AccountService(
    AppDbContext db,
    UserManager<User> userManager,
    IOptions<IdentityOptions> identityOptions,
    IOptions<DataProtectionTokenProviderOptions> tokenOptions,
    AlertEmails emails,
    IMemoryCache cache,
    TimeProvider timeProvider)
{
    /// <summary>Creates a company together with its owner account.</summary>
    public async Task<RegistrationResult> RegisterAsync(RegisterCommand command, CancellationToken ct)
    {
        var language = command.Language ?? Languages.Default;
        if (!Languages.IsSupported(language))
        {
            return Failed(new AppError("validation.unsupported_language", "language"));
        }

        // UserManager uses the same DbContext, so the tenant and the user are saved atomically.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var tenant = new Tenant(command.CompanyName, timeProvider.GetUtcNow());
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        var email = command.Email.Trim();
        var user = new User
        {
            UserName = email,
            Email = email,
            Name = command.Name.Trim(),
            TenantId = tenant.Id,
            Language = language,
        };
        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            return Failed([.. result.Errors.Select(MapIdentityError).Distinct()]);
        }

        await transaction.CommitAsync(ct);
        return new RegistrationResult(user, []);
    }

    public const string InvitationInvalid = "invitation.invalid";

    /// <summary>
    /// Creates the invited person's account in the inviting company, with the invitation's email, and
    /// uses up the invitation. Found by token across companies, like <see cref="InvitationService.GetByTokenAsync"/>.
    /// </summary>
    public async Task<RegistrationResult> AcceptInvitationAsync(string token, string name, string password, string? language,
        CancellationToken ct)
    {
        language ??= Languages.Default;
        if (!Languages.IsSupported(language))
        {
            return Failed(new AppError("validation.unsupported_language", "language"));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var hash = Invitation.HashToken(token);
        var invitation = await db.Invitations.IgnoreQueryFilters().SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        var now = timeProvider.GetUtcNow();
        if (invitation is null || invitation.StateAt(now) != InvitationState.Pending)
        {
            return Failed(new AppError(InvitationInvalid));
        }

        var user = new User
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            Name = name.Trim(),
            TenantId = invitation.TenantId,
            Language = language,
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return Failed([.. result.Errors.Select(MapIdentityError).Distinct()]);
        }

        invitation.Accept(now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new RegistrationResult(user, []);
    }

    public const string ResetInvalid = "auth.reset_invalid";

    /// <summary>How often one account can get a reset email, so the form can't flood someone's inbox.</summary>
    public static readonly TimeSpan PasswordResetEmailInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Emails a link for setting a new password, in the user's language. An unknown address gets nothing,
    /// and the caller answers the same either way, so the form doesn't tell which addresses have an account.
    /// </summary>
    public async Task RequestPasswordResetAsync(string email, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user?.Email is null)
        {
            return;
        }

        var throttleKey = $"password-reset:{user.Id}";
        if (cache.TryGetValue(throttleKey, out _))
        {
            return;
        }

        cache.Set(throttleKey, true, PasswordResetEmailInterval);

        // Identity's token holds characters that don't belong in an address (+, /, =).
        var token = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(await userManager.GeneratePasswordResetTokenAsync(user)));
        var link = $"/reset-password?email={Uri.EscapeDataString(user.Email)}&token={token}";
        var tenant = await GetTenantAsync(user.TenantId, ct);
        var expiresAt = timeProvider.GetUtcNow() + tokenOptions.Value.TokenLifespan;
        await emails.SendPasswordResetAsync(user.Email, user.Language, tenant.Name, link, expiresAt, ct);
    }

    /// <summary>
    /// Sets a new password with the token from the emailed link and unlocks the account. Identity then
    /// changes the security stamp, so the link can't be used again and other devices are signed out.
    /// </summary>
    public async Task<RegistrationResult> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !TryDecodeToken(token, out var decoded))
        {
            return Failed(new AppError(ResetInvalid));
        }

        var result = await userManager.ResetPasswordAsync(user, decoded, newPassword);
        if (!result.Succeeded)
        {
            return Failed([.. result.Errors.Select(MapPasswordResetError).Distinct()]);
        }

        // Five wrong passwords lock the account; whoever proved they own the address may come in.
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        return new RegistrationResult(user, []);
    }

    private static bool TryDecodeToken(string token, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
            return true;
        }
        catch (FormatException)
        {
            decoded = "";
            return false;
        }
    }

    private AppError MapPasswordResetError(IdentityError error)
    {
        if (error.Code == nameof(IdentityErrorDescriber.InvalidToken))
        {
            return new AppError(ResetInvalid);
        }

        var mapped = MapIdentityError(error);
        return mapped.Field == "password" ? mapped with { Field = "newPassword" } : mapped;
    }

    public Task<Tenant> GetTenantAsync(Guid tenantId, CancellationToken ct) =>
        db.Tenants.SingleAsync(t => t.Id == tenantId, ct);

    /// <summary>Renames the user's company. Request validation already checked the name.</summary>
    public async Task RenameCompanyAsync(User user, string name, CancellationToken ct)
    {
        var tenant = await GetTenantAsync(user.TenantId, ct);
        tenant.Rename(name);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Changes the user's own name and UI language.</summary>
    public async Task<IReadOnlyList<AppError>> UpdateProfileAsync(User user, string name, string language)
    {
        if (!Languages.IsSupported(language))
        {
            return [new AppError("validation.unsupported_language", "language")];
        }

        user.Name = name.Trim();
        user.Language = language;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? [] : [.. result.Errors.Select(MapIdentityError).Distinct()];
    }

    /// <summary>
    /// Changes the user's password after checking the current one. Identity then changes the user's
    /// security stamp, which signs the user out on other devices once their cookie is checked again.
    /// </summary>
    public async Task<IReadOnlyList<AppError>> ChangePasswordAsync(User user, string currentPassword, string newPassword)
    {
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded ? [] : [.. result.Errors.Select(MapPasswordChangeError).Distinct()];
    }

    /// <summary>A wrong current password belongs to that field; the rules for a new one, to the new one.</summary>
    private AppError MapPasswordChangeError(IdentityError error)
    {
        if (error.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
        {
            return new AppError("auth.wrong_password", "currentPassword");
        }

        var mapped = MapIdentityError(error);
        return mapped.Field == "password" ? mapped with { Field = "newPassword" } : mapped;
    }

    private static RegistrationResult Failed(params AppError[] errors) => new(null, errors);

    private AppError MapIdentityError(IdentityError error) => error.Code switch
    {
        // The email is also the user name, so both duplicate errors mean the same thing.
        nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)
            => new AppError("auth.email_taken", "email"),
        nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName)
            => new AppError("validation.email", "email"),
        nameof(IdentityErrorDescriber.PasswordTooShort)
            => new AppError("auth.password_too_short", "password",
                new Dictionary<string, object> { ["min"] = identityOptions.Value.Password.RequiredLength }),
        _ when error.Code.StartsWith("Password", StringComparison.Ordinal)
            => new AppError($"auth.{ToSnakeCase(error.Code)}", "password"),
        _ => new AppError($"auth.{ToSnakeCase(error.Code)}"),
    };

    private static string ToSnakeCase(string value) => PascalCaseBoundary().Replace(value, "_$1").ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
