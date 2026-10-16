using Ged.Application.Common;
using Ged.Application.Dtos;
using Ged.Application.Ports;
using Ged.Domain.Common;
using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Application.UseCases;

public sealed class CreateFolder(IFolderRepository folders, IClock clock)
{
    public FolderDto Execute(string name, Guid? parentId = null)
    {
        if (parentId is { } id && folders.FindById(id) is null)
            throw new NotFoundException("Dossier", id);

        var folder = Folder.Create(Guid.NewGuid(), FolderName.Of(name), parentId, clock.UtcNow);
        folders.Save(folder);
        return FolderDto.From(folder);
    }
}

public sealed class ListFolders(IFolderRepository folders)
{
    public IReadOnlyList<FolderDto> Execute() => folders.FindAll().Select(FolderDto.From).ToList();
}

public sealed class DeleteFolder(IFolderRepository folders, IDocumentRepository documents)
{
    public void Execute(Guid folderId)
    {
        var folder = folders.FindById(folderId) ?? throw new NotFoundException("Dossier", folderId);
        if (folders.HasChildren(folderId) || documents.AnyInFolder(folderId))
            throw new FolderNotEmptyException(folder.Name.Value);
        folders.Delete(folderId);
    }
}
