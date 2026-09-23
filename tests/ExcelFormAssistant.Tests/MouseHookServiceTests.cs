using ExcelFormAssistant.Services;
using static ExcelFormAssistant.Services.NativeMethods;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Décision du hook souris, vérifiée sans poser de hook global : les tests ne doivent
/// pas intercepter les clics de la machine.
/// </summary>
public sealed class MouseHookServiceTests
{
    private int _triggers;
    private bool _shiftDown;

    private MouseHookService CreateService() =>
        new(() => _triggers++, () => _shiftDown, install: false);

    [Fact]
    public void ShiftRightClick_SwallowsTheClickAndOpensTheMenuOnRelease()
    {
        var hook = CreateService();
        _shiftDown = true;

        Assert.True(hook.ShouldSwallow(WM_RBUTTONDOWN));
        Assert.Equal(0, _triggers); // rien à l'enfoncement, comme tout menu contextuel
        Assert.True(hook.ShouldSwallow(WM_RBUTTONUP));

        Assert.Equal(1, _triggers);
    }

    [Fact]
    public void RightClickAlone_IsLeftUntouched()
    {
        var hook = CreateService();

        Assert.False(hook.ShouldSwallow(WM_RBUTTONDOWN));
        Assert.False(hook.ShouldSwallow(WM_RBUTTONUP));

        Assert.Equal(0, _triggers); // le menu contextuel habituel de l'application s'ouvre
    }

    [Fact]
    public void ShiftReleasedBeforeTheButton_StillOpensTheMenu()
    {
        var hook = CreateService();
        _shiftDown = true;
        Assert.True(hook.ShouldSwallow(WM_RBUTTONDOWN));

        // Maj relâchée pendant le clic : le relâchement du bouton est quand même avalé,
        // sinon l'application verrait un bouton droit qui se relève sans avoir été enfoncé.
        _shiftDown = false;

        Assert.True(hook.ShouldSwallow(WM_RBUTTONUP));
        Assert.Equal(1, _triggers);
    }

    [Fact]
    public void ShiftPressedDuringAnOrdinaryClick_ChangesNothing()
    {
        var hook = CreateService();
        Assert.False(hook.ShouldSwallow(WM_RBUTTONDOWN));

        _shiftDown = true;

        Assert.False(hook.ShouldSwallow(WM_RBUTTONUP));
        Assert.Equal(0, _triggers);
    }

    [Fact]
    public void OtherMouseMessages_AreLeftUntouched()
    {
        var hook = CreateService();
        _shiftDown = true;

        Assert.False(hook.ShouldSwallow(0x0201)); // WM_LBUTTONDOWN
        Assert.False(hook.ShouldSwallow(0x0200)); // WM_MOUSEMOVE
        Assert.Equal(0, _triggers);
    }
}
