namespace Ged.Domain.ValueObjects;

/// <summary>Fichier déjà écrit dans le stockage : le domaine ne connaît que sa clé, jamais ses octets.</summary>
public sealed record StoredFile(
    string StorageKey,
    string OriginalFileName,
    MimeType MimeType,
    FileSize Size,
    Checksum Checksum);
