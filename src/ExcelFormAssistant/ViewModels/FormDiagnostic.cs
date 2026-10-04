using System.Text;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.ViewModels;

/// <summary>Ce que deviendrait un champ si le remplissage automatique existait.</summary>
public enum FieldOutcome
{
    /// <summary>Libellé reconnu, colonne trouvée : le champ serait rempli.</summary>
    WouldFill,

    /// <summary>Libellé trouvé, mais aucune colonne ne porte ce nom.</summary>
    NoColumn,

    /// <summary>Aucun libellé trouvé.</summary>
    NoLabel,

    /// <summary>Déjà rempli : jamais écrasé.</summary>
    AlreadyFilled,

    /// <summary>Mot de passe ou champ en lecture seule : jamais touché.</summary>
    Skipped,
}

/// <summary>Une ligne du rapport de diagnostic.</summary>
public sealed record FieldDiagnosticRow(
    int Number,
    DetectedField Field,
    ExcelColumn? Column,
    string PreviewValue,
    FieldOutcome Outcome)
{
    public string Kind => Field.Kind;

    public string Label => Field.LabelSource == LabelSource.None ? "(aucun)" : Field.Label;

    public string SourceText => Field.LabelSource switch
    {
        LabelSource.AccessibleName => "Nom du champ",
        LabelSource.LabeledBy => "Libellé lié",
        LabelSource.HelpText => "Texte d'aide",
        LabelSource.NearbyText => "Texte voisin",
        _ => "—",
    };

    public string ColumnName => Column?.Name ?? "—";

    public string OutcomeText => Outcome switch
    {
        FieldOutcome.WouldFill => "Serait rempli",
        FieldOutcome.NoColumn => "Aucune colonne de ce nom",
        FieldOutcome.NoLabel => "Libellé introuvable",
        FieldOutcome.AlreadyFilled => "Déjà rempli, pas modifié",
        _ => Field.IsPassword ? "Ignoré (mot de passe)" : "Ignoré (lecture seule)",
    };
}

/// <summary>
/// Diagnostic d'une zone de formulaire : pour chaque champ trouvé, son libellé, la colonne
/// Excel qui lui correspondrait, et ce qui lui arriverait. Rien n'est rempli.
/// </summary>
public sealed class FormDiagnostic
{
    private FormDiagnostic(IReadOnlyList<FieldDiagnosticRow> rows) => Rows = rows;

    public IReadOnlyList<FieldDiagnosticRow> Rows { get; }

    public int WouldFillCount => Rows.Count(r => r.Outcome == FieldOutcome.WouldFill);

    public string Summary => Rows.Count == 0
        ? "Aucun champ trouvé dans la zone."
        : $"{Rows.Count} champ(s) trouvé(s) · {WouldFillCount} serai(en)t rempli(s) · "
          + $"{Rows.Count(r => r.Outcome == FieldOutcome.NoColumn)} sans colonne · "
          + $"{Rows.Count(r => r.Outcome == FieldOutcome.NoLabel)} sans libellé";

    /// <param name="activeRow">Ligne active, pour montrer la valeur qui serait collée ; null si aucune.</param>
    public static FormDiagnostic Build(IReadOnlyList<DetectedField> fields, IReadOnlyList<ExcelColumn> columns,
        ExcelRow? activeRow)
    {
        var rows = new List<FieldDiagnosticRow>(fields.Count);
        foreach (var field in fields)
        {
            var column = field.LabelSource == LabelSource.None ? null : FieldMatcher.Match(field.Label, columns);
            var outcome = field switch
            {
                { IsPassword: true } or { IsReadOnly: true } => FieldOutcome.Skipped,
                { LabelSource: LabelSource.None } => FieldOutcome.NoLabel,
                _ when column is null => FieldOutcome.NoColumn,
                { HasValue: true } => FieldOutcome.AlreadyFilled,
                _ => FieldOutcome.WouldFill,
            };

            string preview = column is not null && activeRow is not null && outcome == FieldOutcome.WouldFill
                ? DisplayValue(activeRow.Values[column.Index])
                : "—";
            rows.Add(new FieldDiagnosticRow(rows.Count + 1, field, column, preview, outcome));
        }
        return new FormDiagnostic(rows);
    }

    /// <summary>
    /// Rapport texte à coller dans un message. Il décrit les champs et les colonnes,
    /// mais ne contient aucune valeur : ni celles du fichier Excel, ni celles du formulaire.
    /// </summary>
    public string ToReport(string? sheetName)
    {
        var report = new StringBuilder();
        report.AppendLine("Diagnostic de formulaire — Excel Form Assistant");
        if (sheetName is not null)
            report.AppendLine($"Feuille : {sheetName}");
        report.AppendLine(Summary);
        report.AppendLine();
        foreach (var row in Rows)
        {
            report.AppendLine($"{row.Number}. {row.Kind} | libellé : {row.Label} ({row.SourceText}) | "
                + $"colonne : {row.ColumnName} | {row.OutcomeText}");
        }
        return report.ToString();
    }

    private static string DisplayValue(string value) => value.Length == 0 ? ExcelRow.EmptyDisplay : value;
}
