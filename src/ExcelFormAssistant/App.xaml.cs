using System.Windows;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;
using ExcelFormAssistant.Views;
using Microsoft.Win32;

namespace ExcelFormAssistant;

public partial class App : Application
{
    private ClipboardService? _clipboard;
    private HotkeyService? _hotkeys;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        var dataMenu = new DataMenuPresenter(viewModel, _clipboard, _hotkeys);

        // Raccourci utilisable depuis le formulaire à remplir, sans revenir à cette fenêtre.
        var shortcut = new GlobalShortcutService(_hotkeys);
        shortcut.Enable(dataMenu.Show);
        viewModel.ShortcutText = shortcut.StatusText;

        window = new MainWindow(viewModel, dataMenu);
        window.Show();

        // Permet d'ouvrir un fichier passé en argument (glisser sur l'exe, tests).
        if (e.Args.Length > 0)
            viewModel.LoadFile(e.Args[0]);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _clipboard?.Dispose();
        base.OnExit(e);
    }
}
