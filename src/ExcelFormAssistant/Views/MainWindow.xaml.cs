using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Views;

public partial class MainWindow : Window
{
    private static readonly Style CellTextStyle = CreateCellTextStyle();

    private readonly MainViewModel _viewModel;

    private static Style CreateCellTextStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(MarginProperty, new Thickness(6, 0, 6, 0)));
        style.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Seal();
        return style;
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DataTableRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            ActivateSelectedRow();
    }

    private void DataTable_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Entrée définit la ligne active au lieu de descendre à la ligne suivante.
        if (e.Key == Key.Enter)
        {
            ActivateSelectedRow();
            e.Handled = true;
        }
    }

    private void ActivateSelectedRow()
    {
        if (_viewModel.ActivateSelectedRowCommand.CanExecute(null))
            _viewModel.ActivateSelectedRowCommand.Execute(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Columns))
            RebuildColumns();
    }

    /// <summary>Les colonnes dépendent du fichier : aucune n'est codée en dur.</summary>
    private void RebuildColumns()
    {
        DataTable.Columns.Clear();
        foreach (var column in _viewModel.Columns)
        {
            DataTable.Columns.Add(new DataGridTextColumn
            {
                // Header en TextBlock : sinon un « _ » dans le nom serait pris pour un raccourci clavier.
                Header = new TextBlock { Text = column.Name },
                Binding = new Binding($"Values[{column.Index}]") { Converter = EmptyToDashConverter.Instance },
                ElementStyle = CellTextStyle,
            });
        }
    }
}
