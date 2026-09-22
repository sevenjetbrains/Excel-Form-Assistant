namespace ExcelFormAssistant.Models;

/// <summary>Une ligne de données, avec le texte tel qu'affiché dans Excel.</summary>
/// <param name="RowNumber">Numéro de la ligne dans Excel (2 = première ligne de données).</param>
/// <param name="Values">Une valeur par colonne ; chaîne vide pour une cellule vide.</param>
public sealed record ExcelRow(int RowNumber, IReadOnlyList<string> Values);
