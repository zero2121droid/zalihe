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

public record CurrentUserResponse(Guid Id, string Name, string Email, string Language, Guid TenantId, string TenantName);

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
    string Email);

public record AcceptInvitationRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(User.NameMaxLength, ErrorMessage = "validation.max_length")]
    string Name,
    [Required(ErrorMessage = "validation.required")]
    string Password,
    string? Language);
