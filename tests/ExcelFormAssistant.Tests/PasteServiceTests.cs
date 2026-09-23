using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Le collage est testé sans toucher au clavier ni au presse-papiers réels :
/// la copie, la fenêtre au premier plan et l'envoi du Ctrl+V sont des délégués.
/// </summary>
public sealed class PasteServiceTests
{
    private static readonly IntPtr Form = new(1234);
    private static readonly IntPtr AnotherWindow = new(5678);

    private readonly List<string> _copied = [];
    private int _pastes;

    private PasteService CreateService(IntPtr foreground, bool clipboardAvailable = true) =>
        new(text => { if (!clipboardAvailable) return false; _copied.Add(text); return true; },
            () => foreground,
            () => _pastes++);

    [Fact]
    public void CopyAndPaste_IntoTheTargetWindow_CopiesThenPastes()
    {
        var service = CreateService(foreground: Form);

        Assert.Equal(PasteOutcome.Pasted, service.CopyAndPaste("0550123456", Form));

        Assert.Equal(["0550123456"], _copied);
        Assert.Equal(1, _pastes);
    }

    [Fact]
    public void CopyAndPaste_WhenTheUserChangedWindow_CopiesWithoutTypingAnywhere()
    {
        var service = CreateService(foreground: AnotherWindow);

        Assert.Equal(PasteOutcome.CopiedOnly, service.CopyAndPaste("BENALI", Form));

        Assert.Equal(["BENALI"], _copied); // Ctrl+V reste possible à la main
        Assert.Equal(0, _pastes);
    }

    [Fact]
    public void CopyAndPaste_WithoutAnyTargetWindow_CopiesOnly()
    {
        var service = CreateService(foreground: Form);

        Assert.Equal(PasteOutcome.CopiedOnly, service.CopyAndPaste("BENALI", IntPtr.Zero));

        Assert.Equal(0, _pastes);
    }

    [Fact]
    public void CopyAndPaste_WhenTheClipboardIsBusy_PastesNothing()
    {
        var service = CreateService(foreground: Form, clipboardAvailable: false);

        Assert.Equal(PasteOutcome.ClipboardBusy, service.CopyAndPaste("BENALI", Form));

        Assert.Empty(_copied);
        Assert.Equal(0, _pastes);
    }
}
