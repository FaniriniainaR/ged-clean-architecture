using Ged.Domain.ValueObjects;

namespace Ged.Domain.Entities;

public sealed class DocumentVersion
{
    public int Number { get; }
    public StoredFile File { get; }
    public DateTime CreatedAt { get; }

    public DocumentVersion(int number, StoredFile file, DateTime createdAt)
    {
        Number = number;
        File = file;
        CreatedAt = createdAt;
    }
}
