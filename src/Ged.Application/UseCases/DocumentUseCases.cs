using Ged.Application.Common;
using Ged.Application.Dtos;
using Ged.Application.Ports;
using Ged.Domain.Common;
using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Application.UseCases;

public sealed record UploadDocumentCommand(
    string FileName, Stream Content, string? Title = null, Guid? FolderId = null,
    IReadOnlyCollection<string>? Tags = null);

public sealed class UploadDocument(
    IDocumentRepository documents, IFolderRepository folders, IFileStorage storage, FileIntake intake, IClock clock)
{
    public DocumentDto Execute(UploadDocumentCommand command)
    {
        var title = DocumentTitle.Of(string.IsNullOrWhiteSpace(command.Title)
            ? Path.GetFileNameWithoutExtension(command.FileName)
            : command.Title);
        var tags = (command.Tags ?? []).Select(Tag.Of).ToList();

        if (command.FolderId is { } folderId && folders.FindById(folderId) is null)
            throw new NotFoundException("Dossier", folderId);
        if (documents.TitleExistsInFolder(command.FolderId, title))
            throw new DuplicateTitleException(title.Value);

        var file = intake.Store(command.FileName, command.Content);
        try
        {
            var document = Document.Create(Guid.NewGuid(), title, command.FolderId, tags, file, clock.UtcNow);
            documents.Save(document);
            return DocumentDto.From(document);
        }
        catch
        {
            storage.Delete(file.StorageKey); // pas de fichier orphelin si l'enregistrement échoue
            throw;
        }
    }
}

public sealed class AddVersion(IDocumentRepository documents, IFileStorage storage, FileIntake intake, IClock clock)
{
    public DocumentDto Execute(Guid documentId, string fileName, Stream content)
    {
        var document = documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId);
        document.EnsureActive();

        var file = intake.Store(fileName, content);
        try
        {
            document.AddVersion(file, clock.UtcNow);
            documents.Save(document);
            return DocumentDto.From(document);
        }
        catch
        {
            storage.Delete(file.StorageKey);
            throw;
        }
    }
}

public sealed class DownloadDocument(IDocumentRepository documents, IFileStorage storage)
{
    /// <param name="versionNumber">null = version courante.</param>
    public DownloadResult Execute(Guid documentId, int? versionNumber = null)
    {
        var document = documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId);
        document.EnsureActive();

        var version = versionNumber is { } n ? document.GetVersion(n) : document.CurrentVersion;
        return new DownloadResult(
            version.File.OriginalFileName, version.File.MimeType.Value, storage.Open(version.File.StorageKey));
    }
}

public sealed class GetDocument(IDocumentRepository documents)
{
    public DocumentDto Execute(Guid documentId)
        => DocumentDto.From(documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId));
}

public sealed class TagDocument(IDocumentRepository documents)
{
    public DocumentDto Execute(Guid documentId, IEnumerable<string>? add = null, IEnumerable<string>? remove = null)
    {
        var document = documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId);
        foreach (var tag in add ?? []) document.AddTag(Tag.Of(tag));
        foreach (var tag in remove ?? []) document.RemoveTag(Tag.Of(tag));
        documents.Save(document);
        return DocumentDto.From(document);
    }
}

public sealed class RenameDocument(IDocumentRepository documents)
{
    public DocumentDto Execute(Guid documentId, string newTitle)
    {
        var document = documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId);
        var title = DocumentTitle.Of(newTitle);
        if (documents.TitleExistsInFolder(document.FolderId, title, excludeId: document.Id))
            throw new DuplicateTitleException(title.Value);

        document.Rename(title);
        documents.Save(document);
        return DocumentDto.From(document);
    }
}

public sealed class MoveDocument(IDocumentRepository documents, IFolderRepository folders)
{
    /// <param name="targetFolderId">null = racine.</param>
    public DocumentDto Execute(Guid documentId, Guid? targetFolderId)
    {
        var document = documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId);
        if (targetFolderId is { } folderId && folders.FindById(folderId) is null)
            throw new NotFoundException("Dossier", folderId);
        if (documents.TitleExistsInFolder(targetFolderId, document.Title, excludeId: document.Id))
            throw new DuplicateTitleException(document.Title.Value);

        document.MoveTo(targetFolderId);
        documents.Save(document);
        return DocumentDto.From(document);
    }
}

public sealed record SearchQuery(
    string? Text = null, string? Tag = null, string? Type = null, Guid? FolderId = null, bool InTrash = false);

public sealed class SearchDocuments(IDocumentRepository documents)
{
    public IReadOnlyList<DocumentDto> Execute(SearchQuery query)
    {
        var criteria = new SearchCriteria(
            Text: string.IsNullOrWhiteSpace(query.Text) ? null : query.Text.Trim(),
            Tag: string.IsNullOrWhiteSpace(query.Tag) ? null : Tag.Of(query.Tag),
            Type: string.IsNullOrWhiteSpace(query.Type) ? null : MimeType.FromExtension(query.Type),
            FolderId: query.FolderId,
            Status: query.InTrash ? DocumentStatus.Trashed : DocumentStatus.Active);

        return documents.Search(criteria).Select(DocumentDto.From).ToList();
    }
}

public sealed class TrashDocument(IDocumentRepository documents, IClock clock)
{
    public void Execute(Guid documentId)
    {
        var document = documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId);
        document.Trash(clock.UtcNow);
        documents.Save(document);
    }
}

public sealed class RestoreDocument(IDocumentRepository documents)
{
    public DocumentDto Execute(Guid documentId)
    {
        var document = documents.FindById(documentId) ?? throw new NotFoundException("Document", documentId);
        if (documents.TitleExistsInFolder(document.FolderId, document.Title, excludeId: document.Id))
            throw new DuplicateTitleException(document.Title.Value);

        document.Restore();
        documents.Save(document);
        return DocumentDto.From(document);
    }
}

/// <summary>Supprime définitivement les documents restés trop longtemps dans la corbeille.</summary>
public sealed class PurgeTrash(IDocumentRepository documents, IFileStorage storage, IClock clock, DocumentPolicy policy)
{
    public int Execute()
    {
        var now = clock.UtcNow;
        var purgeable = documents.Search(new SearchCriteria(Status: DocumentStatus.Trashed))
            .Where(d => d.IsPurgeable(now, policy))
            .ToList();

        foreach (var document in purgeable)
        {
            documents.Delete(document.Id);
            foreach (var version in document.Versions)
                storage.Delete(version.File.StorageKey);
        }
        return purgeable.Count;
    }
}
