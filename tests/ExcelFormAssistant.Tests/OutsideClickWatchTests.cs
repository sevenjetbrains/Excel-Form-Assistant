using ExcelFormAssistant.Views;

namespace ExcelFormAssistant.Tests;

public sealed class OutsideClickWatchTests
{
    [Fact]
    public void TheClickThatOpenedTheMenu_DoesNotCloseItImmediately()
    {
        var watch = new OutsideClickWatch();

        // Premier passage : le bouton est encore vu enfoncé, et le curseur est à côté du menu.
        Assert.False(watch.ShouldDismiss(foregroundChanged: false, anyButtonDown: true, cursorInsideMenu: false));
    }

    [Fact]
    public void OnceEveryButtonIsReleased_AClickOutsideClosesTheMenu()
    {
        var watch = new OutsideClickWatch();
        watch.ShouldDismiss(false, anyButtonDown: true, cursorInsideMenu: false);

        Assert.False(watch.ShouldDismiss(false, anyButtonDown: false, cursorInsideMenu: false)); // relâché : on arme
        Assert.True(watch.ShouldDismiss(false, anyButtonDown: true, cursorInsideMenu: false));
    }

    [Fact]
    public void AClickInsideTheMenu_NeverClosesIt()
    {
        var watch = new OutsideClickWatch();
        watch.ShouldDismiss(false, false, false); // armé

        Assert.False(watch.ShouldDismiss(false, anyButtonDown: true, cursorInsideMenu: true));
    }

    [Fact]
    public void ChangingWindow_ClosesTheMenuEvenBeforeAnyButtonIsReleased()
    {
        var watch = new OutsideClickWatch();

        Assert.True(watch.ShouldDismiss(foregroundChanged: true, anyButtonDown: true, cursorInsideMenu: false));
    }

    [Fact]
    public void WithoutAnyClick_TheMenuStaysOpen()
    {
        var watch = new OutsideClickWatch();

        for (int tick = 0; tick < 10; tick++)
            Assert.False(watch.ShouldDismiss(false, anyButtonDown: false, cursorInsideMenu: false));
    }
}
