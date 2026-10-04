using System.Windows;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Choix de la façon de remplir le champ, sans toucher au clavier, à la souris
/// ni au presse-papiers réels : tout est injecté.
/// </summary>
public sealed class PasteServiceTests
{
    private static readonly IntPtr Form = new(1234);
    private static readonly IntPtr AnotherWindow = new(5678);

    private readonly List<string> _copied = [];
    private readonly List<string> _typed = [];
    private readonly List<Point> _clicks = [];
    private readonly List<string> _written = [];
    private int _pastes;

    private PasteService CreateService(IntPtr foreground, bool clipboardAvailable = true) =>
        new(text => { if (!clipboardAvailable) return false; _copied.Add(text); return true; },
            () => foreground,
            () => _pastes++,
            _typed.Add,
            _clicks.Add);

    /// <summary>Champ de saisie ordinaire : l'écriture directe réussit.</summary>
    private TargetField TextField(FillResult result = FillResult.Done) =>
        new(FieldKind.Text, Fill: text => { _written.Add(text); return result; });

    /// <summary>Liste déroulante : le choix de l'option réussit, ou reste à trancher.</summary>
    private TargetField ListField(FillResult result = FillResult.Done) =>
        new(FieldKind.List, Fill: text => { _written.Add(text); return result; });

    private static TargetField DateField(Point? day = null) =>
        new(FieldKind.Date, FocusPoint: day);

    [Fact]
    public void InATextField_TheValueIsWrittenDirectly()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Written, service.Fill("BENALI", TextField(), Form));

        Assert.Equal(["BENALI"], _written);
        Assert.Equal(["BENALI"], _copied); // copiée quand même, pour un Ctrl+V à la main
        Assert.Equal(0, _pastes);
        Assert.Empty(_typed);
    }

    [Fact]
    public void InADateField_TheDigitsAreTypedAfterClickingTheDayBox()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Typed, service.Fill("15/05/1993", DateField(new Point(100, 50)), Form));

        Assert.Equal(["15051993"], _typed); // sans les barres obliques
        Assert.Equal([new Point(100, 50)], _clicks);
        Assert.Equal(0, _pastes);
    }

    [Fact]
    public void InADateField_WithoutAKnownBox_TypesWithoutClicking()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Typed, service.Fill("15/05/1993", DateField(), Form));

        Assert.Equal(["15051993"], _typed);
        Assert.Empty(_clicks);
    }

    [Fact]
    public void InADateField_AValueThatIsNotADate_FallsBackToPasting()
    {
        var service = CreateService(Form);

        // On ne tape jamais au hasard dans un sélecteur de date.
        Assert.Equal(PasteOutcome.Pasted, service.Fill("BENALI", DateField(new Point(1, 2)), Form));

        Assert.Empty(_typed);
        Assert.Empty(_clicks);
        Assert.Equal(1, _pastes);
    }

    [Fact]
    public void WhenTheDirectWriteDoesNotTake_ItFallsBackToPasting()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Pasted, service.Fill("BENALI", TextField(FillResult.Failed), Form));

        Assert.Equal(["BENALI"], _written);
        Assert.Equal(1, _pastes);
    }

    [Fact]
    public void InAList_TheOptionIsChosen()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Chosen, service.Fill("Ingénieur d'État", ListField(), Form));

        Assert.Equal(["Ingénieur d'État"], _written);
        Assert.Equal(0, _pastes);
        Assert.Empty(_typed);
    }

    [Fact]
    public void InAList_WithSeveralOptionsLeft_NothingIsChosenInTheUserPlace()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Narrowed, service.Fill("ingénieur", ListField(FillResult.Narrowed), Form));

        // La liste reste ouverte et filtrée : aucun collage ni frappe derrière.
        Assert.Equal(0, _pastes);
        Assert.Empty(_typed);
    }

    [Fact]
    public void InAList_ThatRefusesEverything_ItFallsBackToPasting()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Pasted, service.Fill("BENALI", ListField(FillResult.Failed), Form));

        Assert.Equal(1, _pastes);
    }

    [Fact]
    public void InAnUnrecognisedField_ItPastes()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.Pasted, service.Fill("BENALI", TargetField.Unknown, Form));

        Assert.Equal(1, _pastes);
    }

    [Fact]
    public void WhenTheUserChangedWindow_NothingIsSentAnywhere()
    {
        var service = CreateService(AnotherWindow);

        Assert.Equal(PasteOutcome.CopiedOnly, service.Fill("15/05/1993", DateField(new Point(1, 2)), Form));

        Assert.Equal(["15/05/1993"], _copied);
        Assert.Empty(_typed);
        Assert.Empty(_clicks);
        Assert.Empty(_written);
        Assert.Equal(0, _pastes);
    }

    [Fact]
    public void WithoutAnyTargetWindow_ItOnlyCopies()
    {
        var service = CreateService(Form);

        Assert.Equal(PasteOutcome.CopiedOnly, service.Fill("BENALI", TextField(), IntPtr.Zero));

        Assert.Empty(_written);
    }

    [Fact]
    public void WhenTheClipboardIsBusy_NothingIsAttempted()
    {
        var service = CreateService(Form, clipboardAvailable: false);

        Assert.Equal(PasteOutcome.ClipboardBusy, service.Fill("BENALI", TextField(), Form));

        Assert.Empty(_copied);
        Assert.Empty(_written);
        Assert.Equal(0, _pastes);
    }
}
