namespace ExcelFormAssistant.Models;

/// <summary>Contenu lu d'une feuille : en-têtes et lignes, plus la liste des feuilles du classeur.</summary>
public sealed record SheetData(
    string SheetName,
    IReadOnlyList<string> SheetNames,
    IReadOnlyList<ExcelColumn> Columns,
    IReadOnlyList<ExcelRow> Rows);
