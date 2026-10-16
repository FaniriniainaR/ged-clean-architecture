using Ged.Domain.Common;

namespace Ged.Domain.ValueObjects;

/// <summary>Empreinte SHA-256 (64 caractères hexadécimaux).</summary>
public sealed record Checksum
{
    public string Value { get; }

    private Checksum(string value) => Value = value;

    public static Checksum Of(string hex)
    {
        var value = (hex ?? "").ToLowerInvariant();
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new InvalidValueException("Empreinte SHA-256 invalide.");
        return new Checksum(value);
    }

    public override string ToString() => Value;
}
