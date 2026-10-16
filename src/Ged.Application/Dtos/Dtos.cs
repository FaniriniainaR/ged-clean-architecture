using Ged.Domain.Entities;

namespace Ged.Application.Dtos;

/// <summary>Données renvoyées à la présentation : elle ne manipule jamais les entités directement.</summary>
public sealed record VersionDto(
    int Number, string FileName, string MimeType, long SizeBytes, string Size, string Checksum, DateTime CreatedAt);

public sealed record DocumentDto(
    Guid Id, string Title, Guid? FolderId, string Status, IReadOnlyList<string> Tags,
    DateTime CreatedAt, DateTime? TrashedAt, IReadOnlyList<VersionDto> Versions)
{
    public VersionDto CurrentVersion => Versions[^1];

    public static DocumentDto From(Document d) => new(
        d.Id, d.Title.Value, d.FolderId, d.Status.ToString(),
        d.Tags.Select(t => t.Value).ToList(), d.CreatedAt, d.TrashedAt,
        d.Versions.Select(v => new VersionDto(
            v.Number, v.File.OriginalFileName, v.File.MimeType.Value, v.File.Size.Bytes,
            v.File.Size.ToString(), v.File.Checksum.Value, v.CreatedAt)).ToList());
}

public sealed record FolderDto(Guid Id, string Name, Guid? ParentId, DateTime CreatedAt)
{
    public static FolderDto From(Folder f) => new(f.Id, f.Name.Value, f.ParentId, f.CreatedAt);
}

public sealed record DownloadResult(string FileName, string MimeType, Stream Content);
