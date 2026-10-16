using System.Text.Json;
using Ged.Infrastructure.Sql;

namespace Ged.Cli;

/// <summary>Configuration lue depuis ged.config.json (ou le fichier indiqué par la variable GED_CONFIG).</summary>
public sealed class AppConfig
{
    public DatabaseOptions Database { get; init; } = new("sqlite", "Data Source=ged.db");
    public string StoragePath { get; init; } = "storage";
    public int MaxFileSizeMb { get; init; } = 20;
    public int TrashRetentionDays { get; init; } = 30;

    public static AppConfig Load()
    {
        var path = Environment.GetEnvironmentVariable("GED_CONFIG") ?? "ged.config.json";
        if (!File.Exists(path))
            return new AppConfig();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options)
               ?? throw new InvalidOperationException($"Configuration invalide : {path}.");
    }
}
