using System.Windows.Input;

namespace ExcelFormAssistant.Services;

/// <summary>Une combinaison de touches et son libellé en français (« Ctrl+Maj+E »).</summary>
public readonly record struct GlobalShortcut(ModifierKeys Modifiers, Key Key)
{
    public string Label
    {
        get
        {
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Maj");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Windows");
            parts.Add(KeyLabel(Key));
            return string.Join("+", parts);
        }
    }

    private static string KeyLabel(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Pavé {key - Key.NumPad0}",
        Key.Escape => "Échap",
        Key.Space => "Espace",
        _ => key.ToString(),
    };
}
