using System.Windows.Input;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Raccourci qui ouvre le menu « Données Excel » depuis n'importe quelle application.
/// Une combinaison peut déjà appartenir à un autre logiciel : on essaie la liste dans
/// l'ordre et on garde la première qui est libre.
/// </summary>
public sealed class GlobalShortcutService
{
    /// <summary>Combinaisons essayées, de la préférée à la dernière solution.</summary>
    public static readonly IReadOnlyList<GlobalShortcut> Candidates =
    [
        new(ModifierKeys.Control | ModifierKeys.Shift, Key.E),
        new(ModifierKeys.Control | ModifierKeys.Shift, Key.D),
        new(ModifierKeys.Control | ModifierKeys.Alt, Key.E),
        new(ModifierKeys.Control | ModifierKeys.Alt, Key.D),
        new(ModifierKeys.Control | ModifierKeys.Shift, Key.F12),
    ];

    /// <summary>Combinaisons de l'analyse de formulaire : touches de fonction, rarement utilisées ailleurs.</summary>
    public static readonly IReadOnlyList<GlobalShortcut> FormAnalysisCandidates =
    [
        new(ModifierKeys.Control | ModifierKeys.Shift, Key.F9),
        new(ModifierKeys.Control | ModifierKeys.Shift, Key.F10),
        new(ModifierKeys.Control | ModifierKeys.Shift, Key.F11),
    ];

    private readonly Func<GlobalShortcut, Action, int?> _register;
    private readonly Action<int> _unregister;
    private readonly IReadOnlyList<GlobalShortcut> _candidates;
    private readonly string _purpose;
    private int? _id;

    /// <param name="candidates">Combinaisons à essayer dans l'ordre ; <see cref="Candidates"/> par défaut.</param>
    /// <param name="purpose">Début du libellé affiché dans la barre d'état.</param>
    public GlobalShortcutService(HotkeyService hotkeys, IReadOnlyList<GlobalShortcut>? candidates = null,
        string purpose = "Raccourci global")
        : this((shortcut, handler) => hotkeys.Register(shortcut.Modifiers, shortcut.Key, handler), hotkeys.Unregister,
            candidates, purpose)
    {
    }

    /// <summary>Constructeur de test : l'enregistrement Windows est remplacé par des délégués.</summary>
    internal GlobalShortcutService(Func<GlobalShortcut, Action, int?> register, Action<int> unregister,
        IReadOnlyList<GlobalShortcut>? candidates = null, string purpose = "Raccourci global")
    {
        _register = register;
        _unregister = unregister;
        _candidates = candidates ?? Candidates;
        _purpose = purpose;
    }

    /// <summary>Combinaison réellement enregistrée, ou null si aucune n'était libre.</summary>
    public GlobalShortcut? Active { get; private set; }

    public string StatusText => Active is { } shortcut
        ? $"{_purpose} : {shortcut.Label}"
        : $"{_purpose} indisponible (combinaisons déjà prises)";

    /// <summary>Active le raccourci ; renvoie faux si toutes les combinaisons sont prises.</summary>
    public bool Enable(Action handler)
    {
        Disable();
        foreach (var shortcut in _candidates)
        {
            if (_register(shortcut, handler) is not int id)
                continue;
            _id = id;
            Active = shortcut;
            return true;
        }
        return false;
    }

    public void Disable()
    {
        if (_id is int id)
            _unregister(id);
        _id = null;
        Active = null;
    }
}
