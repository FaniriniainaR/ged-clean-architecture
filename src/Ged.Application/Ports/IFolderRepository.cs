using Ged.Domain.Entities;

namespace Ged.Application.Ports;

public interface IFolderRepository
{
    Folder? FindById(Guid id);
    IReadOnlyList<Folder> FindAll();
    void Save(Folder folder);
    void Delete(Guid id);
    bool HasChildren(Guid id);
}
