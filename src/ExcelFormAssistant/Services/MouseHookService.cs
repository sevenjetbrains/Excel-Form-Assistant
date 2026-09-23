using System.Windows.Threading;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Surveille les clics dans toutes les applications (hook bas niveau WH_MOUSE_LL) pour
/// reconnaître « Maj + clic droit », le geste qui ouvre le menu « Données Excel » sur un champ.
///
/// Ce clic-là est avalé : l'application sous le curseur ne le voit pas, donc son menu
/// contextuel habituel n'apparaît pas et le nôtre prend sa place. Le clic droit seul,
/// lui, n'est jamais touché.
///
/// Le hook vit sur son propre thread, avec sa propre boucle de messages : Windows supprime
/// sans prévenir un hook bas niveau qui met plus de 300 ms à répondre (LowLevelHooksTimeout),
/// et le thread d'interface, occupé à afficher le menu et la bulle, dépasse ce délai. Ici la
/// réponse ne dépend que de ce thread-là, qui ne fait rien d'autre.
/// </summary>
public sealed class MouseHookService : IDisposable
{
    private readonly Action _onTrigger;
    private readonly Func<bool> _isShiftDown;

    // Le délégué doit rester référencé : sinon le ramasse-miettes le libère et Windows plante.
    private HookProc? _callback;
    private IntPtr _hook;
    private Dispatcher? _hookThread;

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

        // On attend que le hook soit posé pour que IsInstalled soit déjà significatif.
        using var installed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _callback = HookCallback;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _callback, IntPtr.Zero, 0);
            _hookThread = Dispatcher.CurrentDispatcher;
            installed.Set();
            Dispatcher.Run(); // boucle de messages : c'est elle qui fait appeler le hook
        })
        {
            Name = "ExcelFormAssistant.MouseHook",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal, // répondre avant le délai de Windows
        };
        thread.Start();
        installed.Wait(TimeSpan.FromSeconds(5));
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
        // Retiré depuis le thread qui l'a posé, puis la boucle de messages s'arrête.
        _hookThread?.Invoke(() =>
        {
            if (IsInstalled)
                UnhookWindowsHookEx(_hook);
        });
        _hookThread?.InvokeShutdown();
    }
}
