using System.ComponentModel.DataAnnotations;
using Zalihe.Domain.Receipts;

namespace Zalihe.Web.Receipts;

/// <param name="DocumentNumber">The invoice or delivery note number; optional.</param>
/// <param name="DocumentDate">The date on the document.</param>
/// <param name="AllowDuplicate">Post even when this supplier's document with the same number is already posted.</param>
public record CreateReceiptRequest(
    [Required(ErrorMessage = "validation.required")]
    [MaxLength(Receipt.SupplierNameMaxLength, ErrorMessage = "validation.max_length")]
    string? SupplierName,
    [MaxLength(Receipt.DocumentNumberMaxLength, ErrorMessage = "validation.max_length")]
    string? DocumentNumber,
    [Required(ErrorMessage = "validation.required")]
    DateOnly? DocumentDate,
    [Required(ErrorMessage = "validation.required")]
    IReadOnlyList<CreateReceiptLineRequest>? Lines,
    bool AllowDuplicate = false);

/// <param name="UnitPrice">Purchase price per unit on the document; optional, and only for users who see purchase prices.</param>
/// <param name="UpdatePurchasePrice">Make the unit price the item's purchase price.</param>
// Range limits are parsed in the invariant culture, see ItemContractsTests.
public record CreateReceiptLineRequest(
    [Required(ErrorMessage = "validation.required")]
    Guid? ItemId,
    [Required(ErrorMessage = "validation.required")]
    [Range(typeof(decimal), "-999999999999999.999", "999999999999999.999", ErrorMessage = "validation.out_of_range", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal? Quantity,
    [Range(typeof(decimal), "-9999999999999999.99", "9999999999999999.99", ErrorMessage = "validation.out_of_range", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal? UnitPrice = null,
    bool UpdatePurchasePrice = false);
