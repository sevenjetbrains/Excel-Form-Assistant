using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Ouvre le menu « Données Excel » pour la ligne active, puis colle la donnée choisie
/// dans le champ visé (et la laisse dans le presse-papiers dans tous les cas).
/// </summary>
public sealed class DataMenuPresenter(MainViewModel viewModel, PasteService paste, HotkeyService hotkeys,
    FieldInspector? inspector = null)
{
    private const int MaxNotifiedLength = 40;

    private DataMenuWindow? _menu;
    private TargetField _field = TargetField.Unknown;

    /// <summary>Faux dans les tests : voir <see cref="DataMenuWindow.ShowNearCursor"/>.</summary>
    internal bool HooksIntoDesktop { get; init; } = true;

    /// <summary>Menu ouvert, ou null s'il n'y en a pas. Utilisé par les tests.</summary>
    internal DataMenuWindow? CurrentMenu => _menu;

    /// <summary>
    /// Maj + clic droit sur un champ. Le clic droit a été avalé par le hook, donc le champ
    /// n'a pas reçu le focus : un clic gauche le lui donne avant d'ouvrir le menu.
    /// </summary>
    public void ShowOnClickedField()
    {
        // Reconnaître le champ avant le clic : le curseur est encore dessus, et le menu
        // n'est pas encore affiché par-dessus.
        _field = inspector?.Inspect(FloatingWindowHelper.GetCursorPoint()) ?? TargetField.Unknown;

        PasteService.FocusUnderCursor();
        Show(_field);
    }

    public void Show() => Show(TargetField.Unknown);

    private void Show(TargetField field)
    {
        // Un seul menu à la fois.
        _menu?.Dismiss();
        _field = field;

        var row = viewModel.ActiveRow;
        string title = row is null ? "Données Excel" : $"Données Excel - {row.Label}";
        if (FieldLabel(field.Kind) is string label)
            title += $" → {label}"; // le champ reconnu, affiché pour que l'utilisateur le voie
        var items = row is null ? [] : DataMenuItem.FromRow(row, viewModel.Columns);
        string? emptyMessage = (viewModel.FilePath, row) switch
        {
            (null, _) => "Aucun fichier ouvert.",
            (_, null) => "Aucune ligne active : double-cliquez une ligne dans Excel Form Assistant.",
            _ => null,
        };

        var menu = new DataMenuWindow(title, items, emptyMessage, hotkeys, Copy);
        menu.Closed += (_, _) =>
        {
            if (_menu == menu)
                _menu = null;
        };
        _menu = menu;
        menu.ShowNearCursor(HooksIntoDesktop);
    }

    /// <summary>Nom du champ reconnu, ou null s'il n'a pas été identifié.</summary>
    private static string? FieldLabel(FieldKind kind) => kind switch
    {
        FieldKind.Date => "champ date",
        FieldKind.Text => "champ de saisie",
        _ => null,
    };

    private void Copy(DataMenuItem item, IntPtr target)
    {
        switch (paste.Fill(item.Value, _field, target))
        {
            case PasteOutcome.Written:
                NotificationWindow.ShowNearCursor($"✓ {Shorten(item.Value)} écrit dans le champ");
                break;

            case PasteOutcome.Typed:
                NotificationWindow.ShowNearCursor($"✓ {Shorten(item.Value)} saisi (champ date)");
                break;

            case PasteOutcome.Pasted:
                NotificationWindow.ShowNearCursor($"✓ {Shorten(item.Value)} collé");
                break;

            case PasteOutcome.CopiedOnly:
                NotificationWindow.ShowNearCursor($"✓ {Shorten(item.Value)} copié — Ctrl+V pour coller");
                break;

            case PasteOutcome.ClipboardBusy:
                NotificationWindow.ShowNearCursor("✗ Presse-papiers occupé, réessayez", isError: true);
                break;
        }
    }

    private static string Shorten(string value)
    {
        var singleLine = value.ReplaceLineEndings(" ");
        return singleLine.Length <= MaxNotifiedLength ? singleLine : singleLine[..MaxNotifiedLength] + "…";
    }
}
