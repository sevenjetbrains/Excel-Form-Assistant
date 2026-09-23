using System.IO;
using System.Windows;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;
using ExcelFormAssistant.Views;
using Microsoft.Win32;

namespace ExcelFormAssistant;

public partial class App : Application
{
    private readonly SettingsService _settings = new();

    private ClipboardService? _clipboard;
    private HotkeyService? _hotkeys;
    private MouseHookService? _mouseHook;
    private MainViewModel? _viewModel;
    private WindowBounds? _windowBounds;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = _settings.Load();

        MainWindow? window = null;
        var viewModel = new MainViewModel(
            new ExcelService(),
            pickFile: () =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Ouvrir un fichier Excel",
                    Filter = "Classeurs Excel (*.xlsx)|*.xlsx",
                };
                return dialog.ShowDialog(window) == true ? dialog.FileName : null;
            },
            showError: message =>
                MessageBox.Show(window!, message, "Excel Form Assistant", MessageBoxButton.OK, MessageBoxImage.Warning));

        _clipboard = new ClipboardService();
        _hotkeys = new HotkeyService();
        var dataMenu = new DataMenuPresenter(viewModel, new PasteService(_clipboard), _hotkeys);

        // Raccourci utilisable depuis le formulaire à remplir, sans revenir à cette fenêtre.
        var shortcut = new GlobalShortcutService(_hotkeys);
        shortcut.Enable(dataMenu.Show);

        // Maj + clic droit sur un champ. Le hook doit rendre la main tout de suite — le
        // système attend sa réponse pour délivrer le clic — donc le menu s'ouvre juste après.
        _mouseHook = new MouseHookService(() => Dispatcher.BeginInvoke(dataMenu.ShowOnClickedField));

        viewModel.ShortcutText = _mouseHook.IsInstalled
            ? $"{shortcut.StatusText} · Maj+clic droit sur un champ"
            : shortcut.StatusText;

        _viewModel = viewModel;
        window = new MainWindow(viewModel, dataMenu);
        WindowPlacement.Apply(window, settings.Window);
        window.Closing += (_, _) => _windowBounds = WindowPlacement.Capture(window);
        window.Show();

        // Permet d'ouvrir un fichier passé en argument (glisser sur l'exe, tests).
        if (e.Args.Length > 0)
            viewModel.LoadFile(e.Args[0]);

        // Sinon, le fichier de la dernière fois. Disparu, on rouvre sans rien dire :
        // un message d'erreur au démarrage n'apprendrait rien d'utile.
        else if (settings.FilePath is not null && File.Exists(settings.FilePath))
            viewModel.LoadFile(settings.FilePath, settings.SheetName);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings.Save(new Settings
        {
            FilePath = _viewModel?.FilePath,
            SheetName = _viewModel?.SelectedSheet,
            Window = _windowBounds,
        });

        _mouseHook?.Dispose();
        _hotkeys?.Dispose();
        _clipboard?.Dispose();
        base.OnExit(e);
    }
}
