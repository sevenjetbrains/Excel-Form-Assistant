using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Lecture seule des fichiers .xlsx. Le fichier original n'est jamais modifié,
/// et il peut rester ouvert dans Excel pendant la lecture.
/// </summary>
public sealed class ExcelService
{
    private const string DateFormat = "dd/MM/yyyy";

    public IReadOnlyList<string> GetSheetNames(string path)
    {
        using var workbook = OpenWorkbook(path);
        return workbook.Worksheets.Select(ws => ws.Name).ToList();
    }

    /// <summary>Lit une feuille (la première si <paramref name="sheetName"/> est null ou introuvable).</summary>
    public SheetData LoadSheet(string path, string? sheetName = null)
    {
        using var workbook = OpenWorkbook(path);

        var worksheet = (sheetName is not null && workbook.TryGetWorksheet(sheetName, out var named))
            ? named
            : workbook.Worksheets.First();

        int lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        var headers = Enumerable.Range(1, lastColumn)
            .Select(c => GetDisplayText(worksheet.Cell(1, c)))
            .ToList();
        var columns = BuildColumns(headers);

        var rows = new List<ExcelRow>();
        for (int r = 2; r <= lastRow; r++)
        {
            var values = new string[lastColumn];
            for (int c = 1; c <= lastColumn; c++)
                values[c - 1] = GetDisplayText(worksheet.Cell(r, c));

            // Une ligne entièrement vide n'est pas une personne : on l'ignore.
            if (values.Any(v => v.Length > 0))
                rows.Add(new ExcelRow(r, values));
        }

        return new SheetData(worksheet.Name, columns, rows);
    }

    /// <summary>
    /// Texte tel qu'affiché dans Excel. Les dates sont toujours rendues en JJ/MM/AAAA
    /// (jamais le numéro de série, jamais au format de la culture du PC).
    /// </summary>
    public static string GetDisplayText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank)
            return string.Empty;

        if (value.IsDateTime)
        {
            var date = value.GetDateTime();
            if (date.TimeOfDay == TimeSpan.Zero)
                return date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        return cell.GetFormattedString(CultureInfo.CurrentCulture);
    }

    /// <summary>En-tête vide → « Colonne N » ; en-tête en double → « Nom (2) ».</summary>
    public static IReadOnlyList<ExcelColumn> BuildColumns(IReadOnlyList<string> headers)
    {
        var used = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var columns = new List<ExcelColumn>(headers.Count);

        for (int i = 0; i < headers.Count; i++)
        {
            string baseName = string.IsNullOrWhiteSpace(headers[i])
                ? $"Colonne {i + 1}"
                : headers[i].Trim();

            string name = baseName;
            for (int n = 2; !used.Add(name); n++)
                name = $"{baseName} ({n})";

            columns.Add(new ExcelColumn(i, name));
        }

        return columns;
    }

    private static XLWorkbook OpenWorkbook(string path)
    {
        // FileShare.ReadWrite : autorise la lecture pendant qu'Excel garde le fichier ouvert.
        // On copie en mémoire pour relâcher le fichier immédiatement.
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var memory = new MemoryStream();
        file.CopyTo(memory);
        memory.Position = 0;
        return new XLWorkbook(memory);
    }
}
