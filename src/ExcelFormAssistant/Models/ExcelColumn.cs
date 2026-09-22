namespace ExcelFormAssistant.Models;

/// <summary>Une colonne de la feuille, nommée d'après la ligne 1.</summary>
/// <param name="Index">Position dans <see cref="ExcelRow.Values"/> (0 = première colonne).</param>
/// <param name="Name">Nom unique (en-têtes vides ou en double déjà renommés).</param>
public sealed record ExcelColumn(int Index, string Name);
