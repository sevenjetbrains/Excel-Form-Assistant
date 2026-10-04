using System.Windows;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Views;

/// <summary>Résultat d'une analyse de zone : un tableau des champs trouvés, sans rien remplir.</summary>
public partial class FormDiagnosticWindow : Window
{
    private readonly FormDiagnostic _diagnostic;
    private readonly string? _sheetName;
    private readonly ClipboardService _clipboard;
    private readonly Action _again;
    private readonly FieldHighlightWindow _highlight;

    public FormDiagnosticWindow(FormDiagnostic diagnostic, string context, string? sheetName,
        ClipboardService clipboard, Action again)
    {
        InitializeComponent();
        _diagnostic = diagnostic;
        _sheetName = sheetName;
        _clipboard = clipboard;
        _again = again;

        SummaryText.Text = diagnostic.Summary;
        ContextText.Text = context;
        Table.ItemsSource = diagnostic.Rows;

        _highlight = new FieldHighlightWindow(diagnostic.Rows);
        Loaded += (_, _) => _highlight.ShowOverScreens();
        Closed += (_, _) => _highlight.Close();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        bool copied = _clipboard.SetText(_diagnostic.ToReport(_sheetName));
        CopyButton.Content = copied ? "✓ Rapport copié" : "Presse-papiers occupé, réessayez";
    }

    private void Again_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _again();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
