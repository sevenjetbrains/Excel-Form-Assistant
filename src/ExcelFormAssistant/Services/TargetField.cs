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

    /// <summary>
    /// Liste déroulante, y compris les listes qui se cherchent comme Select2 : on l'ouvre,
    /// on y écrit la recherche s'il y en a une, et on actionne l'option qui correspond.
    /// </summary>
    List,
}

/// <summary>Ce qu'a donné le remplissage d'un champ par l'accessibilité.</summary>
public enum FillResult
{
    /// <summary>La valeur est en place.</summary>
    Done,

    /// <summary>
    /// Plusieurs options restent possibles : la liste est laissée ouverte et filtrée,
    /// à l'utilisateur de trancher. On ne choisit jamais à sa place.
    /// </summary>
    Narrowed,

    /// <summary>Le champ n'a pas voulu : il reste le presse-papiers.</summary>
    Failed,
}

/// <summary>
/// Champ sous le curseur au moment du clic droit, et ce qu'on peut en faire.
/// </summary>
/// <param name="Fill">
/// Remplissage par l'accessibilité, si le champ l'accepte : écriture de la valeur pour un
/// champ de saisie, choix de l'option pour une liste.
/// </param>
/// <param name="FocusPoint">
/// Où cliquer pour donner le focus. Pour une date, le bord gauche du champ : la frappe
/// commence à la case sous le curseur, et il faut donc viser le jour, pas le mois ni l'année.
/// </param>
public sealed record TargetField(FieldKind Kind, Func<string, FillResult>? Fill = null, Point? FocusPoint = null)
{
    public static readonly TargetField Unknown = new(FieldKind.Unknown);

    /// <summary>Décision isolée des appels Windows, pour rester vérifiable.</summary>
    /// <param name="isList">
    /// Le champ est annoncé comme une liste déroulante, et il s'ouvre. Pouvoir s'ouvrir ne
    /// suffit pas : un champ de saisie ordinaire s'ouvre aussi, pour montrer les suggestions
    /// de saisie automatique du navigateur. Le prendre pour une liste reviendrait à chercher
    /// la valeur parmi ces suggestions au lieu de l'écrire.
    /// </param>
    internal static FieldKind KindOf(bool hasValue, bool isReadOnly, int spinnerCount, bool isList)
    {
        if (spinnerCount >= 3)
            return FieldKind.Date; // jour + mois + année

        // Avant le cas du champ de saisie : une liste Select2 annonce aussi une valeur,
        // mais l'écrire ne change rien.
        if (isList)
            return FieldKind.List;

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
    /// Comparaison des libellés d'options : insensible à la casse et aux accents, comme la
    /// recherche dans le tableau.
    /// </summary>
    internal static bool SameLabel(string option, string value) =>
        CultureInfo.InvariantCulture.CompareInfo.Compare(option.Trim(), value.Trim(),
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

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
