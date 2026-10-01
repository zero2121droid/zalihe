using System.ComponentModel.DataAnnotations;
using Zalihe.Application.Imports;
using Zalihe.Domain.Items;

namespace Zalihe.Web.Imports;

public class ImportFileForm
{
    [Required(ErrorMessage = "validation.required")]
    public IFormFile? File { get; set; }
}

/// <summary>The CSV file and which column (0-based) holds each field; unmapped fields are left empty.</summary>
public class ItemImportForm : ImportFileForm
{
    public int? NameColumn { get; set; }
    public int? SkuColumn { get; set; }
    public int? BarcodeColumn { get; set; }
    public int? UnitColumn { get; set; }
    public int? CategoryColumn { get; set; }
    public int? GroupNameColumn { get; set; }
    public int? PurchasePriceColumn { get; set; }
    public int? SalePriceColumn { get; set; }
    public int? MinStockColumn { get; set; }
    public int? InitialStockColumn { get; set; }

    /// <summary>Used for rows without a unit, or when no unit column is mapped.</summary>
    public Unit DefaultUnit { get; set; } = Unit.Kom;

    public Dictionary<ItemImportField, int> ToMapping()
    {
        var columns = new Dictionary<ItemImportField, int?>
        {
            [ItemImportField.Name] = NameColumn,
            [ItemImportField.Sku] = SkuColumn,
            [ItemImportField.Barcode] = BarcodeColumn,
            [ItemImportField.Unit] = UnitColumn,
            [ItemImportField.Category] = CategoryColumn,
            [ItemImportField.GroupName] = GroupNameColumn,
            [ItemImportField.PurchasePrice] = PurchasePriceColumn,
            [ItemImportField.SalePrice] = SalePriceColumn,
            [ItemImportField.MinStock] = MinStockColumn,
            [ItemImportField.InitialStock] = InitialStockColumn,
        };
        return columns.Where(c => c.Value is not null).ToDictionary(c => c.Key, c => c.Value!.Value);
    }
}
