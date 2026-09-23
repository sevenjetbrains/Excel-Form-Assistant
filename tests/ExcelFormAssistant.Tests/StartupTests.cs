using System.IO;
using System.Windows;
using System.Windows.Threading;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;
using ExcelFormAssistant.Views;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Démarrage tel que l'application le fait : fenêtre affichée, puis fichier chargé.
/// Aucune ligne ne doit être active avant un double-clic ou une touche Entrée.
/// </summary>
public sealed class StartupTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "exemple.xlsx");

    [Fact]
    public void ShowingTheWindowAndLoadingAFile_ActivatesNoRow() => RunOnSta(() =>
    {
        var vm = new MainViewModel(new ExcelService(), () => null, _ => { });
        using var hotkeys = new HotkeyService();
        // Aucun envoi de touches ni de clic pendant les tests.
        var paste = new PasteService(_ => true, () => IntPtr.Zero, () => { });
        var window = new MainWindow(vm, new DataMenuPresenter(vm, paste, hotkeys));

        window.Show();
        DoEvents();
        vm.LoadFile(SamplePath);
        DoEvents();

        var active = vm.ActiveRow;
        var selected = vm.SelectedRow;
        window.Close();

        Assert.Null(active);
        Assert.Null(selected);
        Assert.Equal("Aucune ligne active (double-clic ou Entrée sur une ligne)", vm.ActiveRowText);
    });

    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
