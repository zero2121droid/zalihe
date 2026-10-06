using System.ComponentModel.DataAnnotations;
using Zalihe.Application.Channels;
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

/// <param name="CreateExternalIds">Shop IDs of the new products to create as items; products with a matching SKU are linked anyway.</param>
public record ImportProductsRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(ProductImportService.MaxProducts, ErrorMessage = "validation.max_length")]
    IReadOnlyList<string> CreateExternalIds);
