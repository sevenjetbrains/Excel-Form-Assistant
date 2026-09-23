using System.Windows;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Views;

namespace ExcelFormAssistant.Tests;

public sealed class WindowPlacementTests
{
    /// <summary>Deux écrans 1920x1080 côte à côte, le second à droite.</summary>
    private static readonly Rect TwoScreens = new(0, 0, 3840, 1080);

    /// <summary>Un seul écran : le second a été débranché depuis l'enregistrement.</summary>
    private static readonly Rect OneScreen = new(0, 0, 1920, 1080);

    [Fact]
    public void IsUsable_OnTheSameScreens_AcceptsThePosition() =>
        Assert.True(WindowPlacement.IsUsable(new WindowBounds(2000, 100, 900, 550, false), TwoScreens));

    [Fact]
    public void IsUsable_OnASecondScreenNowUnplugged_RefusesThePosition() =>
        Assert.False(WindowPlacement.IsUsable(new WindowBounds(2000, 100, 900, 550, false), OneScreen));

    [Fact]
    public void IsUsable_PartlyOutsideButStillGrabbable_AcceptsThePosition() =>
        Assert.True(WindowPlacement.IsUsable(new WindowBounds(1800, 1000, 900, 550, false), OneScreen));

    [Theory]
    [InlineData(-1000, 100)] // sortie à gauche
    [InlineData(100, -1000)] // sortie en haut
    [InlineData(100, 1070)]  // il ne reste qu'un liseré en bas
    public void IsUsable_AlmostEntirelyOffScreen_RefusesThePosition(double left, double top) =>
        Assert.False(WindowPlacement.IsUsable(new WindowBounds(left, top, 900, 550, false), OneScreen));

    [Theory]
    [InlineData(900, 0)]      // taille nulle
    [InlineData(900, -550)]   // taille négative (fichier abîmé)
    [InlineData(20, 20)]      // fenêtre inutilisable
    [InlineData(double.NaN, 550)]
    public void IsUsable_WithAnImpossibleSize_RefusesThePosition(double width, double height) =>
        Assert.False(WindowPlacement.IsUsable(new WindowBounds(100, 100, width, height, false), OneScreen));

    [Fact]
    public void IsUsable_WithNothingSaved_RefusesThePosition() =>
        Assert.False(WindowPlacement.IsUsable(null, OneScreen));
}
