using System.ComponentModel.DataAnnotations;
using Zalihe.Application.Stock;
using Zalihe.Domain.Stock;

namespace Zalihe.Web.Stock;

/// <param name="Kind">receipt, sale or return with a positive quantity; count with the counted stock.</param>
/// <param name="Quantity">Amount received, sold or returned; for count the stock found on the shelf.</param>
/// <param name="Note">Optional, except for count, where it says why (count, write-off, damage).</param>
// Range limits are parsed in the invariant culture, see ItemContractsTests.
public record RecordMovementRequest(
    [Required(ErrorMessage = "validation.required")]
    ManualMovementKind? Kind,
    [Required(ErrorMessage = "validation.required")]
    [Range(typeof(decimal), "-999999999999999.999", "999999999999999.999", ErrorMessage = "validation.out_of_range", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal? Quantity,
    [MaxLength(StockMovement.NoteMaxLength, ErrorMessage = "validation.max_length")]
    string? Note = null);

public record RecordMovementResponse(StockMovementDto Movement, decimal Stock);
