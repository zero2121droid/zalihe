using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zalihe.Application.Common;
using Zalihe.Domain.Tenants;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Identity;

public record RegisterCommand(string CompanyName, string Email, string Password, string? Language);

public record RegistrationResult(User? User, IReadOnlyList<AppError> Errors)
{
    public bool Succeeded => User is not null;
}

public partial class AccountService(
    AppDbContext db,
    UserManager<User> userManager,
    IOptions<IdentityOptions> identityOptions,
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
        var user = new User { UserName = email, Email = email, TenantId = tenant.Id, Language = language };
        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            return Failed([.. result.Errors.Select(MapIdentityError).Distinct()]);
        }

        await transaction.CommitAsync(ct);
        return new RegistrationResult(user, []);
    }

    public Task<Tenant> GetTenantAsync(Guid tenantId, CancellationToken ct) =>
        db.Tenants.SingleAsync(t => t.Id == tenantId, ct);

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
