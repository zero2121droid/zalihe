using System.ComponentModel.DataAnnotations;
using Zalihe.Domain.Tenants;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Identity;

namespace Zalihe.Web.Auth;

public record RegisterRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(Tenant.NameMaxLength, ErrorMessage = "validation.max_length")]
    string CompanyName,
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(User.NameMaxLength, ErrorMessage = "validation.max_length")]
    string Name,
    [Required(ErrorMessage = "validation.required")]
    [EmailAddress(ErrorMessage = "validation.email")]
    string Email,
    [Required(ErrorMessage = "validation.required")]
    string Password,
    string? Language);

public record LoginRequest(
    [Required(ErrorMessage = "validation.required")]
    string Email,
    [Required(ErrorMessage = "validation.required")]
    string Password,
    bool RememberMe = false);

public record ForgotPasswordRequest(
    [Required(ErrorMessage = "validation.required")]
    [EmailAddress(ErrorMessage = "validation.email")]
    string Email);

public record ResetPasswordRequest(
    [Required(ErrorMessage = "validation.required")]
    string Email,
    [Required(ErrorMessage = "validation.required")]
    string Token,
    [Required(ErrorMessage = "validation.required")]
    string NewPassword);

/// <param name="Role">What the user may do; the client shows only what the role allows, the API enforces it.</param>
/// <param name="CanSeePurchasePrices">Whether the user sees purchase prices and stock value; the API leaves them out otherwise.</param>
/// <param name="EmployeesSeePurchasePrices">The company's setting, which admins change.</param>
public record CurrentUserResponse(
    Guid Id,
    string Name,
    string Email,
    string Language,
    Guid TenantId,
    string TenantName,
    UserRole Role,
    bool CanSeePurchasePrices,
    bool EmployeesSeePurchasePrices)
{
    public static CurrentUserResponse For(User user, Tenant tenant) =>
        new(user.Id, user.Name, user.Email!, user.Language, tenant.Id, tenant.Name, user.Role,
            user.Role.SeesPurchasePrices(tenant.EmployeesSeePurchasePrices), tenant.EmployeesSeePurchasePrices);
}

public record PurchasePriceSettingRequest(bool EmployeesSeePurchasePrices);

public record UpdateCompanyRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(Tenant.NameMaxLength, ErrorMessage = "validation.max_length")]
    string Name);

public record UpdateProfileRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(User.NameMaxLength, ErrorMessage = "validation.max_length")]
    string Name,
    [Required(ErrorMessage = "validation.required")]
    string Language);

public record ChangePasswordRequest(
    [Required(ErrorMessage = "validation.required")]
    string CurrentPassword,
    [Required(ErrorMessage = "validation.required")]
    string NewPassword);

public record InviteRequest(
    [Required(ErrorMessage = "validation.required")]
    [EmailAddress(ErrorMessage = "validation.email")]
    [MaxLength(Invitation.EmailMaxLength, ErrorMessage = "validation.max_length")]
    string Email,
    [Required(ErrorMessage = "validation.required")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "validation.invalid")]
    UserRole? Role);

public record ChangeRoleRequest(
    [Required(ErrorMessage = "validation.required")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "validation.invalid")]
    UserRole? Role);

public record AcceptInvitationRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(User.NameMaxLength, ErrorMessage = "validation.max_length")]
    string Name,
    [Required(ErrorMessage = "validation.required")]
    string Password,
    string? Language);
