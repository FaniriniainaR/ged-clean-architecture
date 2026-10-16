using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Application.Ports;

/// <summary>Critères de recherche ; un critère null est ignoré.</summary>
public sealed record SearchCriteria(
    string? Text = null,
    Tag? Tag = null,
    MimeType? Type = null,
    Guid? FolderId = null,
    DocumentStatus Status = DocumentStatus.Active);

/// <summary>Port de persistance des documents. Le noyau ne sait pas quelle base est derrière.</summary>
public interface IDocumentRepository
{
    Document? FindById(Guid id);
    void Save(Document document);
    void Delete(Guid id);

    /// <summary>Vrai si un document actif porte déjà ce titre dans ce dossier.</summary>
    bool TitleExistsInFolder(Guid? folderId, DocumentTitle title, Guid? excludeId = null);

    /// <summary>Vrai si le dossier contient au moins un document (actif ou dans la corbeille).</summary>
    bool AnyInFolder(Guid folderId);

    IReadOnlyList<Document> Search(SearchCriteria criteria);
}
