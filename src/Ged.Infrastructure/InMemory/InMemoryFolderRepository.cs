using Ged.Application.Ports;
using Ged.Domain.Entities;

namespace Ged.Infrastructure.InMemory;

public sealed class InMemoryFolderRepository : IFolderRepository
{
    private readonly Dictionary<Guid, Folder> _folders = new();

    public Folder? FindById(Guid id) => _folders.GetValueOrDefault(id);

    public IReadOnlyList<Folder> FindAll() => _folders.Values.OrderBy(f => f.Name.Value.ToLowerInvariant()).ToList();

    public void Save(Folder folder) => _folders[folder.Id] = folder;

    public void Delete(Guid id) => _folders.Remove(id);

    public bool HasChildren(Guid id) => _folders.Values.Any(f => f.ParentId == id);
}
