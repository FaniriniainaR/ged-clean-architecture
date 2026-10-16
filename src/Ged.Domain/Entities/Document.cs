using Ged.Domain.Common;
using Ged.Domain.ValueObjects;

namespace Ged.Domain.Entities;

public sealed class Document
{
    private readonly List<DocumentVersion> _versions;
    private readonly HashSet<Tag> _tags;

    public Guid Id { get; }
    public DocumentTitle Title { get; private set; }
    public Guid? FolderId { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? TrashedAt { get; private set; }

    public IReadOnlyList<DocumentVersion> Versions => _versions;
    public IReadOnlyList<Tag> Tags => _tags.OrderBy(t => t.Value).ToList();
    public DocumentVersion CurrentVersion => _versions[^1];

    private Document(
        Guid id, DocumentTitle title, Guid? folderId, DocumentStatus status,
        DateTime createdAt, DateTime? trashedAt, IEnumerable<Tag> tags, IEnumerable<DocumentVersion> versions)
    {
        Id = id;
        Title = title;
        FolderId = folderId;
        Status = status;
        CreatedAt = createdAt;
        TrashedAt = trashedAt;
        _tags = new HashSet<Tag>(tags);
        _versions = versions.OrderBy(v => v.Number).ToList();
        if (_versions.Count == 0)
            throw new DomainException("Un document doit avoir au moins une version.");
    }

    public static Document Create(
        Guid id, DocumentTitle title, Guid? folderId, IEnumerable<Tag> tags, StoredFile file, DateTime now)
    {
        var firstVersion = new DocumentVersion(1, file, now);
        return new Document(id, title, folderId, DocumentStatus.Active, now, null, tags, [firstVersion]);
    }

    /// <summary>Reconstruit un document depuis le stockage (utilisé par les dépôts).</summary>
    public static Document Rehydrate(
        Guid id, DocumentTitle title, Guid? folderId, DocumentStatus status,
        DateTime createdAt, DateTime? trashedAt, IEnumerable<Tag> tags, IEnumerable<DocumentVersion> versions)
        => new(id, title, folderId, status, createdAt, trashedAt, tags, versions);

    public DocumentVersion AddVersion(StoredFile file, DateTime now)
    {
        EnsureActive();
        if (file.Checksum == CurrentVersion.File.Checksum)
            throw new DuplicateVersionException();

        var version = new DocumentVersion(CurrentVersion.Number + 1, file, now);
        _versions.Add(version);
        return version;
    }

    public DocumentVersion GetVersion(int number)
        => _versions.FirstOrDefault(v => v.Number == number)
           ?? throw new DomainException($"La version {number} n'existe pas.");

    public void Rename(DocumentTitle title)
    {
        EnsureActive();
        Title = title;
    }

    public void MoveTo(Guid? folderId)
    {
        EnsureActive();
        FolderId = folderId;
    }

    public void AddTag(Tag tag)
    {
        EnsureActive();
        _tags.Add(tag);
    }

    public void RemoveTag(Tag tag)
    {
        EnsureActive();
        _tags.Remove(tag);
    }

    public void Trash(DateTime now)
    {
        EnsureActive();
        Status = DocumentStatus.Trashed;
        TrashedAt = now;
    }

    public void Restore()
    {
        if (Status != DocumentStatus.Trashed)
            throw new DocumentNotTrashedException();
        Status = DocumentStatus.Active;
        TrashedAt = null;
    }

    public bool IsPurgeable(DateTime now, DocumentPolicy policy)
        => Status == DocumentStatus.Trashed
           && TrashedAt is { } trashedAt
           && now - trashedAt >= TimeSpan.FromDays(policy.TrashRetentionDays);

    public void EnsureActive()
    {
        if (Status == DocumentStatus.Trashed)
            throw new DocumentTrashedException();
    }
}
