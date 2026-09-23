using System.Windows.Input;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Le raccourci global est testé sans toucher à Windows : l'enregistrement est remplacé
/// par des délégués qui simulent les combinaisons déjà prises par un autre logiciel.
/// </summary>
public sealed class GlobalShortcutServiceTests
{
    private readonly List<GlobalShortcut> _attempts = [];
    private readonly List<int> _unregistered = [];
    private readonly Dictionary<int, Action> _handlers = [];

    /// <param name="taken">Combinaisons refusées, comme si un autre logiciel les utilisait.</param>
    private GlobalShortcutService CreateService(params GlobalShortcut[] taken)
    {
        int nextId = 1;
        return new GlobalShortcutService(
            register: (shortcut, handler) =>
            {
                _attempts.Add(shortcut);
                if (taken.Contains(shortcut))
                    return null;
                int id = nextId++;
                _handlers[id] = handler;
                return id;
            },
            unregister: id =>
            {
                _unregistered.Add(id);
                _handlers.Remove(id);
            });
    }

    [Fact]
    public void Enable_KeepsThePreferredShortcut()
    {
        var service = CreateService();

        Assert.True(service.Enable(() => { }));

        Assert.Equal(GlobalShortcutService.Candidates[0], service.Active);
        Assert.Single(_attempts); // inutile d'essayer les suivantes
        Assert.Equal("Raccourci global : Ctrl+Maj+E", service.StatusText);
    }

    [Fact]
    public void Enable_FallsBackWhenTheShortcutIsAlreadyTaken()
    {
        var service = CreateService(GlobalShortcutService.Candidates[0], GlobalShortcutService.Candidates[1]);

        Assert.True(service.Enable(() => { }));

        Assert.Equal(GlobalShortcutService.Candidates[2], service.Active);
        Assert.Equal(3, _attempts.Count);
    }

    [Fact]
    public void Enable_WithoutAnyFreeShortcut_ReportsItInsteadOfFailing()
    {
        var service = CreateService([.. GlobalShortcutService.Candidates]);

        Assert.False(service.Enable(() => { }));

        Assert.Null(service.Active);
        Assert.Equal("Raccourci global indisponible (combinaisons déjà prises)", service.StatusText);
    }

    [Fact]
    public void PressingTheShortcut_RunsTheHandler()
    {
        var service = CreateService();
        int calls = 0;
        service.Enable(() => calls++);

        _handlers.Single().Value();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Enable_Twice_ReleasesThePreviousRegistration()
    {
        var service = CreateService();
        service.Enable(() => { });

        service.Enable(() => { });

        Assert.Equal([1], _unregistered);
        Assert.Single(_handlers);
    }

    [Fact]
    public void Disable_ReleasesTheShortcut()
    {
        var service = CreateService();
        service.Enable(() => { });

        service.Disable();

        Assert.Equal([1], _unregistered);
        Assert.Null(service.Active);
    }

    [Fact]
    public void Disable_WithoutEnable_DoesNothing()
    {
        var service = CreateService();

        service.Disable();

        Assert.Empty(_unregistered);
    }

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.E, "Ctrl+Maj+E")]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt, Key.D, "Ctrl+Alt+D")]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.F12, "Ctrl+Maj+F12")]
    [InlineData(ModifierKeys.Windows, Key.D1, "Windows+1")]
    [InlineData(ModifierKeys.None, Key.Escape, "Échap")]
    public void Label_IsReadableInFrench(ModifierKeys modifiers, Key key, string expected) =>
        Assert.Equal(expected, new GlobalShortcut(modifiers, key).Label);
}
