using System.Data.Common;
using System.Text;
using Ged.Application.Common;
using Ged.Application.UseCases;
using Ged.Domain.Common;
using Ged.Domain.ValueObjects;
using Ged.Infrastructure.InMemory;
using Ged.Infrastructure.Services;
using Ged.Infrastructure.Sql;

namespace Ged.Tests;

/// <summary>
/// Documents dans une base, dossiers dans une autre : les vrais cas d'utilisation fonctionnent
/// sans savoir que les données sont réparties sur deux bases.
/// </summary>
public static class SplitDatabaseTests
{
    private sealed class Sut
    {
        private readonly SqlConnectionFactory _documentsDb;
        private readonly SqlConnectionFactory _foldersDb;
        private readonly SqlDocumentRepository _documents;
        private readonly SqlFolderRepository _folders;
        private readonly InMemoryFileStorage _storage = new();
        private readonly FakeClock _clock = new(new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc));
        private readonly FileIntake _intake;

        public Sut(DatabaseOptions documentsDb, DatabaseOptions foldersDb)
        {
            _documentsDb = new SqlConnectionFactory(documentsDb);
            _foldersDb = new SqlConnectionFactory(foldersDb);
            new SchemaInitializer(_documentsDb).EnsureDocumentTables();
            new SchemaInitializer(_foldersDb).EnsureFolderTables();
            Clear(_documentsDb, "document_tags", "document_versions", "documents");
            Clear(_foldersDb, "folders");
            _documents = new SqlDocumentRepository(_documentsDb);
            _folders = new SqlFolderRepository(_foldersDb);
            _intake = new FileIntake(_storage, new Sha256ChecksumCalculator(), DocumentPolicy.Default);
        }

        public CreateFolder CreateFolder => new(_folders, _clock);
        public UploadDocument Upload => new(_documents, _folders, _storage, _intake, _clock);
        public MoveDocument Move => new(_documents, _folders);
        public SearchDocuments Search => new(_documents);
        public DeleteFolder DeleteFolder => new(_folders, _documents);

        public bool DocumentsDbHas(string table) => HasTable(_documentsDb, table);
        public bool FoldersDbHas(string table) => HasTable(_foldersDb, table);
    }

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));

    /// <param name="freshDatabases">
    /// Vrai si les deux bases sont neuves et dédiées à ce test : on peut alors vérifier qu'aucune table
    /// « en trop » n'y a été créée. Faux pour des bases partagées avec d'autres tests.
    /// </param>
    public static IEnumerable<TestCase> All(DatabaseOptions documentsDb, DatabaseOptions foldersDb, bool freshDatabases)
        => (freshDatabases ? [IsolationTest(documentsDb, foldersDb)] : Array.Empty<TestCase>()).Concat(UseCases(documentsDb, foldersDb));

    private static TestCase IsolationTest(DatabaseOptions documentsDb, DatabaseOptions foldersDb) =>
        new("Chaque base ne contient que ses propres tables", () =>
        {
            var sut = new Sut(documentsDb, foldersDb);
            Check.True(sut.DocumentsDbHas("documents"), "la base des documents doit avoir la table documents");
            Check.True(!sut.DocumentsDbHas("folders"), "la base des documents ne doit pas avoir la table folders");
            Check.True(sut.FoldersDbHas("folders"), "la base des dossiers doit avoir la table folders");
            Check.True(!sut.FoldersDbHas("documents"), "la base des dossiers ne doit pas avoir la table documents");
        });

    private static IEnumerable<TestCase> UseCases(DatabaseOptions documentsDb, DatabaseOptions foldersDb) =>
    [
        new("Envoyer un document dans un dossier stocké dans l'autre base", () =>
        {
            var sut = new Sut(documentsDb, foldersDb);
            var folder = sut.CreateFolder.Execute("Factures");
            sut.Upload.Execute(new UploadDocumentCommand("edf.pdf", Content("1"), FolderId: folder.Id));
            Check.Equal(1, sut.Search.Execute(new SearchQuery(FolderId: folder.Id)).Count, "documents du dossier");
        }),

        new("Dossier inconnu (vérifié dans la base des dossiers) refusé", () =>
        {
            var sut = new Sut(documentsDb, foldersDb);
            Check.Throws<NotFoundException>(() =>
                sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1"), FolderId: Guid.NewGuid())));
        }),

        new("Dossier non vide (documents dans l'autre base) : suppression refusée", () =>
        {
            var sut = new Sut(documentsDb, foldersDb);
            var folder = sut.CreateFolder.Execute("Contrats");
            sut.Upload.Execute(new UploadDocumentCommand("bail.pdf", Content("1"), FolderId: folder.Id));
            Check.Throws<FolderNotEmptyException>(() => sut.DeleteFolder.Execute(folder.Id));
        }),

        new("Déplacer un document d'un dossier à l'autre", () =>
        {
            var sut = new Sut(documentsDb, foldersDb);
            var a = sut.CreateFolder.Execute("A");
            var b = sut.CreateFolder.Execute("B");
            var doc = sut.Upload.Execute(new UploadDocumentCommand("x.pdf", Content("1"), FolderId: a.Id));
            sut.Move.Execute(doc.Id, b.Id);
            Check.Equal(0, sut.Search.Execute(new SearchQuery(FolderId: a.Id)).Count, "dossier A");
            Check.Equal(1, sut.Search.Execute(new SearchQuery(FolderId: b.Id)).Count, "dossier B");
            sut.DeleteFolder.Execute(a.Id); // A est vide : suppression acceptée
        }),
    ];

    private static void Clear(SqlConnectionFactory db, params string[] tables)
    {
        using var connection = db.Open();
        foreach (var table in tables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {table}";
            command.ExecuteNonQuery();
        }
    }

    private static bool HasTable(SqlConnectionFactory db, string table)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE 1 = 0";
        try
        {
            command.ExecuteScalar();
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
