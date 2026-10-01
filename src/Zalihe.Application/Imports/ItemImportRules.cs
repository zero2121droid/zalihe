using System.Globalization;
using System.Text;
using Zalihe.Application.Common;
using Zalihe.Domain.Items;

namespace Zalihe.Application.Imports;

/// <summary>Item data a CSV column can be mapped to.</summary>
public enum ItemImportField
{
    Name,
    Sku,
    Barcode,
    Unit,
    Category,
    GroupName,
    PurchasePrice,
    SalePrice,
    MinStock,
    InitialStock,
}

/// <summary>A CSV row turned into item data, ready to be imported.</summary>
public record ImportedItem(
    string Name,
    string Sku,
    string? Barcode,
    Unit Unit,
    string? Category,
    string? GroupName,
    decimal? PurchasePrice,
    decimal? SalePrice,
    decimal MinStock,
    decimal InitialStock);

/// <summary>The outcome for one row: an item to import, or the errors that prevent it.</summary>
public record ImportRowResult(int RowNumber, ImportedItem? Item, IReadOnlyList<AppError> Errors);

/// <summary>
/// Pure rules of the item import: suggesting which column is which, and turning a row into
/// item data or errors. No database access, so they are unit tested directly.
/// </summary>
public static class ItemImportRules
{
    private static readonly Dictionary<ItemImportField, string[]> HeaderSynonyms = new()
    {
        [ItemImportField.Name] = ["naziv", "nazivartikla", "artikal", "proizvod", "ime", "name", "product", "productname"],
        [ItemImportField.Sku] = ["sifra", "sifraartikla", "sku", "kod", "code", "sifraproizvoda"],
        [ItemImportField.Barcode] = ["barkod", "barcode", "ean", "ean13", "gtin"],
        [ItemImportField.Unit] = ["jedinicamere", "jedinica", "jm", "jmj", "unit", "uom"],
        [ItemImportField.Category] = ["kategorija", "category", "vrsta"],
        [ItemImportField.GroupName] = ["grupa", "group", "groupname", "nazivgrupe"],
        [ItemImportField.PurchasePrice] = ["nabavnacena", "nabavna", "purchaseprice", "cost", "costprice"],
        [ItemImportField.SalePrice] = ["prodajnacena", "prodajna", "cena", "mpcena", "price", "saleprice"],
        [ItemImportField.MinStock] = ["minimalnazaliha", "minimum", "minzaliha", "min", "minstock", "reorderlevel"],
        [ItemImportField.InitialStock] = ["stanje", "pocetnostanje", "kolicina", "zaliha", "stock", "quantity", "qty"],
    };

    private static readonly Dictionary<string, Unit> UnitSynonyms = new()
    {
        ["kom"] = Unit.Kom, ["komad"] = Unit.Kom, ["komada"] = Unit.Kom, ["kos"] = Unit.Kom, ["pcs"] = Unit.Kom, ["pc"] = Unit.Kom,
        ["kg"] = Unit.Kg, ["kilogram"] = Unit.Kg,
        ["g"] = Unit.G, ["gr"] = Unit.G, ["gram"] = Unit.G,
        ["l"] = Unit.L, ["lit"] = Unit.L, ["litar"] = Unit.L, ["liter"] = Unit.L,
        ["ml"] = Unit.Ml,
        ["m"] = Unit.M, ["metar"] = Unit.M, ["meter"] = Unit.M,
        ["pak"] = Unit.Pak, ["paket"] = Unit.Pak, ["pakovanje"] = Unit.Pak, ["pack"] = Unit.Pak,
    };

    /// <summary>Guesses the column of each field from the header names; each column is used once.</summary>
    public static Dictionary<ItemImportField, int> SuggestMapping(IReadOnlyList<string> headers)
    {
        var normalized = headers.Select(Normalize).ToList();
        var mapping = new Dictionary<ItemImportField, int>();
        foreach (var (field, synonyms) in HeaderSynonyms)
        {
            // Earlier synonyms are better matches ("nabavnacena" before "cena").
            foreach (var synonym in synonyms)
            {
                var index = normalized.FindIndex(h => h == synonym);
                if (index >= 0 && !mapping.ContainsValue(index))
                {
                    mapping[field] = index;
                    break;
                }
            }
        }

        return mapping;
    }

    public static Unit? ParseUnit(string value) =>
        UnitSynonyms.TryGetValue(Normalize(value), out var unit) ? unit : null;

    /// <summary>
    /// Parses a number as spreadsheets write it. Unambiguous forms work in any file: "12,5", "12.5",
    /// "1.284,50", "1,284.50", "1.284.350". The one ambiguous form, a single separator followed by
    /// exactly three digits ("2.000", "2,000"), follows the file's convention: with a decimal comma
    /// (Serbian Excel, ";"-separated files) "2.000" is two thousand. Null for anything else.
    /// </summary>
    public static decimal? ParseDecimal(string value, bool decimalComma = true)
    {
        var text = value.Trim().Replace(" ", "").Replace("\u00A0", "").Replace('\u2212', '-');
        if (text.Length == 0) return null;

        var thousandsSeparator = decimalComma ? '.' : ',';
        var commas = text.Count(c => c == ',');
        var dots = text.Count(c => c == '.');

        char? decimalSeparator;
        if (commas > 0 && dots > 0)
        {
            // Both appear: the last one is the decimal separator ("1.284,50", "1,284.50").
            decimalSeparator = text.LastIndexOf(',') > text.LastIndexOf('.') ? ',' : '.';
        }
        else if (commas + dots == 0)
        {
            decimalSeparator = null;
        }
        else
        {
            var separator = commas > 0 ? ',' : '.';
            var count = Math.Max(commas, dots);
            var afterLast = text.Length - text.LastIndexOf(separator) - 1;
            var before = text[..text.IndexOf(separator)].TrimStart('-');
            // "0,125" can't be thousands: a grouped number never starts with a lone zero.
            var looksGrouped = afterLast == 3 && before.Length is >= 1 and <= 3 && before != "0";
            var isThousands = count > 1 || (looksGrouped && separator == thousandsSeparator);
            decimalSeparator = isThousands ? null : separator;
        }

        if (decimalSeparator is { } dec)
        {
            var index = text.LastIndexOf(dec);
            text = text[..index].Replace(",", "").Replace(".", "") + "." + text[(index + 1)..];
        }
        else
        {
            text = text.Replace(",", "").Replace(".", "");
        }

        return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    /// <summary>Turns one row into item data or a list of errors (field names match the API, e.g. "sku").</summary>
    /// <param name="decimalComma">The file's number convention (see <see cref="ParseDecimal"/>).</param>
    public static ImportRowResult ReadRow(
        CsvRow row, IReadOnlyDictionary<ItemImportField, int> mapping, Unit defaultUnit, bool decimalComma = true)
    {
        var errors = new List<AppError>();
        string Cell(ItemImportField field) => mapping.TryGetValue(field, out var index) ? row.Cell(index).Trim() : "";

        var name = Text(Cell(ItemImportField.Name), ItemImportField.Name, Item.NameMaxLength, required: true, errors);
        var sku = Text(Cell(ItemImportField.Sku), ItemImportField.Sku, Item.SkuMaxLength, required: true, errors);
        var barcode = Text(Cell(ItemImportField.Barcode), ItemImportField.Barcode, Item.BarcodeMaxLength, required: false, errors);
        var category = Text(Cell(ItemImportField.Category), ItemImportField.Category, Item.CategoryMaxLength, required: false, errors);
        var groupName = Text(Cell(ItemImportField.GroupName), ItemImportField.GroupName, Item.GroupNameMaxLength, required: false, errors);

        var unitText = Cell(ItemImportField.Unit);
        var unit = unitText.Length == 0 ? defaultUnit : ParseUnit(unitText);
        if (unit is null)
        {
            errors.Add(new AppError("import.unit_unknown", FieldName(ItemImportField.Unit),
                new Dictionary<string, object> { ["value"] = unitText }));
        }

        var purchasePrice = Number(Cell(ItemImportField.PurchasePrice), ItemImportField.PurchasePrice, decimalComma, Item.MoneyDecimals, errors);
        var salePrice = Number(Cell(ItemImportField.SalePrice), ItemImportField.SalePrice, decimalComma, Item.MoneyDecimals, errors);
        var minStock = Number(Cell(ItemImportField.MinStock), ItemImportField.MinStock, decimalComma, Item.QuantityDecimals, errors);
        var initialStock = Number(Cell(ItemImportField.InitialStock), ItemImportField.InitialStock, decimalComma, Item.QuantityDecimals, errors);

        if (errors.Count > 0)
        {
            return new ImportRowResult(row.RowNumber, null, errors);
        }

        var item = new ImportedItem(name!, sku!, barcode, unit!.Value, category, groupName,
            purchasePrice, salePrice, minStock ?? 0, initialStock ?? 0);
        return new ImportRowResult(row.RowNumber, item, []);
    }

    public static string FieldName(ItemImportField field) => CamelCase(field.ToString());

    private static string? Text(string value, ItemImportField field, int maxLength, bool required, List<AppError> errors)
    {
        if (value.Length == 0)
        {
            if (required) errors.Add(new AppError("validation.required", FieldName(field)));
            return null;
        }

        if (value.Length > maxLength)
        {
            errors.Add(new AppError("validation.max_length", FieldName(field)));
            return null;
        }

        return value;
    }

    private static decimal? Number(string value, ItemImportField field, bool decimalComma, int decimals, List<AppError> errors)
    {
        if (value.Length == 0) return null;

        var number = ParseDecimal(value, decimalComma);
        AppError? error =
            number is null ? new AppError("import.not_a_number", FieldName(field), new Dictionary<string, object> { ["value"] = value })
            : number < 0 ? new AppError("validation.not_negative", FieldName(field))
            : !Item.HasAtMostDecimals(number.Value, decimals)
                ? new AppError("validation.too_many_decimals", FieldName(field), new Dictionary<string, object> { ["max"] = decimals })
            : null;

        if (error is not null)
        {
            errors.Add(error);
            return null;
        }

        return number;
    }

    /// <summary>Lowercase, no diacritics, letters and digits only: "Šifra artikla" → "sifraartikla".</summary>
    private static string Normalize(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static string CamelCase(string value) => char.ToLowerInvariant(value[0]) + value[1..];
}
