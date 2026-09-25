namespace ExcelFormAssistant.Models;

/// <summary>Ce que l'application retient d'une session à l'autre.</summary>
public sealed record Settings
{
    /// <summary>Dernier fichier ouvert ; proposé au démarrage s'il existe toujours.</summary>
    public string? FilePath { get; init; }

    /// <summary>Dernière feuille affichée de ce fichier.</summary>
    public string? SheetName { get; init; }

    /// <summary>Position et taille de la fenêtre.</summary>
    public WindowBounds? Window { get; init; }
}

/// <param name="Maximized">Vrai si la fenêtre était agrandie ; les autres valeurs sont alors celles de la fenêtre restaurée.</param>
public sealed record WindowBounds(double Left, double Top, double Width, double Height, bool Maximized);
