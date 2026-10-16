namespace Ged.Application.Ports;

/// <summary>Stockage des octets des fichiers (disque local, S3...).</summary>
public interface IFileStorage
{
    /// <summary>Écrit le contenu et renvoie une clé unique.</summary>
    string Save(Stream content);
    Stream Open(string storageKey);
    void Delete(string storageKey);
}
