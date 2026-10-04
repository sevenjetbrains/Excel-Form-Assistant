using System.Windows;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Logique du diagnostic de formulaire, sans UI Automation : rapprochement libellé → colonne,
/// libellé voisin, et sort de chaque champ.
/// </summary>
public sealed class FormDiagnosticTests
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
        new[] { "Nom", "Prénom", "Date naiss.", "Adresse", "Téléphone" }.Select((n, i) => new ExcelColumn(i, n)).ToList();

    private static readonly ExcelRow Benali = new(2, ["BENALI", "Ahmed", "15/05/1993", "", "0550123456"]);

    private static DetectedField Field(string label, LabelSource source = LabelSource.AccessibleName,
        bool password = false, bool readOnly = false, bool hasValue = false) =>
        new("Zone de texte", label, source, new Rect(100, 100, 200, 24), password, readOnly, hasValue);

    [Theory]
    [InlineData("Nom", "Nom")]
    [InlineData("NOM :", "Nom")]
    [InlineData("Prenom *", "Prénom")]
    [InlineData("  prénom  ", "Prénom")]
    [InlineData("Téléphone:", "Téléphone")]
    [InlineData("Date naiss", "Date naiss.")]
    public void Match_IgnoresCaseAccentsAndPunctuation(string label, string expected) =>
        Assert.Equal(expected, FieldMatcher.Match(label, Columns)?.Name);

    [Theory]
    [InlineData("Date de naissance")]  // pas d'équivalence approximative : mieux vaut rien que faux
    [InlineData("Ville")]
    [InlineData("")]
    [InlineData("***")]
    public void Match_ReturnsNullWithoutAnExactName(string label) =>
        Assert.Null(FieldMatcher.Match(label, Columns));

    [Fact]
    public void Normalize_KeepsWordsSeparated() =>
        Assert.Equal("date de naissance", FieldMatcher.Normalize("Date-de   naissance :"));

    [Fact]
    public void NearbyLabel_PrefersTheClosestTextOnTheLeft()
    {
        var field = new Rect(200, 100, 200, 24);
        var texts = new[]
        {
            ("Prénom", new Rect(20, 102, 60, 18)),   // à gauche, plus loin
            ("Nom", new Rect(120, 104, 40, 18)),     // à gauche, le plus proche
            ("Titre de la page", new Rect(0, 0, 300, 30)), // trop haut
        };

        Assert.Equal("Nom", LabelLocator.FindNearest(field, texts));
    }

    [Fact]
    public void NearbyLabel_AcceptsATextJustAbove()
    {
        var field = new Rect(100, 100, 200, 24);
        var texts = new[] { ("Adresse", new Rect(100, 78, 60, 18)) };

        Assert.Equal("Adresse", LabelLocator.FindNearest(field, texts));
    }

    [Fact]
    public void NearbyLabel_IgnoresTextsInsideTheFieldAndParagraphs()
    {
        var field = new Rect(100, 100, 200, 24);
        var texts = new[]
        {
            ("Saisissez votre nom", new Rect(105, 103, 120, 18)), // texte grisé dans le champ
            (new string('x', 120), new Rect(0, 102, 90, 18)),      // paragraphe
            ("Libellé lointain", new Rect(100, 0, 80, 18)),        // trop haut au-dessus
        };

        Assert.Null(LabelLocator.FindNearest(field, texts));
    }

    [Fact]
    public void Build_WouldFillMatchingFieldsWithTheActiveRowValue()
    {
        var diagnostic = FormDiagnostic.Build([Field("Nom :"), Field("Téléphone")], Columns, Benali);

        Assert.All(diagnostic.Rows, r => Assert.Equal(FieldOutcome.WouldFill, r.Outcome));
        Assert.Equal(["BENALI", "0550123456"], diagnostic.Rows.Select(r => r.PreviewValue));
        Assert.Equal(2, diagnostic.WouldFillCount);
    }

    [Fact]
    public void Build_ExplainsEveryFieldThatWouldNotBeFilled()
    {
        var diagnostic = FormDiagnostic.Build(
        [
            Field("Ville"),
            Field("", LabelSource.None),
            Field("Nom", hasValue: true),
            Field("Mot de passe", password: true),
            Field("Prénom", readOnly: true),
        ], Columns, Benali);

        Assert.Equal(
            [FieldOutcome.NoColumn, FieldOutcome.NoLabel, FieldOutcome.AlreadyFilled, FieldOutcome.Skipped, FieldOutcome.Skipped],
            diagnostic.Rows.Select(r => r.Outcome));
        Assert.All(diagnostic.Rows, r => Assert.Equal("—", r.PreviewValue));
        Assert.Equal("Ignoré (mot de passe)", diagnostic.Rows[3].OutcomeText);
    }

    [Fact]
    public void Build_WithoutActiveRow_StillMatchesColumns()
    {
        var diagnostic = FormDiagnostic.Build([Field("Prénom")], Columns, activeRow: null);

        Assert.Equal("Prénom", diagnostic.Rows[0].ColumnName);
        Assert.Equal("—", diagnostic.Rows[0].PreviewValue);
    }

    [Fact]
    public void Report_ContainsLabelsAndColumnsButNoValues()
    {
        var diagnostic = FormDiagnostic.Build([Field("Nom"), Field("Téléphone"), Field("Ville")], Columns, Benali);

        var report = diagnostic.ToReport("Candidats");

        Assert.Contains("libellé : Nom (Nom du champ) | colonne : Nom | Serait rempli", report);
        Assert.Contains("Aucune colonne de ce nom", report);
        Assert.DoesNotContain("BENALI", report);
        Assert.DoesNotContain("0550123456", report);
    }

    [Fact]
    public void FormAnalysisShortcut_HasItsOwnLabelAndCandidates()
    {
        var service = new GlobalShortcutService((_, _) => 1, _ => { },
            GlobalShortcutService.FormAnalysisCandidates, "Analyse de formulaire");

        service.Enable(() => { });

        Assert.Equal("Analyse de formulaire : Ctrl+Maj+F9", service.StatusText);
    }
}
