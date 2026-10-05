using System.Globalization;
using System.Windows;

namespace ExcelFormAssistant.Services;

/// <summary>Nature du champ visé, qui décide de la façon de le remplir.</summary>
public enum FieldKind
{
    /// <summary>
    /// Non reconnu : on s'en tiendra au presse-papiers et à Ctrl+V. C'est aussi le cas des
    /// listes déroulantes, que l'application ne touche pas.
    /// </summary>
    Unknown,

    /// <summary>Champ de saisie ordinaire : la valeur peut y être écrite directement.</summary>
    Text,

    /// <summary>
    /// Sélecteur de date (<c>&lt;input type="date"&gt;</c>) : trois cases jour / mois / année.
    /// L'accessibilité refuse de l'écrire, il faut y taper les chiffres.
    /// </summary>
    Date,
}

/// <summary>Ce qu'a donné le remplissage d'un champ par l'accessibilité.</summary>
public enum FillResult
{
    /// <summary>La valeur est en place.</summary>
    Done,

    /// <summary>Le champ n'a pas voulu : il reste le presse-papiers.</summary>
    Failed,
}

/// <summary>
/// Champ sous le curseur au moment du clic droit, et ce qu'on peut en faire.
/// </summary>
/// <param name="Fill">Écriture de la valeur par l'accessibilité, si le champ l'accepte.</param>
/// <param name="FocusPoint">
/// Où cliquer pour donner le focus. Pour une date, le bord gauche du champ : la frappe
/// commence à la case sous le curseur, et il faut donc viser le jour, pas le mois ni l'année.
/// </param>
public sealed record TargetField(FieldKind Kind, Func<string, FillResult>? Fill = null, Point? FocusPoint = null)
{
    public static readonly TargetField Unknown = new(FieldKind.Unknown);

    /// <summary>Décision isolée des appels Windows, pour rester vérifiable.</summary>
    /// <param name="isList">
    /// Le champ est une liste déroulante. L'application n'y touche pas : ouvrir la liste et y
    /// choisir une option s'est révélé trop hasardeux, notamment face aux listes qui se
    /// cherchent. Ces champs restent au presse-papiers et à Ctrl+V.
    /// </param>
    internal static FieldKind KindOf(bool hasValue, bool isReadOnly, int spinnerCount, bool isList)
    {
        if (spinnerCount >= 3)
            return FieldKind.Date; // jour + mois + année

        if (isList)
            return FieldKind.Unknown;

        return hasValue && !isReadOnly ? FieldKind.Text : FieldKind.Unknown;
    }

    /// <summary>
    /// Verdict d'une écriture, d'après la valeur du champ avant et après. La relecture peut
    /// tomber avant que la page n'ait fini de se mettre à jour : on ne conclut à l'échec que
    /// si <b>rien</b> n'a changé, car un collage par-dessus une écriture qui a pris mettrait
    /// la valeur en double dans le champ.
    /// </summary>
    internal static FillResult Verdict(string? before, string? after, string text)
    {
        if (after == text)
            return FillResult.Done;

        // Changé, mais pas exactement : la page a reformaté la valeur, ou s'y attelle encore.
        return after == before ? FillResult.Failed : FillResult.Done;
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
