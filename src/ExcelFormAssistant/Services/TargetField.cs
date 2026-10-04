using System.Globalization;
using System.Windows;

namespace ExcelFormAssistant.Services;

/// <summary>Nature du champ visé, qui décide de la façon de le remplir.</summary>
public enum FieldKind
{
    /// <summary>Non reconnu : on s'en tiendra au presse-papiers et à Ctrl+V.</summary>
    Unknown,

    /// <summary>Champ de saisie ordinaire : la valeur peut y être écrite directement.</summary>
    Text,

    /// <summary>
    /// Sélecteur de date (<c>&lt;input type="date"&gt;</c>) : trois cases jour / mois / année.
    /// L'accessibilité refuse de l'écrire, il faut y taper les chiffres.
    /// </summary>
    Date,
}

/// <summary>
/// Champ sous le curseur au moment du clic droit, et ce qu'on peut en faire.
/// </summary>
/// <param name="Write">
/// Écriture directe de la valeur, si le champ l'accepte. Renvoie faux si elle n'a pas pris.
/// </param>
/// <param name="FocusPoint">
/// Où cliquer pour donner le focus. Pour une date, le bord gauche du champ : la frappe
/// commence à la case sous le curseur, et il faut donc viser le jour, pas le mois ni l'année.
/// </param>
public sealed record TargetField(FieldKind Kind, Func<string, bool>? Write = null, Point? FocusPoint = null)
{
    public static readonly TargetField Unknown = new(FieldKind.Unknown);

    /// <summary>Décision isolée des appels Windows, pour rester vérifiable.</summary>
    internal static FieldKind KindOf(bool hasValue, bool isReadOnly, int spinnerCount)
    {
        if (spinnerCount >= 3)
            return FieldKind.Date; // jour + mois + année

        return hasValue && !isReadOnly ? FieldKind.Text : FieldKind.Unknown;
    }

    /// <summary>
    /// Chiffres à taper dans un sélecteur de date : « 15/05/1993 » donne « 15051993 ».
    /// Null si la valeur n'est pas une date JJ/MM/AAAA — on ne tape jamais au hasard.
    /// </summary>
    internal static string? DateDigits(string value)
    {
        var texte = value.Trim();
        if (!DateTime.TryParseExact(texte, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return null;

        return date.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
    }
}
