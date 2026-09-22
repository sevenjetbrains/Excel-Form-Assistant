using System.Windows;
using System.Windows.Interop;
using ExcelFormAssistant.Services;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Fenêtres flottantes (menu, notification) qui ne prennent jamais le focus :
/// le curseur reste dans le champ du formulaire, pour que Ctrl+V colle au bon endroit.
/// </summary>
internal static class FloatingWindowHelper
{
    /// <summary>À appeler avant Show(). <paramref name="clickThrough"/> : les clics traversent la fenêtre.</summary>
    public static void MakeNonActivating(Window window, bool clickThrough = false)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Topmost = true;
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            long extra = WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | (clickThrough ? WS_EX_TRANSPARENT : 0);
            long style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64() | extra;
            SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style));

            // Un clic dans la fenêtre ne doit pas non plus l'activer.
            HwndSource.FromHwnd(handle)?.AddHook((IntPtr _, int msg, IntPtr _, IntPtr _, ref bool handled) =>
            {
                if (msg != WM_MOUSEACTIVATE)
                    return IntPtr.Zero;
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            });
        };

        // Évite un bref affichage à la mauvaise position avant MoveNear().
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
    }

    public static POINT GetCursorPosition()
    {
        GetCursorPos(out var point);
        return point;
    }

    /// <summary>
    /// Place la fenêtre (déjà affichée) en bas à droite de <paramref name="anchor"/>,
    /// en la gardant entièrement dans la zone de travail de l'écran.
    /// </summary>
    public static void MoveNear(Window window, POINT anchor, int offset = 8)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(handle, out var rect))
            return;
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromPoint(anchor, MONITOR_DEFAULTTONEAREST), ref info);
        var work = info.rcWork;

        int x = anchor.X + offset;
        int y = anchor.Y + offset;
        if (x + width > work.Right)
            x = anchor.X - offset - width;
        if (y + height > work.Bottom)
            y = anchor.Y - offset - height;
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height));

        SetWindowPos(handle, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Le point (coordonnées écran) est-il dans la fenêtre ?</summary>
    public static bool Contains(Window window, POINT point)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return GetWindowRect(handle, out var r)
            && point.X >= r.Left && point.X < r.Right && point.Y >= r.Top && point.Y < r.Bottom;
    }
}
