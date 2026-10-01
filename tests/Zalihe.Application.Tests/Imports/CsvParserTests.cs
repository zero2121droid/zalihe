using System.Text;
using Shouldly;
using Zalihe.Application.Imports;

namespace Zalihe.Application.Tests.Imports;

public class CsvParserTests
{
    static CsvParserTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static CsvFile Parse(string text, Encoding? encoding = null) =>
        CsvParser.Parse((encoding ?? new UTF8Encoding(false)).GetBytes(text)).ShouldNotBeNull();

    [Fact]
    public void Parse_SemicolonSeparatedFromSerbianExcel_ReadsHeadersAndRows()
    {
        // Act
        var file = Parse("Naziv;Šifra;Stanje\r\nKafa Etiopija;KF-ETI-250;12,5\r\nŠolja;SO-1;3\r\n");

        // Assert
        file.Delimiter.ShouldBe(';');
        file.Headers.ShouldBe(["Naziv", "Šifra", "Stanje"]);
        file.Rows.Count.ShouldBe(2);
        file.Rows[0].Cells.ShouldBe(["Kafa Etiopija", "KF-ETI-250", "12,5"]);
        file.Rows[0].RowNumber.ShouldBe(2);
    }

    [Fact]
    public void Parse_CommaSeparated_DetectsCommaAndDecimalPoint()
    {
        var file = Parse("name,sku\nCoffee,C-1\n");
        file.Delimiter.ShouldBe(',');
        file.UsesDecimalComma.ShouldBeFalse();
    }

    [Fact]
    public void Parse_SemicolonSeparated_UsesDecimalComma()
    {
        Parse("Naziv;Šifra\nKafa;K-1\n").UsesDecimalComma.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Windows1250File_DecodesSerbianLetters()
    {
        // Act
        var file = Parse("Naziv;Šifra\nČajnik đak žuti ćup;CA-1\n", Encoding.GetEncoding(1250));

        // Assert
        file.Headers[1].ShouldBe("Šifra");
        file.Rows[0].Cell(0).ShouldBe("Čajnik đak žuti ćup");
    }

    [Fact]
    public void Parse_Utf8WithBom_DropsBom()
    {
        Parse("Naziv;Šifra\nKafa;K-1\n", new UTF8Encoding(true)).Headers[0].ShouldBe("Naziv");
    }

    [Fact]
    public void Parse_QuotedFields_KeepsDelimitersQuotesAndLineBreaks()
    {
        // Act
        var file = Parse("Naziv;Opis;Šifra\n\"Šolja; siva\";\"Kaže \"\"super\"\"\nnovi red\";SO-1\nKafa;;K-1\n");

        // Assert
        file.Rows[0].Cells.ShouldBe(["Šolja; siva", "Kaže \"super\"\nnovi red", "SO-1"]);
        file.Rows[1].RowNumber.ShouldBe(3);
    }

    [Fact]
    public void Parse_EmptyLines_AreSkippedButRowNumbersStayRight()
    {
        // Act
        var file = Parse("Naziv;Šifra\n\nKafa;K-1\n;\nČaj;C-1");

        // Assert
        file.Rows.Select(r => (r.RowNumber, r.Cell(0))).ShouldBe([(3, "Kafa"), (5, "Čaj")]);
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsNull()
    {
        CsvParser.Parse([]).ShouldBeNull();
        CsvParser.Parse(Encoding.UTF8.GetBytes("\n\n")).ShouldBeNull();
    }

    [Fact]
    public void Cell_RowShorterThanHeader_ReturnsEmpty()
    {
        Parse("A;B;C\n1\n").Rows[0].Cell(2).ShouldBe("");
    }
}
