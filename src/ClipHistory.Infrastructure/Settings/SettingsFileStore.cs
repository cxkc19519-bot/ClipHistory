using System.Text.Json;
using System.Text.Json.Serialization;
using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Storage;

namespace ClipHistory.Infrastructure.Settings;

public sealed class SettingsFileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AppDataPaths paths;

    public SettingsFileStore(AppDataPaths paths)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public AppSettings Load()
    {
        if (!File.Exists(paths.SettingsPath))
        {
            return AppSettings.Default;
        }

        try
        {
            string json = File.ReadAllText(paths.SettingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings is not null && IsValid(settings)
                ? settings
                : AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
        catch (IOException)
        {
            return AppSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid(settings))
        {
            throw new ArgumentException("Application settings contain unsupported values.", nameof(settings));
        }

        paths.EnsureDirectoriesExist();
        string temporaryPath = $"{paths.SettingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, paths.SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static bool IsValid(AppSettings settings)
    {
        try
        {
            _ = settings.RetentionPeriod.ToTimeSpan();
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return Enum.IsDefined(settings.Language) && Enum.IsDefined(settings.HotKey);
    }
}
