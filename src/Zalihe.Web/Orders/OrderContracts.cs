using System.ComponentModel.DataAnnotations;
using Zalihe.Application.Orders;
using Zalihe.Domain.Orders;

namespace Zalihe.Web.Orders;

/// <param name="Lines">At least one line; the same item may appear more than once.</param>
/// <param name="Reference">Optional, e.g. "IG 154".</param>
/// <param name="Note">Optional; never the customer's address or phone.</param>
public record CreateOrderRequest(
    [Required(ErrorMessage = "validation.required")]
    IReadOnlyList<CreateOrderLineRequest>? Lines,
    [MaxLength(ExternalOrder.ReferenceMaxLength, ErrorMessage = "validation.max_length")]
    string? Reference = null,
    [MaxLength(ExternalOrder.NoteMaxLength, ErrorMessage = "validation.max_length")]
    string? Note = null);

// Range limits are parsed in the invariant culture, see ItemContractsTests.
public record CreateOrderLineRequest(
    [Required(ErrorMessage = "validation.required")]
    Guid? ItemId,
    [Required(ErrorMessage = "validation.required")]
    [Range(typeof(decimal), "-999999999999999.999", "999999999999999.999", ErrorMessage = "validation.out_of_range", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal? Quantity);

public record SetOrderStatusRequest(
    [Required(ErrorMessage = "validation.required")]
    ManualOrderStatusChange? Status);
