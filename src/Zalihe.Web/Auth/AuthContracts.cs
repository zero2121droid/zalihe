using System.ComponentModel.DataAnnotations;
using Zalihe.Domain.Tenants;
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

public record CurrentUserResponse(Guid Id, string Name, string Email, string Language, Guid TenantId, string TenantName);
