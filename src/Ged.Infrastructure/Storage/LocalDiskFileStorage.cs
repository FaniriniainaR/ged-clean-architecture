using Ged.Application.Ports;

namespace Ged.Infrastructure.Storage;

/// <summary>Stocke chaque fichier sur le disque : racine/ab/abcdef... (2 premiers caractères = sous-dossier).</summary>
public sealed class LocalDiskFileStorage(string rootPath) : IFileStorage
{
    public string Save(Stream content)
    {
        var key = Guid.NewGuid().ToString("N");
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        content.CopyTo(file);
        return key;
    }

    public Stream Open(string storageKey)
    {
        var path = PathFor(storageKey);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fichier absent du stockage : {storageKey}.");
        return File.OpenRead(path);
    }

    public void Delete(string storageKey)
    {
        var path = PathFor(storageKey);
        if (File.Exists(path)) File.Delete(path);
    }

    private string PathFor(string key)
    {
        if (key.Length < 3 || !key.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException($"Clé de stockage invalide : {key}.");
        return Path.Combine(rootPath, key[..2], key);
    }
}
