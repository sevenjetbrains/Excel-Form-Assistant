using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Surveille les clics dans toutes les applications (hook bas niveau WH_MOUSE_LL) pour
/// reconnaître « Maj + clic droit », le geste qui ouvre le menu « Données Excel » sur un champ.
///
/// Ce clic-là est avalé : l'application sous le curseur ne le voit pas, donc son menu
/// contextuel habituel n'apparaît pas et le nôtre prend sa place. Le clic droit seul,
/// lui, n'est jamais touché.
/// </summary>
public sealed class MouseHookService : IDisposable
{
    private readonly Action _onTrigger;
    private readonly Func<bool> _isShiftDown;

    // Le délégué doit rester référencé : sinon le ramasse-miettes le libère et Windows plante.
    private readonly HookProc? _callback;
    private readonly IntPtr _hook;

    private bool _swallowedButtonDown;

    public MouseHookService(Action onTrigger)
        : this(onTrigger, () => (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0, install: true)
    {
    }

    /// <param name="install">Faux dans les tests : la décision est vérifiée sans poser de hook global.</param>
    internal MouseHookService(Action onTrigger, Func<bool> isShiftDown, bool install)
    {
        _onTrigger = onTrigger;
        _isShiftDown = isShiftDown;

        if (!install)
            return;

        _callback = HookCallback;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _callback, IntPtr.Zero, 0);
    }

    /// <summary>Faux si Windows a refusé le hook : l'application reste utilisable au clavier.</summary>
    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <summary>
    /// Décide du sort d'un clic. Le menu s'ouvre au relâchement, comme tout menu contextuel
    /// sous Windows, et les deux messages du clic sont avalés ensemble pour ne pas laisser
    /// l'application croire que le bouton est resté enfoncé.
    /// </summary>
    internal bool ShouldSwallow(int message)
    {
        switch (message)
        {
            case WM_RBUTTONDOWN when _isShiftDown():
                _swallowedButtonDown = true;
                return true;

            case WM_RBUTTONUP when _swallowedButtonDown:
                _swallowedButtonDown = false;
                _onTrigger();
                return true;

            default:
                return false;
        }
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        // code < 0 : message à transmettre sans l'examiner (règle de l'API).
        if (code >= 0 && ShouldSwallow(wParam.ToInt32()))
            return 1;

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (IsInstalled)
            UnhookWindowsHookEx(_hook);
    }
}
