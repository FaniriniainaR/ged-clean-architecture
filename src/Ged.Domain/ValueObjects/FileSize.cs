using Ged.Domain.Common;

namespace Ged.Domain.ValueObjects;

public sealed record FileSize
{
    public long Bytes { get; }

    private FileSize(long bytes) => Bytes = bytes;

    public static FileSize Of(long bytes)
    {
        if (bytes <= 0)
            throw new InvalidValueException("Le fichier est vide.");
        return new FileSize(bytes);
    }

    public override string ToString() => Bytes switch
    {
        < 1024 => $"{Bytes} o",
        < 1024 * 1024 => $"{Bytes / 1024.0:0.#} Ko",
        _ => $"{Bytes / (1024.0 * 1024):0.#} Mo",
    };
}
