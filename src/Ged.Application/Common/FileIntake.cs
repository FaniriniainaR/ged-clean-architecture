using Ged.Application.Ports;
using Ged.Domain.ValueObjects;

namespace Ged.Application.Common;

/// <summary>Valide un fichier entrant (type, taille), calcule son empreinte puis l'écrit dans le stockage.</summary>
public sealed class FileIntake(IFileStorage storage, IChecksumCalculator checksums, DocumentPolicy policy)
{
    public StoredFile Store(string fileName, Stream content)
    {
        var mime = MimeType.FromFileName(fileName);
        var seekable = content.CanSeek ? content : CopyToMemory(content);

        seekable.Position = 0;
        var size = FileSize.Of(seekable.Length);
        policy.EnsureSizeAllowed(size);

        var checksum = checksums.Compute(seekable);
        seekable.Position = 0;
        var key = storage.Save(seekable);

        return new StoredFile(key, Path.GetFileName(fileName), mime, size, checksum);
    }

    private static MemoryStream CopyToMemory(Stream content)
    {
        var memory = new MemoryStream();
        content.CopyTo(memory);
        return memory;
    }
}
