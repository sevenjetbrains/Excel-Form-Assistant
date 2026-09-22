using System.Windows.Input;
using System.Windows.Interop;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Services;

/// <summary>Raccourcis clavier globaux (API RegisterHotKey), actifs depuis n'importe quelle application.</summary>
public sealed class HotkeyService : IDisposable
{
    // Plage d'identifiants autorisée pour une application : 0x0000 – 0xBFFF.
    private const int MaxId = 0xBFFF;

    private readonly HwndSource _window;
    private readonly Dictionary<int, Action> _handlers = [];

    public HotkeyService()
    {
        // Fenêtre invisible qui reçoit les messages WM_HOTKEY.
        _window = new HwndSource(new HwndSourceParameters("ExcelFormAssistant.Hotkeys") { ParentWindow = HWND_MESSAGE });
        _window.AddHook(WndProc);
    }

    /// <summary>
    /// Enregistre un raccourci. Renvoie son identifiant, ou null s'il est déjà pris par un autre logiciel.
    /// </summary>
    public int? Register(ModifierKeys modifiers, Key key, Action handler)
    {
        int id = 1;
        while (_handlers.ContainsKey(id))
            id++;
        if (id > MaxId)
            return null;

        // Les valeurs de ModifierKeys (Alt=1, Control=2, Shift=4, Windows=8) sont celles de l'API.
        uint nativeModifiers = (uint)modifiers | MOD_NOREPEAT;
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(_window.Handle, id, nativeModifiers, virtualKey))
            return null;

        _handlers[id] = handler;
        return id;
    }

    public void Unregister(int id)
    {
        if (_handlers.Remove(id))
            UnregisterHotKey(_window.Handle, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var handler))
        {
            handled = true;
            handler();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys.ToList())
            Unregister(id);
        _window.Dispose();
    }
}
