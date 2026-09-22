using System.IO;
using System.Security.Cryptography;
using ClosedXML.Excel;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

public sealed class ExcelServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"efa-{Guid.NewGuid():N}.xlsx");
    private readonly ExcelService _service = new();

    public void Dispose() => File.Delete(_path);

    /// <summary>Reproduit le fichier d'exemple du cahier des charges (BENALI, AMEUR).</summary>
    private void CreateSampleFile(Action<IXLWorksheet>? customize = null)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Candidats");
        ws.Cell(1, 1).Value = "Nom";
        ws.Cell(1, 2).Value = "Prénom";
        ws.Cell(1, 3).Value = "Date naiss.";
        ws.Cell(1, 4).Value = "Adresse";
        ws.Cell(1, 5).Value = "Téléphone";

        ws.Cell(2, 1).Value = "BENALI";
        ws.Cell(2, 2).Value = "Ahmed";
        ws.Cell(2, 3).Value = new DateTime(1993, 5, 15);
        ws.Cell(2, 3).Style.NumberFormat.Format = "dd/mm/yyyy";
        ws.Cell(2, 4).Value = "Azazga";
        ws.Cell(2, 5).Value = "0550123456"; // colonne au format Texte
        ws.Cell(2, 5).Style.NumberFormat.Format = "@";

        ws.Cell(3, 1).Value = "AMEUR";
        ws.Cell(3, 2).Value = "Karim";
        ws.Cell(3, 3).Value = new DateTime(1990, 2, 12);
        ws.Cell(3, 3).Style.NumberFormat.Format = "dd/mm/yyyy";
        ws.Cell(3, 4).Value = "Tizi Ouzou";
        ws.Cell(3, 5).Value = "0661234567";
        ws.Cell(3, 5).Style.NumberFormat.Format = "@";

        customize?.Invoke(ws);
        workbook.SaveAs(_path);
    }

    [Fact]
    public void SampleFile_IsReadWithHeadersAndRows()
    {
        CreateSampleFile();

        var sheet = _service.LoadSheet(_path);

        Assert.Equal("Candidats", sheet.SheetName);
        Assert.Equal(["Nom", "Prénom", "Date naiss.", "Adresse", "Téléphone"], sheet.Columns.Select(c => c.Name));
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal(["BENALI", "Ahmed", "15/05/1993", "Azazga", "0550123456"], sheet.Rows[0].Values);
        Assert.Equal(["AMEUR", "Karim", "12/02/1990", "Tizi Ouzou", "0661234567"], sheet.Rows[1].Values);
    }

    [Fact]
    public void ProvidedSampleFile_OpensCorrectly()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "exemple.xlsx");

        Assert.Equal(["Candidats", "Autre feuille"], _service.GetSheetNames(sample));
        var sheet = _service.LoadSheet(sample);
        Assert.Equal(["BENALI", "Ahmed", "15/05/1993", "Azazga", "0550123456"], sheet.Rows[0].Values);
    }

    [Fact]
    public void Phone_InTextColumn_KeepsLeadingZero()
    {
        CreateSampleFile(ws => ws.Cell(2, 5).Value = "001245");

        Assert.Equal("001245", _service.LoadSheet(_path).Rows[0].Values[4]);
    }

    [Theory]
    [InlineData("dd/mm/yyyy")]
    [InlineData("m/d/yyyy")]      // format de date court par défaut (US)
    [InlineData("d-mmm-yy")]
    public void DateCell_IsAlwaysCopiedAsDdMmYyyy_NeverAsSerialNumber(string excelFormat)
    {
        CreateSampleFile(ws => ws.Cell(2, 3).Style.NumberFormat.Format = excelFormat);

        var value = _service.LoadSheet(_path).Rows[0].Values[2];

        Assert.Equal("15/05/1993", value);
        Assert.NotEqual("34104", value);
    }

    [Fact]
    public void DateTypedAsText_IsCopiedUnchanged()
    {
        CreateSampleFile(ws => ws.Cell(2, 3).Value = "15/05/1993");

        Assert.Equal("15/05/1993", _service.LoadSheet(_path).Rows[0].Values[2]);
    }

    [Fact]
    public void EmptyCell_IsEmptyString()
    {
        CreateSampleFile(ws => ws.Cell(2, 4).Clear());

        Assert.Equal("", _service.LoadSheet(_path).Rows[0].Values[3]);
    }

    [Fact]
    public void EmptyRows_AreSkipped()
    {
        CreateSampleFile(ws =>
        {
            ws.Row(3).InsertRowsAbove(2); // deux lignes vides entre BENALI et AMEUR
        });

        var rows = _service.LoadSheet(_path).Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal("AMEUR", rows[1].Values[0]);
        Assert.Equal(5, rows[1].RowNumber);
    }

    [Fact]
    public void EmptyOrDuplicateHeaders_AreRenamed()
    {
        var columns = ExcelService.BuildColumns(["Nom", "Prénom", "", "Nom", "  ", "Nom"]);

        Assert.Equal(["Nom", "Prénom", "Colonne 3", "Nom (2)", "Colonne 5", "Nom (3)"], columns.Select(c => c.Name));
    }

    [Fact]
    public void FileOpenWithWriteLock_CanStillBeRead()
    {
        CreateSampleFile();

        // Simule Excel : le fichier est ouvert en écriture par un autre programme.
        using var excelLock = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        Assert.Equal(2, _service.LoadSheet(_path).Rows.Count);
    }

    [Fact]
    public void OriginalFile_IsNeverModified()
    {
        CreateSampleFile();
        var before = SHA256.HashData(File.ReadAllBytes(_path));
        var lastWrite = File.GetLastWriteTimeUtc(_path);

        _service.LoadSheet(_path);
        _service.GetSheetNames(_path);

        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(_path)));
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(_path));
    }
}
