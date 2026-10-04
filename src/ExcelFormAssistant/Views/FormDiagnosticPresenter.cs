using System.Windows;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Analyse de formulaire (diagnostic) : tracé de la zone, lecture des champs par
/// UI Automation, puis tableau des résultats et cadres à l'écran. Rien n'est rempli.
/// </summary>
public sealed class FormDiagnosticPresenter(MainViewModel viewModel, FormFieldScanner scanner, ClipboardService clipboard)
{
    private bool _busy;
    private FormDiagnosticWindow? _results;

    public async void Start()
    {
        if (_busy)
            return;
        _busy = true;
        try
        {
            _results?.Close();

            // La fenêtre principale ne doit pas cacher le formulaire pendant le tracé.
            var main = Application.Current.MainWindow;
            if (main is { IsActive: true, WindowState: not WindowState.Minimized })
                main.WindowState = WindowState.Minimized;

            if (ZoneSelectionWindow.Select() is not Rect zone)
                return;

            IReadOnlyList<DetectedField> fields;
            try
            {
                // UI Automation hors du thread d'interface (voir FormFieldScanner).
                fields = await Task.Run(() => scanner.Scan(zone));
            }
            catch (Exception ex) // application cible fermée, accès refusé…
            {
                MessageBox.Show($"L'analyse de la zone a échoué :\n{ex.Message}", "Excel Form Assistant",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var diagnostic = FormDiagnostic.Build(fields, viewModel.Columns, viewModel.ActiveRow);
            _results = new FormDiagnosticWindow(diagnostic, Context(), viewModel.SelectedSheet, clipboard, Start);
            _results.Closed += (_, _) => _results = null;
            _results.Show();
        }
        finally
        {
            _busy = false;
        }
    }

    private string Context() => (viewModel.FilePath, viewModel.ActiveRow) switch
    {
        (null, _) => "Aucun fichier Excel ouvert : les libellés sont lus, mais il n'y a aucune colonne à comparer.",
        (_, null) => $"Colonnes de la feuille « {viewModel.SelectedSheet} ». Aucune ligne active : la colonne "
                     + "Valeur montre « — ».",
        (_, var row) => $"Colonnes de la feuille « {viewModel.SelectedSheet} », ligne active : {row.Label}.",
    };
}
