using System.IO;
using System.Text.Json;
using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Paramètres enregistrés dans %AppData%\ExcelFormAssistant\parametres.json.
/// Un fichier absent, illisible ou abîmé n'est jamais une erreur : on repart des valeurs
/// par défaut, et un enregistrement impossible n'empêche pas de fermer l'application.
/// </summary>
public sealed class SettingsService(string? path = null)
{
    private static readonly JsonSerializerOptions JsonFormat = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExcelFormAssistant",
        "parametres.json");

    public string FilePath { get; } = path ?? DefaultPath;

    public Settings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonFormat));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // Dossier en lecture seule, disque plein… : la session suivante repartira des valeurs par défaut.
        }
    }
}
