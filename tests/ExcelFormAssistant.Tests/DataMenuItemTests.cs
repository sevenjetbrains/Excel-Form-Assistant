using ExcelFormAssistant.Models;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Tests;

public sealed class DataMenuItemTests
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
        new[] { "Nom", "Prénom", "Date", "Adresse", "Téléphone" }.Select((n, i) => new ExcelColumn(i, n)).ToList();

    [Fact]
    public void FromRow_NumbersItemsInColumnOrder()
    {
        var row = new ExcelRow(2, ["BENALI", "Ahmed", "15/05/1993", "Azazga", "0550123456"]);

        var items = DataMenuItem.FromRow(row, Columns);

        Assert.Equal(["1", "2", "3", "4", "5"], items.Select(i => i.Shortcut));
        Assert.Equal(["Nom", "Prénom", "Date", "Adresse", "Téléphone"], items.Select(i => i.ColumnName));
        Assert.Equal(["BENALI", "Ahmed", "15/05/1993", "Azazga", "0550123456"], items.Select(i => i.Value));
        Assert.All(items, i => Assert.True(i.CanCopy));
    }

    [Fact]
    public void EmptyCell_ShowsDashAndCannotBeCopied()
    {
        var row = new ExcelRow(2, ["BENALI", "Ahmed", "15/05/1993", "", "0550123456"]);

        var address = DataMenuItem.FromRow(row, Columns)[3];

        Assert.Equal("—", address.DisplayValue);
        Assert.False(address.CanCopy);
    }

    [Fact]
    public void ItemsBeyondNine_HaveNoShortcut()
    {
        var columns = Enumerable.Range(0, 11).Select(i => new ExcelColumn(i, $"C{i + 1}")).ToList();
        var row = new ExcelRow(2, Enumerable.Range(1, 11).Select(i => $"v{i}").ToList());

        var items = DataMenuItem.FromRow(row, columns);

        Assert.Equal("9", items[8].Shortcut);
        Assert.Equal("", items[9].Shortcut);
        Assert.Equal("", items[10].Shortcut);
    }
}
