using Ged.Infrastructure.InMemory;
using Ged.Infrastructure.Sql;
using Ged.Tests;

var results = new List<(int Passed, int Failed)>
{
    TestRunner.Run("Domaine", DomainTests.All()),
    TestRunner.Run("Cas d'utilisation (adaptateurs en mémoire)", UseCaseTests.All()),
    TestRunner.Run("Dépôts : mémoire",
        RepositoryContractTests.All(() => (new InMemoryDocumentRepository(), new InMemoryFolderRepository()))),
};

// Bases SQL : SQLite toujours (simple fichier) ; MySQL et SQL Server si une chaîne de connexion est fournie.
var databases = new List<DatabaseOptions>
{
    new("sqlite", $"Data Source={Path.Combine(Path.GetTempPath(), $"ged-test-{Guid.NewGuid():N}.db")};Pooling=False"),
};
if (Environment.GetEnvironmentVariable("GED_TEST_MYSQL") is { } mysql) databases.Add(new("mysql", mysql));
if (Environment.GetEnvironmentVariable("GED_TEST_SQLSERVER") is { } sqlServer) databases.Add(new("sqlserver", sqlServer));

foreach (var options in databases)
{
    var connections = new SqlConnectionFactory(options);
    new SchemaInitializer(connections).EnsureCreated();
    results.Add(TestRunner.Run($"Dépôts : {options.Provider}", RepositoryContractTests.All(() =>
    {
        ClearTables(connections);
        return (new SqlDocumentRepository(connections), new SqlFolderRepository(connections));
    })));
}

var passed = results.Sum(r => r.Passed);
var failed = results.Sum(r => r.Failed);
Console.WriteLine($"\n{passed} réussi(s), {failed} échoué(s).");
if (databases.Count == 1)
    Console.WriteLine("(Définir GED_TEST_MYSQL / GED_TEST_SQLSERVER pour tester aussi MySQL / SQL Server.)");
return failed == 0 ? 0 : 1;

static void ClearTables(SqlConnectionFactory connections)
{
    using var connection = connections.Open();
    foreach (var table in new[] { "document_tags", "document_versions", "documents", "folders" })
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table}";
        command.ExecuteNonQuery();
    }
}
