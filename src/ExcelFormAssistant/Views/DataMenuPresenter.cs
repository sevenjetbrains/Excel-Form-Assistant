using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Ouvre le menu « Données Excel » pour la ligne active, puis colle la donnée choisie
/// dans le champ visé (et la laisse dans le presse-papiers dans tous les cas).
/// </summary>
public sealed class DataMenuPresenter(MainViewModel viewModel, PasteService paste, HotkeyService hotkeys)
{
    private const int MaxNotifiedLength = 40;

    private DataMenuWindow? _menu;

    /// <summary>
    /// Maj + clic droit sur un champ. Le clic droit a été avalé par le hook, donc le champ
    /// n'a pas reçu le focus : un clic gauche le lui donne avant d'ouvrir le menu.
    /// </summary>
    public void ShowOnClickedField()
    {
        PasteService.FocusUnderCursor();
        Show();
    }

    public void Show()
    {
        // Un seul menu à la fois.
        _menu?.Dismiss();

        var row = viewModel.ActiveRow;
        string title = row is null ? "Données Excel" : $"Données Excel - {row.Label}";
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
        menu.ShowNearCursor();
    }

    private void Copy(DataMenuItem item, IntPtr target)
    {
        switch (paste.CopyAndPaste(item.Value, target))
        {
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
