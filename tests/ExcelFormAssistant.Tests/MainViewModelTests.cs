using System.IO;
using ExcelFormAssistant.Services;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Tests;

public sealed class MainViewModelTests
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "exemple.xlsx");

    private readonly List<string> _errors = [];

    private MainViewModel CreateViewModel() => new(new ExcelService(), pickFile: () => null, showError: _errors.Add);

    [Fact]
    public void LoadFile_ListsSheetsAndSelectsFirst()
    {
        var vm = CreateViewModel();

        Assert.True(vm.LoadFile(SamplePath));

        Assert.Equal(["Candidats", "Autre feuille"], vm.SheetNames);
        Assert.Equal("Candidats", vm.SelectedSheet);
        Assert.Equal("Nom", vm.Columns[0].Name);
        Assert.Equal(2, vm.Rows.Count);
    }

    [Fact]
    public void ChangingSheet_LoadsItsColumnsAndRows()
    {
        var vm = CreateViewModel();
        vm.LoadFile(SamplePath);

        vm.SelectedSheet = "Autre feuille";

        Assert.Equal(["Ville", "Code postal"], vm.Columns.Select(c => c.Name));
        Assert.Equal(["Azazga", "15300"], vm.Rows.Single().Values);
        Assert.Empty(_errors);
    }

    [Fact]
    public void ActivatingSelectedRow_SetsActiveRow()
    {
        var vm = CreateViewModel();
        vm.LoadFile(SamplePath);
        Assert.Null(vm.ActiveRow);
        Assert.False(vm.ActivateSelectedRowCommand.CanExecute(null));

        vm.SelectedRow = vm.Rows[0];
        vm.ActivateSelectedRowCommand.Execute(null);

        Assert.Same(vm.Rows[0], vm.ActiveRow);
        Assert.Equal("Ligne active : BENALI Ahmed", vm.ActiveRowText);
    }

    [Fact]
    public void SelectingAnotherRow_DoesNotChangeActiveRowUntilActivated()
    {
        var vm = CreateViewModel();
        vm.LoadFile(SamplePath);
        vm.SelectedRow = vm.Rows[0];
        vm.ActivateSelectedRowCommand.Execute(null);

        vm.SelectedRow = vm.Rows[1];

        Assert.Equal("BENALI Ahmed", vm.ActiveRow?.Label);
    }

    [Fact]
    public void ChangingSheet_ClearsActiveRow()
    {
        var vm = CreateViewModel();
        vm.LoadFile(SamplePath);
        vm.SelectedRow = vm.Rows[0];
        vm.ActivateSelectedRowCommand.Execute(null);

        vm.SelectedSheet = "Autre feuille";

        Assert.Null(vm.ActiveRow);
    }

    [Fact]
    public void LoadFile_WithUnknownSheet_FallsBackToFirst()
    {
        var vm = CreateViewModel();

        vm.LoadFile(SamplePath, "Feuille supprimée");

        Assert.Equal("Candidats", vm.SelectedSheet);
    }

    [Fact]
    public void LoadFile_InvalidFile_ShowsErrorAndKeepsPreviousData()
    {
        var vm = CreateViewModel();
        vm.LoadFile(SamplePath);
        var bogus = Path.Combine(Path.GetTempPath(), $"efa-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(bogus, "pas un classeur");

        try
        {
            Assert.False(vm.LoadFile(bogus));
        }
        finally
        {
            File.Delete(bogus);
        }

        Assert.Single(_errors);
        Assert.Equal(SamplePath, vm.FilePath);
        Assert.Equal(2, vm.Rows.Count);
    }
}
