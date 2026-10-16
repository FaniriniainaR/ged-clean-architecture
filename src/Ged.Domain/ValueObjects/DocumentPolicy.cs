using Ged.Domain.Common;

namespace Ged.Domain.ValueObjects;

/// <summary>Paramètres des règles métier (taille maximale, durée de la corbeille).</summary>
public sealed record DocumentPolicy(long MaxFileSizeBytes, int TrashRetentionDays)
{
    public static readonly DocumentPolicy Default = new(20L * 1024 * 1024, 30);

    public void EnsureSizeAllowed(FileSize size)
    {
        if (size.Bytes > MaxFileSizeBytes)
            throw new FileTooLargeException(size.Bytes, MaxFileSizeBytes);
    }
}
