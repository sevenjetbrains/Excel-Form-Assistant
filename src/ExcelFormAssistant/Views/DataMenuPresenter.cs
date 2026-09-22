using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Views;

/// <summary>Ouvre le menu « Données Excel » pour la ligne active et copie la donnée choisie.</summary>
public sealed class DataMenuPresenter(MainViewModel viewModel, ClipboardService clipboard, HotkeyService hotkeys)
{
    private const int MaxNotifiedLength = 40;

    private DataMenuWindow? _menu;

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

    private void Copy(DataMenuItem item)
    {
        if (clipboard.SetText(item.Value))
            NotificationWindow.ShowNearCursor($"✓ {Shorten(item.Value)} copié");
        else
            NotificationWindow.ShowNearCursor("✗ Presse-papiers occupé, réessayez", isError: true);
    }

    private static string Shorten(string value)
    {
        var singleLine = value.ReplaceLineEndings(" ");
        return singleLine.Length <= MaxNotifiedLength ? singleLine : singleLine[..MaxNotifiedLength] + "…";
    }
}
