using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Menu flottant « Données Excel ». Il ne prend jamais le focus ; on choisit une donnée
/// à la souris ou avec les touches 1 à 9. Échap, un clic ailleurs ou un choix le ferme.
/// </summary>
public partial class DataMenuWindow : Window
{
    private const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_MBUTTON = 0x04;

    private readonly IReadOnlyList<DataMenuItem> _items;
    private readonly HotkeyService _hotkeys;
    private readonly Action<DataMenuItem, IntPtr> _onPick;
    private readonly List<int> _registeredKeys = [];
    private readonly DispatcherTimer _outsideClickTimer;
    private readonly OutsideClickWatch _outsideClickWatch = new();
    private IntPtr _foregroundAtOpen;

    public DataMenuWindow(string title, IReadOnlyList<DataMenuItem> items, string? emptyMessage,
        HotkeyService hotkeys, Action<DataMenuItem, IntPtr> onPick)
    {
        InitializeComponent();
        _items = items;
        _hotkeys = hotkeys;
        _onPick = onPick;

        TitleText.Text = title;
        ItemsList.ItemsSource = items;
        Grid.SetIsSharedSizeScope(ItemsList, true);
        if (emptyMessage is not null)
        {
            EmptyText.Text = emptyMessage;
            EmptyText.Visibility = Visibility.Visible;
        }

        FloatingWindowHelper.MakeNonActivating(this);

        // Sans focus, la fenêtre ne voit pas les clics ailleurs : on les surveille — mais
        // seulement à partir de ShowNearCursor. Le constructeur de DispatcherTimer à quatre
        // arguments démarre le minuteur immédiatement : il tiquait donc avant que la fenêtre
        // visée ne soit relevée, la comparait à IntPtr.Zero, en concluait un changement de
        // fenêtre, et refermait le menu aussitôt ouvert.
        _outsideClickTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _outsideClickTimer.Tick += (_, _) => CloseIfClickedOutside();

        Closing += (_, _) => IsClosing = true;
        Closed += (_, _) =>
        {
            _outsideClickTimer.Stop();
            foreach (var id in _registeredKeys)
                _hotkeys.Unregister(id);
            _registeredKeys.Clear();
        };
    }

    /// <summary>Vrai dès que la fermeture a commencé (Échap, choix, clic ailleurs…).</summary>
    public bool IsClosing { get; private set; }

    /// <summary>Fenêtre qui avait le focus à l'ouverture : celle où coller la valeur choisie.</summary>
    public IntPtr TargetWindow => _foregroundAtOpen;

    /// <summary>Ferme le menu ; sans effet s'il est déjà en train de se fermer.</summary>
    public void Dismiss()
    {
        if (!IsClosing)
            Close();
    }

    /// <summary>Affiche le menu près de la souris, sans voler le focus.</summary>
    /// <param name="hooksIntoDesktop">
    /// Faux dans les tests. Le menu accroche sinon le bureau entier : il capte Échap et les
    /// touches 1 à 9 comme raccourcis globaux, et surveille la fenêtre au premier plan et les
    /// boutons de la souris. Un test ne maîtrise ni l'un ni l'autre — et n'a pas à confisquer
    /// les touches de qui utilise la machine. La logique est vérifiée séparément par
    /// <see cref="OutsideClickWatch"/> et les tests de <c>HotkeyService</c>.
    /// </param>
    public void ShowNearCursor(bool hooksIntoDesktop = true)
    {
        var cursor = FloatingWindowHelper.GetCursorPosition();
        _foregroundAtOpen = GetForegroundWindow();

        Show();
        FloatingWindowHelper.MoveNear(this, cursor, offset: 4);
        if (hooksIntoDesktop)
        {
            RegisterKeys();
            _outsideClickTimer.Start();
        }
    }

    /// <summary>Touches 1 à 9 (rangée du haut, avec ou sans Maj, et pavé numérique) et Échap.</summary>
    private void RegisterKeys()
    {
        TryRegister(ModifierKeys.None, Key.Escape, Dismiss);

        foreach (var item in _items.Where(i => i.Number <= 9))
        {
            var topRowKey = Key.D0 + item.Number;
            TryRegister(ModifierKeys.None, topRowKey, () => Pick(item));
            TryRegister(ModifierKeys.Shift, topRowKey, () => Pick(item)); // AZERTY : Maj+& = 1
            TryRegister(ModifierKeys.None, Key.NumPad0 + item.Number, () => Pick(item));
        }
    }

    private void TryRegister(ModifierKeys modifiers, Key key, Action action)
    {
        // Si une touche est déjà prise par un autre logiciel, la souris reste disponible.
        if (_hotkeys.Register(modifiers, key, action) is int id)
            _registeredKeys.Add(id);
    }

    /// <summary>
    /// Choix dès l'appui sur le bouton : dans une fenêtre sans focus, Windows refuse la capture
    /// de la souris dont Button a besoin pour déclencher Click au relâchement.
    /// </summary>
    private void Item_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Item_Click(sender, e);
    }

    /// <summary>Click reste utile pour l'accessibilité (lecteurs d'écran, tests).</summary>
    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DataMenuItem item })
            Pick(item);
    }

    private void Pick(DataMenuItem item)
    {
        if (IsClosing || !item.CanCopy)
            return; // cellule vide : rien à copier, le menu reste ouvert
        Dismiss();
        _onPick(item, _foregroundAtOpen);
    }

    private void CloseIfClickedOutside()
    {
        var cursor = FloatingWindowHelper.GetCursorPosition();
        bool buttonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0
            || (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0
            || (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0;

        if (_outsideClickWatch.ShouldDismiss(
                foregroundChanged: GetForegroundWindow() != _foregroundAtOpen, // Alt+Tab, clic ailleurs…
                anyButtonDown: buttonDown,
                cursorInsideMenu: FloatingWindowHelper.Contains(this, cursor)))
            Dismiss();
    }
}
