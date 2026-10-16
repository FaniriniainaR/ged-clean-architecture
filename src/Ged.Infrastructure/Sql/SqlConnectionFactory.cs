using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace Ged.Infrastructure.Sql;

/// <summary>
/// Crée une connexion ADO.NET à partir du nom du fournisseur indiqué dans la configuration.
/// Tout le reste du code ne manipule que DbConnection / DbCommand, communs à toutes les bases.
/// </summary>
public sealed class SqlConnectionFactory
{
    private static readonly Dictionary<string, DbProviderFactory> Providers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sqlite"] = SqliteFactory.Instance,
        ["mysql"] = MySqlConnectorFactory.Instance,
        ["mariadb"] = MySqlConnectorFactory.Instance,
        ["sqlserver"] = SqlClientFactory.Instance,
    };

    private readonly DbProviderFactory _factory;
    private readonly string _connectionString;

    public SqlConnectionFactory(DatabaseOptions options)
    {
        if (!Providers.TryGetValue(options.Provider, out var factory))
            throw new ArgumentException(
                $"Base '{options.Provider}' non supportée. Valeurs possibles : {string.Join(", ", Providers.Keys)}.");
        _factory = factory;
        _connectionString = options.ConnectionString;
    }

    public static IReadOnlyCollection<string> SupportedProviders => Providers.Keys;

    public DbConnection Open()
    {
        var connection = _factory.CreateConnection()!;
        connection.ConnectionString = _connectionString;
        connection.Open();
        return connection;
    }
}
