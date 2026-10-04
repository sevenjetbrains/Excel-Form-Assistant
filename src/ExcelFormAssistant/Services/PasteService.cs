using System.Windows;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Services;

/// <summary>Ce qui est arrivé à la valeur choisie dans le menu.</summary>
public enum PasteOutcome
{
    /// <summary>Écrite directement dans le champ, sans frappe ni Ctrl+V.</summary>
    Written,

    /// <summary>Tapée chiffre par chiffre : c'est le cas des sélecteurs de date.</summary>
    Typed,

    /// <summary>Copiée puis collée par un Ctrl+V.</summary>
    Pasted,

    /// <summary>Copiée seulement : la fenêtre visée n'est plus au premier plan, Ctrl+V reste à faire.</summary>
    CopiedOnly,

    /// <summary>Le presse-papiers est resté occupé par un autre logiciel.</summary>
    ClipboardBusy,
}

/// <summary>
/// Remplit le champ du formulaire avec la valeur choisie, de la façon qui marche pour ce
/// champ-là. La valeur est toujours copiée au passage : si le remplissage échoue, Ctrl+V
/// reste possible à la main.
///
/// Trois moyens, du plus sûr au plus général :
/// <list type="number">
/// <item>un sélecteur de date se tape, chiffre par chiffre — l'accessibilité accepte de
/// l'écrire sans rien changer, et un Ctrl+V n'y entre pas ;</item>
/// <item>un champ de saisie ordinaire s'écrit directement par l'accessibilité : la page
/// reçoit son événement de saisie, et aucune touche ne part dans la mauvaise fenêtre ;</item>
/// <item>sinon Ctrl+V, pour tout ce qui n'a pas été reconnu.</item>
/// </list>
/// </summary>
public sealed class PasteService
{
    private readonly Func<string, bool> _copy;
    private readonly Func<IntPtr> _foregroundWindow;
    private readonly Action _sendPaste;
    private readonly Action<string> _sendText;
    private readonly Action<Point> _clickAt;

    public PasteService(ClipboardService clipboard)
        : this(clipboard.SetText, () => GetForegroundWindow(), SendCtrlV, SendText, ClickAt)
    {
    }

    /// <summary>Constructeur de test : presse-papiers, touches et clics remplacés par des délégués.</summary>
    internal PasteService(Func<string, bool> copy, Func<IntPtr> foregroundWindow, Action sendPaste,
        Action<string> sendText, Action<Point> clickAt)
    {
        _copy = copy;
        _foregroundWindow = foregroundWindow;
        _sendPaste = sendPaste;
        _sendText = sendText;
        _clickAt = clickAt;
    }

    /// <param name="field">Champ reconnu sous le curseur ; <see cref="TargetField.Unknown"/> sinon.</param>
    /// <param name="target">Fenêtre visée, relevée à l'ouverture du menu.</param>
    public PasteOutcome Fill(string value, TargetField field, IntPtr target)
    {
        // Toujours copier d'abord : même si le remplissage échoue, Ctrl+V reste possible.
        if (!_copy(value))
            return PasteOutcome.ClipboardBusy;

        // Rien n'est envoyé si l'utilisateur a changé de fenêtre entre-temps.
        if (target == IntPtr.Zero || _foregroundWindow() != target)
            return PasteOutcome.CopiedOnly;

        if (field.Kind == FieldKind.Date && TargetField.DateDigits(value) is string digits)
        {
            // La frappe commence à la case sous le curseur : on vise d'abord le jour.
            if (field.FocusPoint is Point day)
                _clickAt(day);
            _sendText(digits);
            return PasteOutcome.Typed;
        }

        if (field.Write is not null && field.Write(value))
            return PasteOutcome.Written;

        _sendPaste();
        return PasteOutcome.Pasted;
    }

    /// <summary>
    /// Clic gauche à l'endroit du curseur pour donner le focus au champ visé. Le clic droit
    /// ayant été avalé, l'application n'a rien reçu : sans ce clic, le curseur de saisie
    /// resterait là où il était.
    /// </summary>
    public static void FocusUnderCursor() => Send(MouseInput(MOUSEEVENTF_LEFTDOWN), MouseInput(MOUSEEVENTF_LEFTUP));

    /// <summary>Clic à un endroit précis, en remettant ensuite le curseur où il était.</summary>
    private static void ClickAt(Point point)
    {
        GetCursorPos(out var origin);
        SetCursorPos((int)point.X, (int)point.Y);
        FocusUnderCursor();
        SetCursorPos(origin.X, origin.Y);
    }

    /// <summary>Tape le texte touche par touche, indépendamment de la disposition du clavier.</summary>
    private static void SendText(string text)
    {
        ReleaseHeldModifiers();

        var inputs = new List<INPUT>(text.Length * 2);
        foreach (char character in text)
        {
            inputs.Add(UnicodeInput(character, 0));
            inputs.Add(UnicodeInput(character, KEYEVENTF_KEYUP));
        }
        Send([.. inputs]);
    }

    private static void SendCtrlV()
    {
        var inputs = new List<INPUT>(8);
        inputs.AddRange(ReleasedModifiers());
        inputs.Add(KeyInput(VK_CONTROL, 0));
        inputs.Add(KeyInput(VK_V, 0));
        inputs.Add(KeyInput(VK_V, KEYEVENTF_KEYUP));
        inputs.Add(KeyInput(VK_CONTROL, KEYEVENTF_KEYUP));
        Send([.. inputs]);
    }

    private static void ReleaseHeldModifiers()
    {
        var released = ReleasedModifiers().ToArray();
        if (released.Length > 0)
            Send(released);
    }

    /// <summary>
    /// Maj est encore enfoncée juste après « Maj + clic droit » : relâchée d'abord, sinon le
    /// formulaire reçoit Ctrl+Maj+V, ou des chiffres transformés en symboles.
    /// </summary>
    private static IEnumerable<INPUT> ReleasedModifiers()
    {
        foreach (ushort modifier in (ushort[])[VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN])
        {
            if ((GetAsyncKeyState(modifier) & 0x8000) != 0)
                yield return KeyInput(modifier, KEYEVENTF_KEYUP);
        }
    }

    private static INPUT KeyInput(ushort key, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        data = { Keyboard = new KEYBDINPUT { wVk = key, dwFlags = flags } },
    };

    /// <summary>Touche désignée par son caractère : indépendante du clavier AZERTY ou QWERTY.</summary>
    private static INPUT UnicodeInput(char character, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        data = { Keyboard = new KEYBDINPUT { wScan = character, dwFlags = flags | KEYEVENTF_UNICODE } },
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
