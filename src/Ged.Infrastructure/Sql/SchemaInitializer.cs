using System.Data.Common;

namespace Ged.Infrastructure.Sql;

/// <summary>
/// Crée les tables si elles n'existent pas. Les types utilisés (CHAR, NVARCHAR, INT, BIGINT)
/// sont acceptés par SQLite, MySQL/MariaDB et SQL Server : un seul script pour toutes les bases.
/// </summary>
public sealed class SchemaInitializer(SqlConnectionFactory connections)
{
    private static readonly string[] Statements =
    [
        """
        CREATE TABLE folders (
            id          CHAR(36)      NOT NULL PRIMARY KEY,
            name        NVARCHAR(100) NOT NULL,
            parent_id   CHAR(36)      NULL,
            created_at  VARCHAR(40)   NOT NULL
        )
        """,
        """
        CREATE TABLE documents (
            id                CHAR(36)      NOT NULL PRIMARY KEY,
            title             NVARCHAR(200) NOT NULL,
            title_normalized  NVARCHAR(200) NOT NULL,
            folder_id         CHAR(36)      NULL,
            status            VARCHAR(20)   NOT NULL,
            created_at        VARCHAR(40)   NOT NULL,
            trashed_at        VARCHAR(40)   NULL
        )
        """,
        """
        CREATE TABLE document_versions (
            document_id         CHAR(36)      NOT NULL,
            version_number      INT           NOT NULL,
            storage_key         VARCHAR(100)  NOT NULL,
            original_file_name  NVARCHAR(255) NOT NULL,
            mime_type           VARCHAR(150)  NOT NULL,
            size_bytes          BIGINT        NOT NULL,
            checksum            CHAR(64)      NOT NULL,
            created_at          VARCHAR(40)   NOT NULL,
            PRIMARY KEY (document_id, version_number),
            FOREIGN KEY (document_id) REFERENCES documents (id)
        )
        """,
        """
        CREATE TABLE document_tags (
            document_id  CHAR(36)     NOT NULL,
            tag          NVARCHAR(50) NOT NULL,
            PRIMARY KEY (document_id, tag),
            FOREIGN KEY (document_id) REFERENCES documents (id)
        )
        """,
    ];

    public void EnsureCreated()
    {
        using var connection = connections.Open();
        if (TablesExist(connection)) return;

        using var transaction = connection.BeginTransaction();
        foreach (var sql in Statements)
            connection.Command(sql, transaction).ExecuteNonQuery();
        transaction.Commit();
    }

    // « CREATE TABLE IF NOT EXISTS » n'existe pas en SQL Server : on teste plutôt une requête simple.
    private static bool TablesExist(DbConnection connection)
    {
        try
        {
            connection.Command("SELECT COUNT(*) FROM documents WHERE 1 = 0").ExecuteScalar();
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
