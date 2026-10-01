using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Shouldly;
using Zalihe.Web.Items;

namespace Zalihe.IntegrationTests.Items;

public class ItemContractsTests
{
    // Regression: [Range] limits like "9999999999999999.99" were parsed with the server culture.
    // On "sr-Latn" (comma as decimal separator) parsing threw, so every item create returned 500.
    // Attributes are read fresh via reflection, so no cached conversion hides the problem.
    [Theory]
    [InlineData(nameof(ItemRequest.PurchasePrice))]
    [InlineData(nameof(ItemRequest.SalePrice))]
    [InlineData(nameof(ItemRequest.MinStock))]
    public void RangeAttribute_SerbianCulture_ValidatesWithoutThrowing(string parameterName)
    {
        // Arrange
        var parameter = typeof(ItemRequest).GetConstructors().Single().GetParameters()
            .Single(p => p.Name == parameterName);
        var range = parameter.GetCustomAttribute<RangeAttribute>()!;
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sr-Latn-RS");

        try
        {
            // Act & Assert
            range.IsValid(900.5m).ShouldBeTrue();
            range.IsValid(-1m).ShouldBeFalse();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
