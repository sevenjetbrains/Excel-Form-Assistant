namespace ExcelFormAssistant.Models;

/// <summary>Contenu lu d'une feuille : en-têtes et lignes.</summary>
public sealed record SheetData(string SheetName, IReadOnlyList<ExcelColumn> Columns, IReadOnlyList<ExcelRow> Rows);
