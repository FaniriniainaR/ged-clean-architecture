using Ged.Domain.Common;

namespace Ged.Domain.ValueObjects;

/// <summary>Type de fichier. Seuls les types de la liste blanche sont acceptés.</summary>
public sealed record MimeType
{
    private static readonly Dictionary<string, string> AllowedByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["txt"] = "text/plain",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    public string Value { get; }

    private MimeType(string value) => Value = value;

    public static MimeType Of(string value)
    {
        if (!AllowedByExtension.ContainsValue(value))
            throw new UnsupportedFileTypeException(value);
        return new MimeType(value);
    }

    public static MimeType FromExtension(string extension)
    {
        var ext = extension.TrimStart('.');
        if (!AllowedByExtension.TryGetValue(ext, out var mime))
            throw new UnsupportedFileTypeException(extension);
        return new MimeType(mime);
    }

    public static MimeType FromFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            throw new UnsupportedFileTypeException(fileName);
        try { return FromExtension(ext); }
        catch (UnsupportedFileTypeException) { throw new UnsupportedFileTypeException(fileName); }
    }

    public override string ToString() => Value;
}
