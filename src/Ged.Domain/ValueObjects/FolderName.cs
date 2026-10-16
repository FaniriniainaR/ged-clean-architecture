using Ged.Domain.Common;

namespace Ged.Domain.ValueObjects;

public sealed record FolderName
{
    public const int MaxLength = 100;

    public string Value { get; }

    private FolderName(string value) => Value = value;

    public static FolderName Of(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (value.Length == 0)
            throw new InvalidValueException("Le nom du dossier ne peut pas être vide.");
        if (value.Length > MaxLength)
            throw new InvalidValueException($"Le nom du dossier dépasse {MaxLength} caractères.");
        if (value.Contains('/') || value.Contains('\\'))
            throw new InvalidValueException("Le nom du dossier ne peut pas contenir '/' ou '\\'.");
        return new FolderName(value);
    }

    public override string ToString() => Value;
}
