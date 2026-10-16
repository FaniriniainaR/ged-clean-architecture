using Ged.Domain.Common;

namespace Ged.Domain.ValueObjects;

public sealed record DocumentTitle
{
    public const int MaxLength = 200;

    public string Value { get; }

    /// <summary>Forme utilisée pour comparer et rechercher (insensible à la casse).</summary>
    public string Normalized => Value.ToLowerInvariant();

    private DocumentTitle(string value) => Value = value;

    public static DocumentTitle Of(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (value.Length == 0)
            throw new InvalidValueException("Le titre ne peut pas être vide.");
        if (value.Length > MaxLength)
            throw new InvalidValueException($"Le titre dépasse {MaxLength} caractères.");
        return new DocumentTitle(value);
    }

    public bool SameAs(DocumentTitle other) => Normalized == other.Normalized;

    public override string ToString() => Value;
}
