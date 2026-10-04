using System.Windows;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Retrouve le libellé d'un champ qui n'en déclare aucun : le texte le plus proche
/// à sa gauche (sur la même ligne) ou juste au-dessus (dans la même colonne).
/// </summary>
public static class LabelLocator
{
    /// <summary>Écart maximal entre un libellé placé à gauche et son champ, en pixels.</summary>
    public const double MaxLeftGap = 300;

    /// <summary>Écart maximal entre un libellé placé au-dessus et son champ, en pixels.</summary>
    public const double MaxAboveGap = 60;

    /// <summary>Au-delà, c'est un paragraphe, pas un libellé.</summary>
    private const int MaxLabelLength = 80;

    /// <summary>Tolérance de chevauchement : un libellé déborde souvent de quelques pixels.</summary>
    private const double Tolerance = 4;

    public static string? FindNearest(Rect field, IEnumerable<(string Text, Rect Bounds)> texts)
    {
        string? best = null;
        double bestDistance = double.MaxValue;

        foreach (var (text, bounds) in texts)
        {
            var label = text.Trim();
            if (label.Length == 0 || label.Length > MaxLabelLength || bounds.IsEmpty)
                continue;

            // Texte dessiné dans le champ (texte grisé, valeur) : ce n'est pas un libellé.
            if (field.Contains(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)))
                continue;

            double? distance = LeftDistance(field, bounds) ?? AboveDistance(field, bounds);
            if (distance is double d && d < bestDistance)
            {
                bestDistance = d;
                best = label;
            }
        }

        return best;
    }

    /// <summary>Texte à gauche, sur la même ligne (centre vertical du texte dans la hauteur du champ).</summary>
    private static double? LeftDistance(Rect field, Rect text)
    {
        if (text.Right > field.Left + Tolerance)
            return null;
        double middle = text.Y + text.Height / 2;
        if (middle < field.Top - Tolerance || middle > field.Bottom + Tolerance)
            return null;
        double gap = Math.Max(0, field.Left - text.Right);
        return gap <= MaxLeftGap ? gap : null;
    }

    /// <summary>Texte au-dessus, qui chevauche horizontalement le champ.</summary>
    private static double? AboveDistance(Rect field, Rect text)
    {
        if (text.Bottom > field.Top + Tolerance)
            return null;
        if (text.Right <= field.Left || text.Left >= field.Right)
            return null;
        double gap = Math.Max(0, field.Top - text.Bottom);
        return gap <= MaxAboveGap ? gap : null;
    }
}
