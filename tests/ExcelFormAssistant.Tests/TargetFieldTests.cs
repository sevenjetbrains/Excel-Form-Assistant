using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

public sealed class TargetFieldTests
{
    [Fact]
    public void ThreeSpinners_MeanADateField() =>
        // Un <input type="date"> se présente en trois cases : jour, mois, année.
        Assert.Equal(FieldKind.Date, TargetField.KindOf(true, isReadOnly: false, spinnerCount: 3, isList: false));

    [Fact]
    public void ADateField_IsRecognisedEvenWhenItRefusesToBeWritten() =>
        // C'est justement le cas de Chrome : la valeur s'y écrit sans rien changer.
        Assert.Equal(FieldKind.Date, TargetField.KindOf(true, isReadOnly: true, spinnerCount: 3, isList: false));

    [Fact]
    public void ADropDownList_IsLeftAlone() =>
        // L'application n'y touche pas : il restera le presse-papiers et Ctrl+V.
        Assert.Equal(FieldKind.Unknown, TargetField.KindOf(true, isReadOnly: false, spinnerCount: 0, isList: true));

    [Fact]
    public void ATextFieldWithBrowserSuggestions_StaysATextField() =>
        // Il sait s'ouvrir pour montrer les suggestions de saisie automatique du navigateur,
        // mais il n'est pas une liste pour autant : la valeur doit y être écrite.
        Assert.Equal(FieldKind.Text, TargetField.KindOf(true, isReadOnly: false, spinnerCount: 0, isList: false));

    [Fact]
    public void AWritableFieldWithoutSpinners_IsAnOrdinaryTextField() =>
        Assert.Equal(FieldKind.Text, TargetField.KindOf(true, isReadOnly: false, spinnerCount: 0, isList: false));

    [Theory]
    [InlineData(false, false)] // pas de valeur à écrire
    [InlineData(true, true)]   // champ en lecture seule
    public void WithoutAnythingWritable_TheFieldStaysUnknown(bool hasValue, bool isReadOnly) =>
        Assert.Equal(FieldKind.Unknown, TargetField.KindOf(hasValue, isReadOnly, spinnerCount: 0, isList: false));

    [Fact]
    public void AWriteThatShowsTheValue_IsASuccess() =>
        Assert.Equal(FillResult.Done, TargetField.Verdict(before: "", after: "سمير", text: "سمير"));

    [Fact]
    public void AWriteThatChangedNothing_IsAFailure() =>
        // Rien n'a bougé : le Ctrl+V de repli ne risque pas de doubler la valeur.
        Assert.Equal(FillResult.Failed, TargetField.Verdict(before: "", after: "", text: "سمير"));

    [Theory]
    [InlineData("", "SMIR")]        // la page a reformaté la valeur
    [InlineData("", "سمي")]         // la page n'a pas fini de se mettre à jour
    [InlineData("ancien", "سمير x")]
    public void AWriteThatChangedSomethingElse_CountsAsDoneRatherThanBeingPastedOver(string before, string after) =>
        // C'est le bug du champ rempli en double : coller par-dessus ajouterait la valeur une
        // seconde fois. Mieux vaut considérer que l'écriture a pris.
        Assert.Equal(FillResult.Done, TargetField.Verdict(before, after, "سمير"));

    [Fact]
    public void AWriteIntoAFieldThatCannotBeRead_IsAFailure() =>
        Assert.Equal(FillResult.Failed, TargetField.Verdict(before: null, after: null, text: "سمير"));


    [Theory]
    [InlineData("15/05/1993", "15051993")]
    [InlineData("01/01/2000", "01012000")]
    [InlineData(" 15/05/1993 ", "15051993")] // espaces autour
    public void ADate_BecomesTheDigitsToType(string value, string expected) =>
        Assert.Equal(expected, TargetField.DateDigits(value));

    [Theory]
    [InlineData("BENALI")]
    [InlineData("0550123456")]
    [InlineData("")]
    [InlineData("1993-05-15")]   // format ISO : pas celui qu'affiche l'application
    [InlineData("32/05/1993")]   // jour impossible
    [InlineData("15/05/93")]     // année sur deux chiffres
    public void AnythingElse_GivesNothingToType(string value) =>
        Assert.Null(TargetField.DateDigits(value));
}
