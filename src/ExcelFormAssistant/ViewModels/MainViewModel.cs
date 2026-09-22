using System.IO;
using System.Windows.Input;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ExcelService _excelService;
    private readonly Func<string?> _pickFile;
    private readonly Action<string> _showError;

    private string? _filePath;
    private IReadOnlyList<ExcelColumn> _columns = [];
    private IReadOnlyList<ExcelRow> _rows = [];
    private string _statusText = "Aucun fichier ouvert.";

    /// <param name="pickFile">Demande un fichier .xlsx à l'utilisateur ; null si annulé.</param>
    /// <param name="showError">Affiche un message d'erreur à l'utilisateur.</param>
    public MainViewModel(ExcelService excelService, Func<string?> pickFile, Action<string> showError)
    {
        _excelService = excelService;
        _pickFile = pickFile;
        _showError = showError;
        OpenCommand = new RelayCommand(Open);
    }

    public ICommand OpenCommand { get; }

    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
                OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string WindowTitle => FilePath is null
        ? "Excel Form Assistant"
        : $"{Path.GetFileName(FilePath)} – Excel Form Assistant";

    public IReadOnlyList<ExcelColumn> Columns
    {
        get => _columns;
        private set => SetProperty(ref _columns, value);
    }

    public IReadOnlyList<ExcelRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private void Open()
    {
        var path = _pickFile();
        if (path is not null)
            LoadFile(path);
    }

    public void LoadFile(string path)
    {
        SheetData sheet;
        try
        {
            sheet = _excelService.LoadSheet(path);
        }
        catch (Exception ex) // fichier verrouillé, corrompu, pas un vrai .xlsx…
        {
            _showError($"Impossible d'ouvrir le fichier :\n{path}\n\n{ex.Message}");
            return;
        }

        FilePath = path;
        Columns = sheet.Columns;
        Rows = sheet.Rows;
        StatusText = $"Feuille « {sheet.SheetName} » : {sheet.Rows.Count} ligne(s).";
    }
}
