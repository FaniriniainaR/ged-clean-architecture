namespace Ged.Infrastructure.Sql;

/// <summary>Lu depuis le fichier de configuration : changer de base = changer ces deux valeurs.</summary>
public sealed record DatabaseOptions(string Provider, string ConnectionString);
