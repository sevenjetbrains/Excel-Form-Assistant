using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

public sealed class RowSearchTests
{
    private static readonly ExcelRow Benaissa = new(2, ["Benaïssa", "Élodie", "Tizi Ouzou"]);
    private static readonly ExcelRow Ameur = new(3, ["AMEUR", "Karim", "Azazga"]);
    private static readonly IReadOnlyList<ExcelRow> Rows = [Benaissa, Ameur];

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Filter_WithoutSearch_KeepsEveryRow(string? text) =>
        Assert.Same(Rows, RowSearch.Filter(Rows, text));

    [Fact]
    public void Filter_IgnoresCase() =>
        Assert.Equal([Ameur], RowSearch.Filter(Rows, "ameur"));

    [Theory]
    [InlineData("benaissa")]  // tapé sans accent
    [InlineData("BENAÏSSA")]  // tapé avec accent et en majuscules
    [InlineData("elodie")]
    public void Filter_IgnoresAccents(string text) =>
        Assert.Equal([Benaissa], RowSearch.Filter(Rows, text));

    [Fact]
    public void Filter_MatchesInsideAValue() =>
        Assert.Equal([Ameur], RowSearch.Filter(Rows, "zaz"));

    [Theory]
    [InlineData("ameur karim")]      // deux colonnes différentes
    [InlineData("karim ameur")]      // l'ordre n'importe pas
    [InlineData("  ameur   karim ")] // les espaces en trop non plus
    public void Filter_RequiresEveryTermSomewhereInTheRow(string text) =>
        Assert.Equal([Ameur], RowSearch.Filter(Rows, text));

    [Fact]
    public void Filter_WithATermFoundNowhere_KeepsNothing() =>
        Assert.Empty(RowSearch.Filter(Rows, "ameur azazga inconnu"));

    [Fact]
    public void Matches_AnswersForASingleRow()
    {
        Assert.True(RowSearch.Matches(Ameur, "karim"));
        Assert.True(RowSearch.Matches(Ameur, ""));
        Assert.False(RowSearch.Matches(Ameur, "élodie"));
    }
}
