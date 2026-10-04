using System.Windows;

namespace ExcelFormAssistant.Models;

/// <summary>D'où vient le libellé d'un champ détecté : du plus fiable au moins fiable.</summary>
public enum LabelSource
{
    /// <summary>Nom accessible du champ (sur une page web : son &lt;label&gt;).</summary>
    AccessibleName,

    /// <summary>Élément texte désigné comme libellé du champ (LabeledBy).</summary>
    LabeledBy,

    /// <summary>Texte d'aide ou texte grisé affiché dans le champ vide (placeholder).</summary>
    HelpText,

    /// <summary>Texte le plus proche à gauche ou au-dessus du champ.</summary>
    NearbyText,

    /// <summary>Aucun libellé trouvé.</summary>
    None,
}

/// <summary>Un champ de formulaire trouvé dans la zone sélectionnée.</summary>
/// <param name="Kind">Type de champ lisible : « Zone de texte », « Liste déroulante »…</param>
/// <param name="Label">Libellé trouvé ; vide si <paramref name="LabelSource"/> vaut None.</param>
/// <param name="Bounds">Position à l'écran, en pixels.</param>
/// <param name="HasValue">Le champ contient déjà quelque chose (jamais la valeur elle-même).</param>
public sealed record DetectedField(
    string Kind,
    string Label,
    LabelSource LabelSource,
    Rect Bounds,
    bool IsPassword,
    bool IsReadOnly,
    bool HasValue);
