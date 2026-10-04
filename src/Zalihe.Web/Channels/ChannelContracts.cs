using System.ComponentModel.DataAnnotations;
using Zalihe.Domain.Channels;

namespace Zalihe.Web.Channels;

public record ConnectWooCommerceRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(SalesChannel.BaseUrlMaxLength, ErrorMessage = "validation.max_length")]
    string BaseUrl,
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(200, ErrorMessage = "validation.max_length")]
    string ConsumerKey,
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(200, ErrorMessage = "validation.max_length")]
    string ConsumerSecret);

public record ReplaceCredentialsRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(200, ErrorMessage = "validation.max_length")]
    string ConsumerKey,
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(200, ErrorMessage = "validation.max_length")]
    string ConsumerSecret);
