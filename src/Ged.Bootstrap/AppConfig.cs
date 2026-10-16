using System.Text.Json;
using Ged.Infrastructure.Sql;

namespace Ged.Bootstrap;

/// <summary>Configuration lue depuis ged.config.json (ou le fichier indiqué par la variable GED_CONFIG).</summary>
public sealed class AppConfig
{
    /// <summary>Base par défaut, utilisée pour toutes les données sauf indication contraire dans « databases ».</summary>
    public DatabaseOptions Database { get; init; } = new("sqlite", "Data Source=ged.db");

    /// <summary>Facultatif : une base différente pour les documents et/ou pour les dossiers.</summary>
    public DatabasesConfig? Databases { get; init; }

    public string StoragePath { get; init; } = "storage";
    public int MaxFileSizeMb { get; init; } = 20;
    public int TrashRetentionDays { get; init; } = 30;
    public string WebUrl { get; init; } = "http://localhost:5000/";

    public DatabaseOptions DocumentsDatabase => Databases?.Documents ?? Database;
    public DatabaseOptions FoldersDatabase => Databases?.Folders ?? Database;

    public string DescribeDatabases() => DocumentsDatabase == FoldersDatabase
        ? DocumentsDatabase.Provider
        : $"documents : {DocumentsDatabase.Provider}, dossiers : {FoldersDatabase.Provider}";

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

public sealed class DatabasesConfig
{
    public DatabaseOptions? Documents { get; init; }
    public DatabaseOptions? Folders { get; init; }
}
