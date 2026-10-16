using Ged.Domain.Common;

namespace Ged.Domain.ValueObjects;

/// <summary>Étiquette normalisée : " Facture " et "FACTURE" donnent le même tag.</summary>
public sealed record Tag
{
    public const int MaxLength = 50;

    public string Value { get; }

    private Tag(string value) => Value = value;

    public static Tag Of(string? raw)
    {
        var value = string.Join(' ', (raw ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
        if (value.Length == 0)
            throw new InvalidValueException("Un tag ne peut pas être vide.");
        if (value.Length > MaxLength)
            throw new InvalidValueException($"Un tag dépasse {MaxLength} caractères.");
        return new Tag(value);
    }

    public override string ToString() => Value;
}
