using Ged.Application.Ports;
using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Infrastructure.InMemory;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly Dictionary<Guid, Document> _documents = new();

    public Document? FindById(Guid id) => _documents.GetValueOrDefault(id);

    public void Save(Document document) => _documents[document.Id] = document;

    public void Delete(Guid id) => _documents.Remove(id);

    public bool TitleExistsInFolder(Guid? folderId, DocumentTitle title, Guid? excludeId = null)
        => _documents.Values.Any(d =>
            d.Status == DocumentStatus.Active && d.FolderId == folderId && d.Title.SameAs(title) && d.Id != excludeId);

    public bool AnyInFolder(Guid folderId) => _documents.Values.Any(d => d.FolderId == folderId);

    public IReadOnlyList<Document> Search(SearchCriteria c)
        => _documents.Values
            .Where(d => d.Status == c.Status)
            .Where(d => c.Text is null || d.Title.Normalized.Contains(c.Text.ToLowerInvariant()))
            .Where(d => c.Tag is null || d.Tags.Contains(c.Tag))
            .Where(d => c.Type is null || d.CurrentVersion.File.MimeType == c.Type)
            .Where(d => c.FolderId is null || d.FolderId == c.FolderId)
            .OrderBy(d => d.Title.Normalized)
            .ToList();
}
