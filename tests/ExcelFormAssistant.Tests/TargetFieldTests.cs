using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

public sealed class TargetFieldTests
{
    [Fact]
    public void ThreeSpinners_MeanADateField() =>
        // Un <input type="date"> se présente en trois cases : jour, mois, année.
        Assert.Equal(FieldKind.Date, TargetField.KindOf(hasValue: true, isReadOnly: false, spinnerCount: 3));

    [Fact]
    public void ADateField_IsRecognisedEvenWhenItRefusesToBeWritten() =>
        // C'est justement le cas de Chrome : la valeur s'y écrit sans rien changer.
        Assert.Equal(FieldKind.Date, TargetField.KindOf(hasValue: true, isReadOnly: true, spinnerCount: 3));

    [Fact]
    public void AWritableFieldWithoutSpinners_IsAnOrdinaryTextField() =>
        Assert.Equal(FieldKind.Text, TargetField.KindOf(hasValue: true, isReadOnly: false, spinnerCount: 0));

    [Theory]
    [InlineData(false, false)] // pas de valeur à écrire
    [InlineData(true, true)]   // champ en lecture seule
    public void WithoutAnythingWritable_TheFieldStaysUnknown(bool hasValue, bool isReadOnly) =>
        Assert.Equal(FieldKind.Unknown, TargetField.KindOf(hasValue, isReadOnly, spinnerCount: 0));

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
