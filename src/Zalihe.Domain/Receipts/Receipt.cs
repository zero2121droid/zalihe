using Zalihe.Domain.Items;
using Zalihe.Domain.Tenants;

namespace Zalihe.Domain.Receipts;

public enum ReceiptStatus
{
    /// <summary>Being filled in; takes nothing into stock yet.</summary>
    Draft,

    /// <summary>Taken into stock; never changed again (a mistake is fixed with a correction).</summary>
    Posted,
}

/// <summary>
/// Goods received from a supplier on one document (invoice or delivery note), with all its lines
/// (SPEC 11.7). Posting takes every line into stock at once; a posted receipt is never changed.
/// </summary>
public class Receipt : ITenantOwned
{
    public const int SupplierNameMaxLength = 200;
    public const int DocumentNumberMaxLength = 64;
    public const int DocumentKeyMaxLength = SupplierNameMaxLength + DocumentNumberMaxLength + 1;

    private readonly List<ReceiptLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>The supplier as the user typed it, e.g. "Kafa Import d.o.o.".</summary>
    public string SupplierName { get; private set; }

    /// <summary>The invoice or delivery note number, e.g. "F-2026-0154"; null when there is none.</summary>
    public string? DocumentNumber { get; private set; }

    /// <summary>
    /// The supplier and document number in a form that ignores case and spacing (<see cref="KeyFor"/>), to
    /// find the same document entered twice; null without a document number.
    /// </summary>
    public string? DocumentKey { get; private set; }

    /// <summary>The date on the document, which may be earlier than the day the goods were taken in.</summary>
    public DateOnly DocumentDate { get; private set; }

    public ReceiptStatus Status { get; private set; }

    /// <summary>Who entered the receipt; null when unknown.</summary>
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the goods were taken into stock; null while a draft.</summary>
    public DateTimeOffset? PostedAt { get; private set; }

    public IReadOnlyList<ReceiptLine> Lines => _lines;

    public Receipt(Guid tenantId, string supplierName, string? documentNumber, DateOnly documentDate, Guid? createdBy, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierName);
        supplierName = supplierName.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(supplierName.Length, SupplierNameMaxLength, nameof(supplierName));
        documentNumber = string.IsNullOrWhiteSpace(documentNumber) ? null : documentNumber.Trim();
        if (documentNumber is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(documentNumber.Length, DocumentNumberMaxLength, nameof(documentNumber));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        SupplierName = supplierName;
        DocumentNumber = documentNumber;
        DocumentKey = KeyFor(supplierName, documentNumber);
        DocumentDate = documentDate;
        Status = ReceiptStatus.Draft;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    // For EF Core.
    private Receipt()
    {
        SupplierName = null!;
    }

    /// <summary>Adds a line while the receipt is a draft; the same item may be on several lines.</summary>
    /// <param name="unitPrice">The purchase price per unit on the document; null when it has none or the user may not see prices.</param>
    public ReceiptLine AddLine(Item item, decimal quantity, decimal? unitPrice)
    {
        if (Status != ReceiptStatus.Draft) throw new InvalidOperationException("A posted receipt can't be changed.");
        if (item.TenantId != TenantId) throw new InvalidOperationException("The item belongs to a different company.");

        var line = new ReceiptLine(this, _lines.Count, item, quantity, unitPrice);
        _lines.Add(line);
        return line;
    }

    /// <summary>Marks the receipt as taken into stock; the caller records a receipt movement per line.</summary>
    public void Post(DateTimeOffset now)
    {
        if (Status != ReceiptStatus.Draft) throw new InvalidOperationException("The receipt is already posted.");
        if (_lines.Count == 0) throw new InvalidOperationException("A receipt needs at least one line.");

        Status = ReceiptStatus.Posted;
        PostedAt = now;
    }

    /// <summary>
    /// The supplier and document number compared case- and space-insensitively, to warn when the same
    /// document is entered twice; null without a document number.
    /// </summary>
    public static string? KeyFor(string supplierName, string? documentNumber) =>
        string.IsNullOrWhiteSpace(documentNumber)
            ? null
            : $"{Normalize(supplierName)}|{Normalize(documentNumber)}";

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
