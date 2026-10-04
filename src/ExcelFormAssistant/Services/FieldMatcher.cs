using System.Globalization;
using System.Text;
using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Rapproche le libellé d'un champ de formulaire d'une colonne Excel. Deux textes
/// correspondent s'ils sont identiques une fois normalisés : majuscules, accents,
/// ponctuation, astérisques d'obligation et deux-points ignorés (« Prénom * : » = « PRENOM »).
/// </summary>
public static class FieldMatcher
{
    /// <summary>Colonne dont le nom correspond au libellé, ou null.</summary>
    public static ExcelColumn? Match(string label, IReadOnlyList<ExcelColumn> columns)
    {
        var key = Normalize(label);
        if (key.Length == 0)
            return null;
        return columns.FirstOrDefault(column => Normalize(column.Name) == key);
    }

    /// <summary>Forme comparable d'un libellé : minuscules sans accents, mots séparés d'un espace.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue; // accent détaché de sa lettre

            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && builder.Length > 0)
                    builder.Append(' ');
                pendingSpace = false;
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingSpace = true; // ponctuation, espaces, « * », « : »…
            }
        }

        return builder.ToString();
    }
}
