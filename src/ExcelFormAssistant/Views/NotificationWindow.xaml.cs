using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ExcelFormAssistant.Views;

/// <summary>Petite bulle « ✓ BENALI copié » près de la souris, qui disparaît seule.</summary>
public partial class NotificationWindow : Window
{
    private static NotificationWindow? _current;

    /// <summary>Bulle affichée, ou null. Utilisée par les tests.</summary>
    internal static NotificationWindow? Current => _current;

    private NotificationWindow(string message, bool isError)
    {
        InitializeComponent();
        MessageText.Text = message;
        Bubble.Background = new SolidColorBrush(isError ? Color.FromRgb(0xB3, 0x26, 0x1E) : Color.FromRgb(0x1E, 0x7A, 0x3C));
        // Les clics traversent la bulle : elle ne gêne jamais la saisie.
        FloatingWindowHelper.MakeNonActivating(this, clickThrough: true);
    }

    public static void ShowNearCursor(string message, bool isError = false)
    {
        _current?.Close();

        var window = new NotificationWindow(message, isError);
        _current = window;
        window.Closed += (_, _) =>
        {
            if (_current == window)
                _current = null;
        };

        window.Show();
        FloatingWindowHelper.MoveNear(window, FloatingWindowHelper.GetCursorPosition(), offset: 16);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (_, _) => window.Close();
            window.BeginAnimation(OpacityProperty, fade);
        };
        timer.Start();
    }
}
