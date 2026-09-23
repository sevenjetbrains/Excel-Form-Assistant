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
    private IReadOnlyList<ExcelRow> _allRows = [];
    private IReadOnlyList<ExcelRow> _rows = [];
    private string _searchText = string.Empty;
    private string? _reloadNote;
    private ExcelRow? _selectedRow;
    private ExcelRow? _activeRow;
    private string _statusText = "Aucun fichier ouvert.";
    private string _shortcutText = string.Empty;

    /// <param name="pickFile">Demande un fichier .xlsx à l'utilisateur ; null si annulé.</param>
    /// <param name="showError">Affiche un message d'erreur à l'utilisateur.</param>
    public MainViewModel(ExcelService excelService, Func<string?> pickFile, Action<string> showError)
    {
        _excelService = excelService;
        _pickFile = pickFile;
        _showError = showError;
        OpenCommand = new RelayCommand(Open);
        ActivateSelectedRowCommand = new RelayCommand(ActivateSelectedRow, () => SelectedRow is not null);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => IsSearching);
        ReloadCommand = new RelayCommand(() => Reload(), () => FilePath is not null);
    }

    public ICommand OpenCommand { get; }

    /// <summary>Double-clic ou Entrée sur une ligne du tableau.</summary>
    public ICommand ActivateSelectedRowCommand { get; }

    /// <summary>Vide la recherche et réaffiche toutes les lignes.</summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>Relit le fichier pour prendre en compte ce qui a été modifié dans Excel (F5).</summary>
    public ICommand ReloadCommand { get; }

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

    /// <summary>Lignes affichées : celles de la feuille, filtrées par la recherche.</summary>
    public IReadOnlyList<ExcelRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>Texte de la zone de recherche ; le tableau se filtre à chaque frappe.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value ?? string.Empty))
                return;
            OnPropertyChanged(nameof(IsSearching));
            ApplySearch();
        }
    }

    public bool IsSearching => SearchText.Length > 0;

    /// <summary>Ligne surlignée dans le tableau (simple sélection).</summary>
    public ExcelRow? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    /// <summary>Ligne dont les données sont proposées dans le menu « Données Excel ».</summary>
    public ExcelRow? ActiveRow
    {
        get => _activeRow;
        private set
        {
            if (SetProperty(ref _activeRow, value))
                OnPropertyChanged(nameof(ActiveRowText));
        }
    }

    public string ActiveRowText => ActiveRow is null
        ? "Aucune ligne active (double-clic ou Entrée sur une ligne)"
        : $"Ligne active : {ActiveRow.Label}";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Libellé du raccourci global, renseigné au démarrage par l'application.</summary>
    public string ShortcutText
    {
        get => _shortcutText;
        set => SetProperty(ref _shortcutText, value);
    }

    private void Open()
    {
        var path = _pickFile();
        if (path is not null)
            LoadFile(path);
    }

    private void ActivateSelectedRow()
    {
        if (SelectedRow is not null)
            ActiveRow = SelectedRow;
    }

    /// <summary>Charge une feuille du fichier (la première si <paramref name="sheetName"/> est null ou n'existe plus).</summary>
    public bool LoadFile(string path, string? sheetName = null) => Load(path, sheetName, keepRows: false);

    /// <summary>
    /// Relit le fichier et la feuille en cours. La ligne active et la ligne surlignée sont
    /// retrouvées par leur numéro de ligne Excel : elles portent alors les valeurs à jour.
    /// </summary>
    public bool Reload()
    {
        if (FilePath is null)
            return false;

        return Load(FilePath, SelectedSheet, keepRows: true);
    }

    private bool Load(string path, string? sheetName, bool keepRows)
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

        // Repères à retrouver après la relecture (les lignes sont de nouveaux objets).
        int? activeRowNumber = keepRows ? ActiveRow?.RowNumber : null;
        int? selectedRowNumber = keepRows ? SelectedRow?.RowNumber : null;

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
        _allRows = sheet.Rows;
        _reloadNote = keepRows ? $"Rechargé à {DateTime.Now:HH:mm:ss}." : null;

        // Sans conservation : une ligne d'une autre feuille / d'un autre fichier n'a plus de sens.
        ActiveRow = FindByRowNumber(activeRowNumber);
        ApplySearch(); // une recherche en cours reste valable sur la nouvelle feuille

        // Après Rows : le tableau ne peut surligner qu'une ligne qu'il affiche.
        var selected = FindByRowNumber(selectedRowNumber);
        SelectedRow = selected is not null && Rows.Contains(selected) ? selected : null;
        return true;
    }

    private ExcelRow? FindByRowNumber(int? rowNumber) =>
        rowNumber is int number ? _allRows.FirstOrDefault(row => row.RowNumber == number) : null;

    private void ApplySearch()
    {
        Rows = RowSearch.Filter(_allRows, SearchText);
        StatusText = (FilePath, IsSearching) switch
        {
            (null, _) => "Aucun fichier ouvert.",
            (_, false) => $"Feuille « {SelectedSheet} » : {_allRows.Count} ligne(s).",
            _ => $"Feuille « {SelectedSheet} » : {Rows.Count} ligne(s) trouvée(s) sur {_allRows.Count}.",
        };
        if (_reloadNote is not null)
            StatusText += $" {_reloadNote}";
    }
}
