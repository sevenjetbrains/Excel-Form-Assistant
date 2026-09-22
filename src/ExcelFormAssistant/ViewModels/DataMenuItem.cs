using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.ViewModels;

/// <summary>Une entrée du menu « Données Excel » : une colonne de la ligne active.</summary>
public sealed record DataMenuItem(int Number, string ColumnName, string Value)
{
    /// <summary>Touche de sélection rapide (1 à 9) ; vide au-delà de la 9e donnée.</summary>
    public string Shortcut => Number <= 9 ? Number.ToString() : "";

    /// <summary>Une cellule vide s'affiche « — » et n'est pas copiée.</summary>
    public bool CanCopy => Value.Length > 0;

    public string DisplayValue => CanCopy ? Value : ExcelRow.EmptyDisplay;

    public static IReadOnlyList<DataMenuItem> FromRow(ExcelRow row, IReadOnlyList<ExcelColumn> columns) =>
        columns.Select((column, i) => new DataMenuItem(i + 1, column.Name, row.Values[column.Index])).ToList();
}
