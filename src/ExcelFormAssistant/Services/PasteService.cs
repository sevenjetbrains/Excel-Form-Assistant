using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Services;

/// <summary>Ce qui est arrivé à la valeur choisie dans le menu.</summary>
public enum PasteOutcome
{
    /// <summary>Copiée puis collée dans le champ.</summary>
    Pasted,

    /// <summary>Copiée seulement : la fenêtre visée n'est plus au premier plan, Ctrl+V reste à faire.</summary>
    CopiedOnly,

    /// <summary>Le presse-papiers est resté occupé par un autre logiciel.</summary>
    ClipboardBusy,
}

/// <summary>
/// Colle la valeur choisie dans le champ du formulaire, en simulant Ctrl+V dans la fenêtre
/// qui avait le focus à l'ouverture du menu. Si l'utilisateur a changé de fenêtre entre-temps,
/// rien n'est envoyé : la valeur est seulement copiée, pour ne jamais écrire ailleurs
/// que là où il l'attend.
/// </summary>
public sealed class PasteService
{
    private readonly Func<string, bool> _copy;
    private readonly Func<IntPtr> _foregroundWindow;
    private readonly Action _sendPaste;

    public PasteService(ClipboardService clipboard)
        : this(clipboard.SetText, () => GetForegroundWindow(), SendCtrlV)
    {
    }

    /// <summary>Constructeur de test : presse-papiers et envoi de touches remplacés par des délégués.</summary>
    internal PasteService(Func<string, bool> copy, Func<IntPtr> foregroundWindow, Action sendPaste)
    {
        _copy = copy;
        _foregroundWindow = foregroundWindow;
        _sendPaste = sendPaste;
    }

    /// <param name="target">Fenêtre visée, relevée à l'ouverture du menu.</param>
    public PasteOutcome CopyAndPaste(string text, IntPtr target)
    {
        // Toujours copier d'abord : même sans collage, Ctrl+V reste possible à la main.
        if (!_copy(text))
            return PasteOutcome.ClipboardBusy;

        if (target == IntPtr.Zero || _foregroundWindow() != target)
            return PasteOutcome.CopiedOnly;

        _sendPaste();
        return PasteOutcome.Pasted;
    }

    /// <summary>
    /// Clic gauche à l'endroit du curseur pour donner le focus au champ visé. Le clic droit
    /// ayant été avalé, l'application n'a rien reçu : sans ce clic, le curseur de saisie
    /// resterait là où il était.
    /// </summary>
    public static void FocusUnderCursor() => Send(
        MouseInput(MOUSEEVENTF_LEFTDOWN),
        MouseInput(MOUSEEVENTF_LEFTUP));

    private static void SendCtrlV()
    {
        // Maj est encore enfoncée juste après « Maj + clic droit » : relâchée d'abord,
        // sinon le formulaire reçoit Ctrl+Maj+V, qui ne colle pas partout.
        var inputs = new List<INPUT>(8);
        foreach (ushort modifier in (ushort[])[VK_SHIFT, VK_MENU, VK_LWIN, VK_RWIN])
        {
            if ((GetAsyncKeyState(modifier) & 0x8000) != 0)
                inputs.Add(KeyInput(modifier, KEYEVENTF_KEYUP));
        }

        inputs.Add(KeyInput(VK_CONTROL, 0));
        inputs.Add(KeyInput(VK_V, 0));
        inputs.Add(KeyInput(VK_V, KEYEVENTF_KEYUP));
        inputs.Add(KeyInput(VK_CONTROL, KEYEVENTF_KEYUP));
        Send([.. inputs]);
    }

    private static INPUT KeyInput(ushort key, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        data = { Keyboard = new KEYBDINPUT { wVk = key, dwFlags = flags } },
    };

    private static INPUT MouseInput(uint flags) => new()
    {
        type = INPUT_MOUSE,
        data = { Mouse = new MOUSEINPUT { dwFlags = flags } },
    };

    private static unsafe void Send(params INPUT[] inputs)
    {
        fixed (INPUT* first = inputs)
            SendInput((uint)inputs.Length, first, sizeof(INPUT));
    }
}
