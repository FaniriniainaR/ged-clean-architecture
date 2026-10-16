using System.Data.Common;
using System.Text;
using Ged.Application.Ports;
using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Infrastructure.Sql;

/// <summary>
/// Dépôt SQL générique : uniquement du SQL standard et des paramètres "@nom",
/// donc le même code fonctionne avec SQLite, MySQL/MariaDB et SQL Server.
/// </summary>
public sealed class SqlDocumentRepository(SqlConnectionFactory connections) : IDocumentRepository
{
    public Document? FindById(Guid id)
    {
        using var connection = connections.Open();
        return Load(connection, id);
    }

    public void Save(Document document)
    {
        using var connection = connections.Open();
        using var transaction = connection.BeginTransaction();
        var id = document.Id.ToDb();

        var exists = connection.Command("SELECT COUNT(*) FROM documents WHERE id = @id", transaction)
            .With("@id", id).Count() > 0;

        var sql = exists
            ? """
              UPDATE documents SET title = @title, title_normalized = @title_normalized, folder_id = @folder_id,
                     status = @status, created_at = @created_at, trashed_at = @trashed_at
              WHERE id = @id
              """
            : """
              INSERT INTO documents (id, title, title_normalized, folder_id, status, created_at, trashed_at)
              VALUES (@id, @title, @title_normalized, @folder_id, @status, @created_at, @trashed_at)
              """;

        connection.Command(sql, transaction)
            .With("@id", id)
            .With("@title", document.Title.Value)
            .With("@title_normalized", document.Title.Normalized)
            .With("@folder_id", document.FolderId.ToDb())
            .With("@status", document.Status.ToString())
            .With("@created_at", document.CreatedAt.ToDb())
            .With("@trashed_at", document.TrashedAt.ToDb())
            .ExecuteNonQuery();

        // Tags et versions : on remplace l'ensemble (simple et portable, pas d'UPSERT spécifique à une base).
        connection.Command("DELETE FROM document_tags WHERE document_id = @id", transaction).With("@id", id).ExecuteNonQuery();
        connection.Command("DELETE FROM document_versions WHERE document_id = @id", transaction).With("@id", id).ExecuteNonQuery();

        foreach (var tag in document.Tags)
        {
            connection.Command("INSERT INTO document_tags (document_id, tag) VALUES (@id, @tag)", transaction)
                .With("@id", id).With("@tag", tag.Value).ExecuteNonQuery();
        }

        foreach (var v in document.Versions)
        {
            connection.Command("""
                INSERT INTO document_versions
                    (document_id, version_number, storage_key, original_file_name, mime_type, size_bytes, checksum, created_at)
                VALUES (@id, @number, @storage_key, @file_name, @mime, @size, @checksum, @created_at)
                """, transaction)
                .With("@id", id)
                .With("@number", v.Number)
                .With("@storage_key", v.File.StorageKey)
                .With("@file_name", v.File.OriginalFileName)
                .With("@mime", v.File.MimeType.Value)
                .With("@size", v.File.Size.Bytes)
                .With("@checksum", v.File.Checksum.Value)
                .With("@created_at", v.CreatedAt.ToDb())
                .ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void Delete(Guid id)
    {
        using var connection = connections.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[] { "document_tags", "document_versions" })
            connection.Command($"DELETE FROM {table} WHERE document_id = @id", transaction).With("@id", id.ToDb()).ExecuteNonQuery();
        connection.Command("DELETE FROM documents WHERE id = @id", transaction).With("@id", id.ToDb()).ExecuteNonQuery();
        transaction.Commit();
    }

    public bool TitleExistsInFolder(Guid? folderId, DocumentTitle title, Guid? excludeId = null)
    {
        using var connection = connections.Open();
        var sql = new StringBuilder("SELECT COUNT(*) FROM documents WHERE status = @status AND title_normalized = @title");
        var command = connection.Command("")
            .With("@status", nameof(DocumentStatus.Active))
            .With("@title", title.Normalized);

        if (folderId is null)
            sql.Append(" AND folder_id IS NULL");
        else
        {
            sql.Append(" AND folder_id = @folder_id");
            command.With("@folder_id", folderId.ToDb());
        }
        if (excludeId is not null)
        {
            sql.Append(" AND id <> @exclude_id");
            command.With("@exclude_id", excludeId.ToDb());
        }

        command.CommandText = sql.ToString();
        return command.Count() > 0;
    }

    public bool AnyInFolder(Guid folderId)
    {
        using var connection = connections.Open();
        return connection.Command("SELECT COUNT(*) FROM documents WHERE folder_id = @folder_id")
            .With("@folder_id", folderId.ToDb()).Count() > 0;
    }

    public IReadOnlyList<Document> Search(SearchCriteria c)
    {
        using var connection = connections.Open();
        var sql = new StringBuilder("SELECT d.id FROM documents d WHERE d.status = @status");
        var command = connection.Command("").With("@status", c.Status.ToString());

        if (c.Text is not null)
        {
            sql.Append(" AND d.title_normalized LIKE @text");
            command.With("@text", $"%{c.Text.ToLowerInvariant()}%");
        }
        if (c.Tag is not null)
        {
            sql.Append(" AND EXISTS (SELECT 1 FROM document_tags t WHERE t.document_id = d.id AND t.tag = @tag)");
            command.With("@tag", c.Tag.Value);
        }
        if (c.Type is not null)
        {
            // Type de la version courante (= numéro de version le plus élevé).
            sql.Append("""
                 AND EXISTS (SELECT 1 FROM document_versions v
                             WHERE v.document_id = d.id AND v.mime_type = @mime
                               AND v.version_number = (SELECT MAX(v2.version_number) FROM document_versions v2
                                                       WHERE v2.document_id = d.id))
                """);
            command.With("@mime", c.Type.Value);
        }
        if (c.FolderId is not null)
        {
            sql.Append(" AND d.folder_id = @folder_id");
            command.With("@folder_id", c.FolderId.ToDb());
        }
        sql.Append(" ORDER BY d.title_normalized");
        command.CommandText = sql.ToString();

        var ids = new List<Guid>();
        using (var reader = command.ExecuteReader())
            while (reader.Read()) ids.Add(reader.ReadGuid("id"));

        return ids.Select(id => Load(connection, id)!).ToList();
    }

    private static Document? Load(DbConnection connection, Guid id)
    {
        var key = id.ToDb();

        DocumentTitle title;
        Guid? folderId;
        DocumentStatus status;
        DateTime createdAt;
        DateTime? trashedAt;
        using (var r = connection.Command(
                   "SELECT title, folder_id, status, created_at, trashed_at FROM documents WHERE id = @id")
                   .With("@id", key).ExecuteReader())
        {
            if (!r.Read()) return null;
            title = DocumentTitle.Of(r.ReadString("title"));
            folderId = r.ReadNullableGuid("folder_id");
            status = Enum.Parse<DocumentStatus>(r.ReadString("status"));
            createdAt = r.ReadDate("created_at");
            trashedAt = r.ReadNullableDate("trashed_at");
        }

        var tags = new List<Tag>();
        using (var r = connection.Command("SELECT tag FROM document_tags WHERE document_id = @id")
                   .With("@id", key).ExecuteReader())
            while (r.Read()) tags.Add(Tag.Of(r.ReadString("tag")));

        var versions = new List<DocumentVersion>();
        using (var r = connection.Command("""
                   SELECT version_number, storage_key, original_file_name, mime_type, size_bytes, checksum, created_at
                   FROM document_versions WHERE document_id = @id
                   """).With("@id", key).ExecuteReader())
        {
            while (r.Read())
            {
                var file = new StoredFile(
                    r.ReadString("storage_key"),
                    r.ReadString("original_file_name"),
                    MimeType.Of(r.ReadString("mime_type")),
                    FileSize.Of(r.ReadLong("size_bytes")),
                    Checksum.Of(r.ReadString("checksum")));
                versions.Add(new DocumentVersion((int)r.ReadLong("version_number"), file, r.ReadDate("created_at")));
            }
        }

        return Document.Rehydrate(id, title, folderId, status, createdAt, trashedAt, tags, versions);
    }
}
