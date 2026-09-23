using System.Windows;
using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Position et taille de la fenêtre d'une session à l'autre. Un écran débranché, une
/// résolution changée ou un fichier de paramètres abîmé ne doivent pas faire réapparaître
/// la fenêtre hors de l'écran ni minuscule.
/// </summary>
public static class WindowPlacement
{
    /// <summary>Largeur et hauteur minimales devant rester visibles pour pouvoir attraper la fenêtre.</summary>
    private const double MinVisible = 80;

    /// <summary>Vrai si la position enregistrée laisse la fenêtre attrapable sur les écrans actuels.</summary>
    public static bool IsUsable(WindowBounds? bounds, Rect screens)
    {
        // Les nombres sont vérifiés avant de construire le Rect, qui refuse une taille négative.
        if (bounds is null
            || !double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top)
            || !(bounds.Width >= MinVisible) || !(bounds.Height >= MinVisible))
            return false;

        var window = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        window.Intersect(screens);
        return window.Width >= MinVisible && window.Height >= MinVisible;
    }

    /// <summary>Applique la position enregistrée ; sans effet si elle n'est plus utilisable.</summary>
    public static void Apply(Window window, WindowBounds? bounds)
    {
        if (!IsUsable(bounds, VirtualScreen))
            return; // la fenêtre garde son placement par défaut (centrée)

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = bounds!.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        if (bounds.Maximized)
            window.WindowState = WindowState.Maximized;
    }

    /// <summary>Relève la position à enregistrer ; agrandie, c'est la taille fenêtrée qui est retenue.</summary>
    public static WindowBounds Capture(Window window)
    {
        bool maximized = window.WindowState == WindowState.Maximized;
        var area = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;
        return new WindowBounds(area.Left, area.Top, area.Width, area.Height, maximized);
    }

    /// <summary>Rectangle couvrant tous les écrans branchés.</summary>
    private static Rect VirtualScreen => new(
        SystemParameters.VirtualScreenLeft,
        SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth,
        SystemParameters.VirtualScreenHeight);
}
