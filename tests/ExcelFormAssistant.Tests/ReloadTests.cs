using System.IO;
using ClosedXML.Excel;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Rechargement : le fichier est modifié entre-temps comme le ferait Excel,
/// sur une copie de l'exemple pour ne jamais toucher au fichier du dépôt.
/// </summary>
public sealed class ReloadTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"efa-{Guid.NewGuid():N}.xlsx");
    private readonly List<string> _errors = [];

    public ReloadTests() =>
        File.Copy(Path.Combine(AppContext.BaseDirectory, "exemple.xlsx"), _path);

    public void Dispose() => File.Delete(_path);

    private MainViewModel CreateLoadedViewModel()
    {
        var vm = new MainViewModel(new ExcelService(), pickFile: () => null, showError: _errors.Add);
        Assert.True(vm.LoadFile(_path));
        return vm;
    }

    /// <summary>Modifie une cellule du fichier, comme un utilisateur dans Excel.</summary>
    private void EditCell(int row, int column, string value)
    {
        using var workbook = new XLWorkbook(_path);
        workbook.Worksheet(1).Cell(row, column).Value = value;
        workbook.Save();
    }

    [Fact]
    public void Reload_ShowsTheNewValuesAndKeepsTheActiveRow()
    {
        var vm = CreateLoadedViewModel();
        vm.SelectedRow = vm.Rows[0]; // ligne Excel 2 : BENALI
        vm.ActivateSelectedRowCommand.Execute(null);
        EditCell(2, 2, "Ahmed Amine");

        Assert.True(vm.Reload());

        Assert.Equal("Ahmed Amine", vm.Rows[0].Values[1]);
        Assert.Equal(2, vm.ActiveRow?.RowNumber);
        Assert.Equal("BENALI Ahmed Amine", vm.ActiveRow?.Label); // le menu proposera la valeur à jour
        Assert.Same(vm.Rows[0], vm.ActiveRow);
        Assert.Same(vm.Rows[0], vm.SelectedRow);
        Assert.Empty(_errors);
    }

    [Fact]
    public void Reload_SeesRowsAddedInExcel()
    {
        var vm = CreateLoadedViewModel();
        Assert.Equal(2, vm.Rows.Count);
        EditCell(4, 1, "MOKRANE");

        vm.Reload();

        Assert.Equal(3, vm.Rows.Count);
        Assert.Contains("3 ligne(s)", vm.StatusText);
        Assert.Contains("Rechargé à", vm.StatusText);
    }

    [Fact]
    public void Reload_KeepsTheSheetAndTheSearch()
    {
        var vm = CreateLoadedViewModel();
        vm.SelectedSheet = "Autre feuille";
        vm.SearchText = "azazga";

        vm.Reload();

        Assert.Equal("Autre feuille", vm.SelectedSheet);
        Assert.Equal("azazga", vm.SearchText);
        Assert.Equal(["Azazga", "15300"], vm.Rows.Single().Values);
    }

    [Fact]
    public void Reload_WhenTheActiveRowWasDeleted_ForgetsIt()
    {
        var vm = CreateLoadedViewModel();
        vm.SelectedRow = vm.Rows[1]; // ligne Excel 3 : AMEUR
        vm.ActivateSelectedRowCommand.Execute(null);

        using (var workbook = new XLWorkbook(_path))
        {
            workbook.Worksheet(1).Row(3).Delete();
            workbook.Save();
        }
        vm.Reload();

        Assert.Null(vm.ActiveRow);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public void Reload_WhenTheFileIsGone_KeepsWhatIsDisplayed()
    {
        var vm = CreateLoadedViewModel();
        File.Delete(_path);

        Assert.False(vm.Reload());

        Assert.Single(_errors);
        Assert.Equal(2, vm.Rows.Count);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "exemple.xlsx"), _path); // pour Dispose
    }

    [Fact]
    public void Reload_WithoutAnyFileOpen_DoesNothing()
    {
        var vm = new MainViewModel(new ExcelService(), () => null, _errors.Add);

        Assert.False(vm.Reload());
        Assert.False(vm.ReloadCommand.CanExecute(null));
        Assert.Empty(_errors);
    }
}
