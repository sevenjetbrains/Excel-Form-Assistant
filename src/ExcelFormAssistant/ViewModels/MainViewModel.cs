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
    private IReadOnlyList<string> _sheetNames = [];
    private string? _selectedSheet;
    private bool _isApplyingSheet;
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

    public IReadOnlyList<string> SheetNames
    {
        get => _sheetNames;
        private set => SetProperty(ref _sheetNames, value);
    }

    /// <summary>Feuille affichée ; la changer depuis la liste déroulante relit le fichier.</summary>
    public string? SelectedSheet
    {
        get => _selectedSheet;
        set
        {
            if (SetProperty(ref _selectedSheet, value) && !_isApplyingSheet && value is not null && FilePath is not null)
                LoadFile(FilePath, value);
        }
    }

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

    /// <summary>Charge une feuille du fichier (la première si <paramref name="sheetName"/> est null ou n'existe plus).</summary>
    public bool LoadFile(string path, string? sheetName = null)
    {
        SheetData sheet;
        try
        {
            sheet = _excelService.LoadSheet(path, sheetName);
        }
        catch (Exception ex) // fichier verrouillé, corrompu, pas un vrai .xlsx…
        {
            _showError($"Impossible d'ouvrir le fichier :\n{path}\n\n{ex.Message}");
            return false;
        }

        FilePath = path;
        SheetNames = sheet.SheetNames;

        // Mettre à jour la liste déroulante sans déclencher une deuxième lecture.
        _isApplyingSheet = true;
        try
        {
            SelectedSheet = sheet.SheetName;
        }
        finally
        {
            _isApplyingSheet = false;
        }

        Columns = sheet.Columns;
        Rows = sheet.Rows;
        StatusText = $"Feuille « {sheet.SheetName} » : {sheet.Rows.Count} ligne(s).";
        return true;
    }
}
