using System.Text;

namespace Zalihe.Application.Imports;

/// <summary>A parsed CSV file: the header row and the data rows, with their row numbers.</summary>
public record CsvFile(IReadOnlyList<string> Headers, IReadOnlyList<CsvRow> Rows, char Delimiter)
{
    /// <summary>
    /// Comma-separated files come from English-locale spreadsheets (decimal point); ";" and tab
    /// files from Serbian and other European locales, where the comma is the decimal separator.
    /// </summary>
    public bool UsesDecimalComma => Delimiter != ',';
}

/// <param name="RowNumber">Row number as a spreadsheet shows it (one per record, the header is row 1).</param>
public record CsvRow(int RowNumber, IReadOnlyList<string> Cells)
{
    public string Cell(int index) => index < Cells.Count ? Cells[index] : "";
}

/// <summary>
/// Reads CSV files as spreadsheets in Serbia and the region save them: UTF-8 or Windows-1250
/// (Excel's "CSV" in a Serbian locale), separated by ";", "," or tabs, with quoted fields.
/// </summary>
public static class CsvParser
{
    private static readonly char[] Delimiters = [';', ',', '\t'];

    static CsvParser()
    {
        // Windows code pages are not available in .NET by default.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Returns null when the file has no header row.</summary>
    public static CsvFile? Parse(byte[] content)
    {
        var text = Decode(content);
        var delimiter = DetectDelimiter(text);
        var records = ReadRecords(text, delimiter);

        // Skip empty lines before the header (and keep their numbers right).
        var header = records.FirstOrDefault(r => !IsEmpty(r.Cells));
        if (header is null)
        {
            return null;
        }

        var rows = records
            .Where(r => r.RowNumber > header.RowNumber && !IsEmpty(r.Cells))
            .ToList();
        return new CsvFile(header.Cells, rows, delimiter);
    }

    /// <summary>UTF-8 (with or without BOM) when the bytes are valid UTF-8, otherwise Windows-1250.</summary>
    public static string Decode(byte[] content)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            var text = utf8.GetString(content);
            return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1250).GetString(content);
        }
    }

    /// <summary>The separator that occurs most often in the first line, outside quotes.</summary>
    public static char DetectDelimiter(string text)
    {
        var counts = Delimiters.ToDictionary(d => d, _ => 0);
        var inQuotes = false;
        foreach (var c in text)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && (c == '\n' || c == '\r')) break;
            else if (!inQuotes && counts.ContainsKey(c)) counts[c]++;
        }

        var best = counts.MaxBy(kv => kv.Value);
        return best.Value > 0 ? best.Key : ';';
    }

    private static List<CsvRow> ReadRecords(string text, char delimiter)
    {
        var records = new List<CsvRow>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    cell.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                records.Add(new CsvRow(records.Count + 1, cells));
                cells = [];
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString().Trim());
            records.Add(new CsvRow(records.Count + 1, cells));
        }

        return records;
    }

    private static bool IsEmpty(IReadOnlyList<string> cells) => cells.All(string.IsNullOrWhiteSpace);
}
