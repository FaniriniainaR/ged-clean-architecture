using Ged.Domain.ValueObjects;

namespace Ged.Domain.Entities;

public sealed class Folder
{
    public Guid Id { get; }
    public FolderName Name { get; private set; }
    public Guid? ParentId { get; }
    public DateTime CreatedAt { get; }

    private Folder(Guid id, FolderName name, Guid? parentId, DateTime createdAt)
    {
        Id = id;
        Name = name;
        ParentId = parentId;
        CreatedAt = createdAt;
    }

    public static Folder Create(Guid id, FolderName name, Guid? parentId, DateTime now)
        => new(id, name, parentId, now);

    public static Folder Rehydrate(Guid id, FolderName name, Guid? parentId, DateTime createdAt)
        => new(id, name, parentId, createdAt);

    public void Rename(FolderName name) => Name = name;
}
