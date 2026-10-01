using Microsoft.EntityFrameworkCore;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;
using Zalihe.Domain.Stock;

namespace Zalihe.Application.Imports;

public record ImportAnalysisDto(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    IReadOnlyDictionary<ItemImportField, int> SuggestedMapping,
    int RowCount);

/// <summary>A row that won't be imported: errors, or skipped because the SKU already exists.</summary>
public record ImportIssueDto(int RowNumber, string? Sku, string? Name, bool Skipped, IReadOnlyList<AppError> Errors);

public record ImportPreviewItemDto(int RowNumber, string Name, string Sku, Unit Unit, decimal InitialStock);

public record ImportPreviewDto(
    int ReadyCount,
    int ErrorCount,
    int SkippedCount,
    IReadOnlyList<ImportIssueDto> Issues,
    IReadOnlyList<ImportPreviewItemDto> Sample);

public record ImportResultDto(int ImportedCount, int ErrorCount, int SkippedCount);

/// <summary>Errors of the file as a whole (empty, too many rows, unreadable mapping).</summary>
public record ImportFileResult<T>(T? Value, AppError? Error)
{
    public static ImportFileResult<T> Ok(T value) => new(value, null);
    public static ImportFileResult<T> Failed(string code, object? max = null) =>
        new(default, new AppError(code, "file", max is null ? null : new Dictionary<string, object> { ["max"] = max }));
}

/// <summary>
/// Imports items with their opening stock from a CSV file in three steps: analyze (columns and a
/// suggested mapping), preview (what would be imported, and why some rows won't) and import.
/// Rows with errors and rows whose SKU already exists are skipped; existing items are never changed.
/// </summary>
public class ItemImportService(IAppDbContext db, ITenantContext tenant, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public const int MaxRows = 5000;
    public const string OpeningStockNote = "Početno stanje";
    private const int SampleSize = 10;

    public ImportFileResult<ImportAnalysisDto> Analyze(byte[] content)
    {
        if (IsExcel(content))
        {
            return ImportFileResult<ImportAnalysisDto>.Failed("import.excel_not_supported");
        }

        if (Read(content) is not { } file)
        {
            return ImportFileResult<ImportAnalysisDto>.Failed("import.file_empty");
        }

        var sample = file.Rows.Take(5)
            .Select(r => (IReadOnlyList<string>)file.Headers.Select((_, i) => r.Cell(i)).ToList())
            .ToList();
        return ImportFileResult<ImportAnalysisDto>.Ok(
            new ImportAnalysisDto(file.Headers, sample, ItemImportRules.SuggestMapping(file.Headers), file.Rows.Count));
    }

    public async Task<ImportFileResult<ImportPreviewDto>> PreviewAsync(
        byte[] content, IReadOnlyDictionary<ItemImportField, int> mapping, Unit defaultUnit, CancellationToken ct)
    {
        var checkedRows = await CheckAsync(content, mapping, defaultUnit, ct);
        if (checkedRows.Error is not null)
        {
            return new ImportFileResult<ImportPreviewDto>(null, checkedRows.Error);
        }

        var rows = checkedRows.Value!;
        var sample = rows.Ready.Take(SampleSize)
            .Select(r => new ImportPreviewItemDto(r.RowNumber, r.Item!.Name, r.Item.Sku, r.Item.Unit, r.Item.InitialStock))
            .ToList();
        return ImportFileResult<ImportPreviewDto>.Ok(new ImportPreviewDto(
            rows.Ready.Count, rows.ErrorCount, rows.SkippedCount, rows.Issues, sample));
    }

    public async Task<ImportFileResult<ImportResultDto>> ImportAsync(
        byte[] content, IReadOnlyDictionary<ItemImportField, int> mapping, Unit defaultUnit, CancellationToken ct)
    {
        var checkedRows = await CheckAsync(content, mapping, defaultUnit, ct);
        if (checkedRows.Error is not null)
        {
            return new ImportFileResult<ImportResultDto>(null, checkedRows.Error);
        }

        var rows = checkedRows.Value!;
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        // Everything in one SaveChanges: the import succeeds as a whole or not at all.
        foreach (var row in rows.Ready)
        {
            var data = row.Item!;
            var item = new Item(tenant.TenantId, data.Name, data.Sku, data.Unit, data.MinStock, now,
                data.Barcode, data.Category, data.GroupName, data.PurchasePrice, data.SalePrice);
            var level = new StockLevel(item, now);
            db.Items.Add(item);
            db.StockLevels.Add(level);
            db.ItemChanges.Add(ItemChange.Created(item, userId, now));

            if (data.InitialStock > 0)
            {
                var opening = StockMovement.Adjustment(item, data.InitialStock, now, StockMovementSource.Csv, userId, OpeningStockNote);
                db.StockMovements.Add(opening);
                level.Apply(opening);
            }
        }

        await db.SaveChangesAsync(ct);
        return ImportFileResult<ImportResultDto>.Ok(new ImportResultDto(rows.Ready.Count, rows.ErrorCount, rows.SkippedCount));
    }

    private sealed record CheckedRows(List<ImportRowResult> Ready, List<ImportIssueDto> Issues, int ErrorCount, int SkippedCount);

    private async Task<ImportFileResult<CheckedRows>> CheckAsync(
        byte[] content, IReadOnlyDictionary<ItemImportField, int> mapping, Unit defaultUnit, CancellationToken ct)
    {
        if (IsExcel(content))
        {
            return ImportFileResult<CheckedRows>.Failed("import.excel_not_supported");
        }

        if (Read(content) is not { } file)
        {
            return ImportFileResult<CheckedRows>.Failed("import.file_empty");
        }

        if (file.Rows.Count > MaxRows)
        {
            return ImportFileResult<CheckedRows>.Failed("import.too_many_rows", MaxRows);
        }

        if (!mapping.ContainsKey(ItemImportField.Name) || !mapping.ContainsKey(ItemImportField.Sku)
            || mapping.Values.Any(i => i < 0 || i >= file.Headers.Count))
        {
            return ImportFileResult<CheckedRows>.Failed("import.mapping_invalid");
        }

        var results = file.Rows.Select(r => ItemImportRules.ReadRow(r, mapping, defaultUnit, file.UsesDecimalComma)).ToList();

        // SKUs already in the company (the tenant filter limits this to the current company).
        var skus = results.Where(r => r.Item is not null).Select(r => r.Item!.Sku).Distinct().ToList();
        var existing = (await db.Items.Where(i => skus.Contains(i.Sku)).Select(i => i.Sku).ToListAsync(ct)).ToHashSet();

        var ready = new List<ImportRowResult>();
        var issues = new List<ImportIssueDto>();
        var firstRowOfSku = new Dictionary<string, int>();
        int errorCount = 0, skippedCount = 0;

        // Results are in the same order as the file's rows.
        foreach (var (row, result) in file.Rows.Zip(results))
        {
            string? Raw(ItemImportField field) => mapping.TryGetValue(field, out var i) && row.Cell(i).Length > 0 ? row.Cell(i).Trim() : null;

            if (result.Item is null)
            {
                errorCount++;
                issues.Add(new ImportIssueDto(result.RowNumber, Raw(ItemImportField.Sku), Raw(ItemImportField.Name), false, result.Errors));
            }
            else if (existing.Contains(result.Item.Sku))
            {
                skippedCount++;
                issues.Add(new ImportIssueDto(result.RowNumber, result.Item.Sku, result.Item.Name, true,
                    [new AppError("import.sku_exists", "sku")]));
            }
            else if (firstRowOfSku.TryGetValue(result.Item.Sku, out var firstRow))
            {
                errorCount++;
                issues.Add(new ImportIssueDto(result.RowNumber, result.Item.Sku, result.Item.Name, false,
                    [new AppError("import.sku_duplicate_in_file", "sku", new Dictionary<string, object> { ["row"] = firstRow })]));
            }
            else
            {
                firstRowOfSku[result.Item.Sku] = result.RowNumber;
                ready.Add(result);
            }
        }

        return ImportFileResult<CheckedRows>.Ok(new CheckedRows(ready, issues, errorCount, skippedCount));
    }

    /// <summary>.xlsx files are ZIP archives ("PK\u0003\u0004"); only CSV is supported for now.</summary>
    private static bool IsExcel(byte[] content) =>
        content.Length >= 4 && content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04;

    private static CsvFile? Read(byte[] content)
    {
        var file = CsvParser.Parse(content);
        return file is null || file.Headers.All(string.IsNullOrWhiteSpace) ? null : file;
    }
}
