using System.Globalization;
using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Recherche dans les lignes affichées. Insensible à la casse et aux accents
/// (« benaissa » trouve « Benaïssa »), et chaque mot tapé doit se trouver quelque part
/// dans la ligne : « benali ahmed » retrouve la ligne même si le nom et le prénom
/// sont dans deux colonnes différentes.
/// </summary>
public static class RowSearch
{
    private const CompareOptions Comparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Lignes correspondant au texte tapé ; toutes les lignes si la recherche est vide.</summary>
    public static IReadOnlyList<ExcelRow> Filter(IReadOnlyList<ExcelRow> rows, string? text)
    {
        var terms = SplitTerms(text);
        return terms.Length == 0 ? rows : [.. rows.Where(row => Matches(row, terms))];
    }

    public static bool Matches(ExcelRow row, string? text)
    {
        var terms = SplitTerms(text);
        return terms.Length == 0 || Matches(row, terms);
    }

    private static bool Matches(ExcelRow row, string[] terms) =>
        terms.All(term => row.Values.Any(value => Contains(value, term)));

    private static bool Contains(string value, string term) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(value, term, Comparison) >= 0;

    /// <summary>La recherche est découpée en mots : l'ordre et les espaces en trop n'importent pas.</summary>
    private static string[] SplitTerms(string? text) =>
        (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
