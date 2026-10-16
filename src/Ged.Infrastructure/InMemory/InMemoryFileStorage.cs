using Ged.Application.Ports;

namespace Ged.Infrastructure.InMemory;

public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new();

    public int Count => _files.Count;

    public string Save(Stream content)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        var key = Guid.NewGuid().ToString("N");
        _files[key] = memory.ToArray();
        return key;
    }

    public Stream Open(string storageKey) => new MemoryStream(_files[storageKey], writable: false);

    public void Delete(string storageKey) => _files.Remove(storageKey);
}
