using System.IO;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"efa-{Guid.NewGuid():N}");

    private SettingsService CreateService() => new(Path.Combine(_folder, "parametres.json"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void SaveThenLoad_GivesBackTheSameSettings()
    {
        var service = CreateService();
        var settings = new Settings
        {
            FilePath = @"D:\dossiers\candidats.xlsx",
            SheetName = "Candidats",
            Window = new WindowBounds(120, 60, 1000, 600, Maximized: true),
        };

        service.Save(settings);

        Assert.Equal(settings, CreateService().Load());
    }

    [Fact]
    public void Save_CreatesTheFolderIfNeeded()
    {
        var service = CreateService();

        service.Save(new Settings { FilePath = "x.xlsx" });

        Assert.True(File.Exists(service.FilePath));
    }

    [Fact]
    public void Load_WithoutAnyFile_GivesTheDefaults()
    {
        var settings = CreateService().Load();

        Assert.Null(settings.FilePath);
        Assert.Null(settings.SheetName);
        Assert.Null(settings.Window);
    }

    [Theory]
    [InlineData("{ pas du json")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    public void Load_WithADamagedFile_GivesTheDefaultsInsteadOfFailing(string content)
    {
        var service = CreateService();
        Directory.CreateDirectory(_folder);
        File.WriteAllText(service.FilePath, content);

        Assert.Equal(new Settings(), service.Load());
    }

    [Fact]
    public void Save_WhenTheFileCannotBeWritten_DoesNotThrow()
    {
        // Un dossier à la place du fichier : l'écriture est impossible.
        var service = CreateService();
        Directory.CreateDirectory(service.FilePath);

        service.Save(new Settings { FilePath = "x.xlsx" });

        Assert.Equal(new Settings(), service.Load());
    }

    [Fact]
    public void DefaultPath_IsInTheUserApplicationData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.Equal(Path.Combine(appData, "ExcelFormAssistant", "parametres.json"), SettingsService.DefaultPath);
    }
}
