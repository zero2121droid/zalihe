using System.ComponentModel.DataAnnotations;
using Zalihe.Domain.Items;

namespace Zalihe.Web.Items;

/// <summary>Item data for both creating and updating an item.</summary>
// Range limits are parsed in the invariant culture; with the server culture ("sr-Latn" uses a
// decimal comma) parsing "9999999999999999.99" throws. See ItemContractsTests.
public record ItemRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(Item.NameMaxLength, ErrorMessage = "validation.max_length")]
    string Name,
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(Item.SkuMaxLength, ErrorMessage = "validation.max_length")]
    string Sku,
    [Required(ErrorMessage = "validation.required")]
    Unit? Unit,
    [MaxLength(Item.BarcodeMaxLength, ErrorMessage = "validation.max_length")]
    string? Barcode = null,
    [MaxLength(Item.CategoryMaxLength, ErrorMessage = "validation.max_length")]
    string? Category = null,
    [MaxLength(Item.GroupNameMaxLength, ErrorMessage = "validation.max_length")]
    string? GroupName = null,
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "validation.not_negative", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal? PurchasePrice = null,
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "validation.not_negative", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal? SalePrice = null,
    [Range(typeof(decimal), "0", "999999999999999.999", ErrorMessage = "validation.not_negative", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal MinStock = 0);
