using System.Data.Common;
using Ged.Application.Ports;
using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Infrastructure.Sql;

public sealed class SqlFolderRepository(SqlConnectionFactory connections) : IFolderRepository
{
    private const string Columns = "id, name, parent_id, created_at";

    public Folder? FindById(Guid id)
    {
        using var connection = connections.Open();
        using var reader = connection.Command($"SELECT {Columns} FROM folders WHERE id = @id")
            .With("@id", id.ToDb()).ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public IReadOnlyList<Folder> FindAll()
    {
        using var connection = connections.Open();
        using var reader = connection.Command($"SELECT {Columns} FROM folders").ExecuteReader();
        var folders = new List<Folder>();
        while (reader.Read()) folders.Add(Map(reader));
        return folders.OrderBy(f => f.Name.Value.ToLowerInvariant()).ToList();
    }

    public void Save(Folder folder)
    {
        using var connection = connections.Open();
        var exists = connection.Command("SELECT COUNT(*) FROM folders WHERE id = @id")
            .With("@id", folder.Id.ToDb()).Count() > 0;

        var sql = exists
            ? "UPDATE folders SET name = @name, parent_id = @parent_id, created_at = @created_at WHERE id = @id"
            : "INSERT INTO folders (id, name, parent_id, created_at) VALUES (@id, @name, @parent_id, @created_at)";

        connection.Command(sql)
            .With("@id", folder.Id.ToDb())
            .With("@name", folder.Name.Value)
            .With("@parent_id", folder.ParentId.ToDb())
            .With("@created_at", folder.CreatedAt.ToDb())
            .ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var connection = connections.Open();
        connection.Command("DELETE FROM folders WHERE id = @id").With("@id", id.ToDb()).ExecuteNonQuery();
    }

    public bool HasChildren(Guid id)
    {
        using var connection = connections.Open();
        return connection.Command("SELECT COUNT(*) FROM folders WHERE parent_id = @id")
            .With("@id", id.ToDb()).Count() > 0;
    }

    private static Folder Map(DbDataReader r) => Folder.Rehydrate(
        r.ReadGuid("id"), FolderName.Of(r.ReadString("name")), r.ReadNullableGuid("parent_id"), r.ReadDate("created_at"));
}
