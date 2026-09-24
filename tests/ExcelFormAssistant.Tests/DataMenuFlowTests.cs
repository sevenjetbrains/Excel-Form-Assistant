using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;
using ExcelFormAssistant.Views;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Chaîne complète : menu ouvert → choix d'une donnée → presse-papiers, collage et bulle.
/// Le clic est simulé par l'API d'accessibilité, et l'envoi du Ctrl+V est remplacé par un
/// témoin : aucun test n'envoie de touches ni de clic dans les fenêtres de la machine.
/// </summary>
[Collection("Presse-papiers")]
public sealed class DataMenuFlowTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "exemple.xlsx");

    [Fact]
    public void PickingAnItem_CopiesItPastesItAndShowsNotification() => RunOnSta(() =>
    {
        var vm = new MainViewModel(new ExcelService(), () => null, _ => { });
        vm.LoadFile(SamplePath);
        vm.SelectedRow = vm.Rows[1];
        vm.ActivateSelectedRowCommand.Execute(null);

        using var clipboard = new ClipboardService();
        using var hotkeys = new HotkeyService();

        // La fenêtre visée est relevée à l'ouverture du menu : le témoin la rend « au premier
        // plan » pour que le collage soit tenté, sans qu'aucune touche ne soit réellement envoyée.
        var target = IntPtr.Zero;
        bool pasted = false;
        var paste = new PasteService(clipboard.SetText, () => target, () => pasted = true);
        // Sans accrochage au bureau : le menu capterait Échap et les touches 1 à 9 sur toute
        // la machine, et se fermerait au moindre changement de fenêtre au premier plan.
        var presenter = new DataMenuPresenter(vm, paste, hotkeys) { HooksIntoDesktop = false };

        presenter.Show();

        // Référence prise au retour de Show(), pas en parcourant l'état WPF global :
        // PresentationSource.CurrentSources est instable quand d'autres tests créent et
        // détruisent des fenêtres en parallèle.
        var menu = presenter.CurrentMenu;
        Assert.NotNull(menu);
        DoEvents();
        Assert.False(menu.IsClosing, "le menu ne devrait pas se refermer tout seul");
        Assert.True(menu.IsVisible, "le menu devrait être affiché");
        target = menu.TargetWindow;
        var buttons = FindChildren<Button>(menu).ToList();
        Assert.Equal(5, buttons.Count);

        var invoke = (IInvokeProvider)new ButtonAutomationPeer(buttons[0]).GetPattern(PatternInterface.Invoke)!;
        invoke.Invoke();
        DoEvents();

        Assert.True(menu.IsClosing, "le menu devrait se fermer après un choix");
        Assert.Equal("AMEUR", System.Windows.Clipboard.GetText());
        Assert.True(pasted, "la valeur devrait être collée dans la fenêtre visée");
        Assert.NotNull(NotificationWindow.Current);
    });

    private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;
            foreach (var nested in FindChildren<T>(child))
                yield return nested;
        }
    }

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
