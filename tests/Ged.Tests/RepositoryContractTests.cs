using Ged.Application.Ports;
using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Tests;

/// <summary>
/// Tests de « contrat » : exactement les mêmes tests sont lancés sur chaque implémentation
/// (mémoire, SQLite, MySQL, SQL Server). S'ils passent partout, la base est vraiment interchangeable.
/// </summary>
public static class RepositoryContractTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc);

    private static StoredFile File(string name, char hash)
        => new(Guid.NewGuid().ToString("N"), name, MimeType.FromFileName(name), FileSize.Of(1234),
            Checksum.Of(new string(hash, 64)));

    private static Document Doc(string title, Guid? folderId = null, string file = "a.pdf", params string[] tags)
        => Document.Create(Guid.NewGuid(), DocumentTitle.Of(title), folderId, tags.Select(Tag.Of), File(file, 'a'), Now);

    public static IEnumerable<TestCase> All(Func<(IDocumentRepository Docs, IFolderRepository Folders)> create) =>
    [
        new("Enregistrer puis relire un document complet", () =>
        {
            var (docs, _) = create();
            var doc = Doc("Facture été é", null, "facture.pdf", "Facture", "2026");
            doc.AddVersion(File("facture-v2.pdf", 'b'), Now.AddHours(1));
            docs.Save(doc);

            var loaded = docs.FindById(doc.Id)!;
            Check.Equal("Facture été é", loaded.Title.Value, "titre (accents)");
            Check.Equal(2, loaded.Versions.Count, "versions");
            Check.Equal("facture-v2.pdf", loaded.CurrentVersion.File.OriginalFileName, "fichier courant");
            Check.Equal(doc.CurrentVersion.File.Checksum, loaded.CurrentVersion.File.Checksum, "empreinte");
            Check.Equal(1234L, loaded.CurrentVersion.File.Size.Bytes, "taille");
            Check.Equal(Now, loaded.CreatedAt, "date de création");
            Check.Equal("2026,facture", string.Join(",", loaded.Tags.Select(t => t.Value)), "tags");
        }),

        new("Document inconnu = null", () => Check.True(create().Docs.FindById(Guid.NewGuid()) is null, "attendu null")),

        new("Mise à jour : corbeille et tags", () =>
        {
            var (docs, _) = create();
            var doc = Doc("Contrat", tags: "a");
            docs.Save(doc);
            doc.RemoveTag(Tag.Of("a"));
            doc.Trash(Now.AddDays(2));
            docs.Save(doc);

            var loaded = docs.FindById(doc.Id)!;
            Check.Equal(DocumentStatus.Trashed, loaded.Status, "statut");
            Check.Equal(Now.AddDays(2), loaded.TrashedAt, "date de mise en corbeille");
            Check.Equal(0, loaded.Tags.Count, "tags");
        }),

        new("Titre existant : insensible à la casse, par dossier, ignore la corbeille", () =>
        {
            var (docs, folders) = create();
            var folder = Folder.Create(Guid.NewGuid(), FolderName.Of("F"), null, Now);
            folders.Save(folder);
            var doc = Doc("Bilan");
            docs.Save(doc);

            Check.True(docs.TitleExistsInFolder(null, DocumentTitle.Of("BILAN")), "même dossier (racine)");
            Check.True(!docs.TitleExistsInFolder(folder.Id, DocumentTitle.Of("Bilan")), "autre dossier");
            Check.True(!docs.TitleExistsInFolder(null, DocumentTitle.Of("Bilan"), excludeId: doc.Id), "exclu");

            doc.Trash(Now);
            docs.Save(doc);
            Check.True(!docs.TitleExistsInFolder(null, DocumentTitle.Of("Bilan")), "dans la corbeille");
        }),

        new("Recherche combinée", () =>
        {
            var (docs, folders) = create();
            var folder = Folder.Create(Guid.NewGuid(), FolderName.Of("Factures"), null, Now);
            folders.Save(folder);
            docs.Save(Doc("Facture EDF", folder.Id, "edf.pdf", "facture"));
            docs.Save(Doc("Facture eau", null, "eau.png", "facture"));
            docs.Save(Doc("Vacances", null, "photo.png", "perso"));

            Check.Equal(2, docs.Search(new SearchCriteria(Text: "FACTURE")).Count, "texte");
            Check.Equal(2, docs.Search(new SearchCriteria(Tag: Tag.Of("facture"))).Count, "tag");
            Check.Equal(2, docs.Search(new SearchCriteria(Type: MimeType.FromExtension("png"))).Count, "type");
            Check.Equal(1, docs.Search(new SearchCriteria(FolderId: folder.Id)).Count, "dossier");
            Check.Equal(1, docs.Search(new SearchCriteria(Text: "facture", Type: MimeType.FromExtension("png"))).Count, "texte + type");
            Check.Equal("Facture eau", docs.Search(new SearchCriteria())[0].Title.Value, "tri par titre");
        }),

        new("Recherche par type : seule la version courante compte", () =>
        {
            var (docs, _) = create();
            var doc = Doc("Scan", file: "scan.png");
            doc.AddVersion(File("scan.pdf", 'b'), Now);
            docs.Save(doc);
            Check.Equal(0, docs.Search(new SearchCriteria(Type: MimeType.FromExtension("png"))).Count, "png");
            Check.Equal(1, docs.Search(new SearchCriteria(Type: MimeType.FromExtension("pdf"))).Count, "pdf");
        }),

        new("Suppression définitive", () =>
        {
            var (docs, _) = create();
            var doc = Doc("A supprimer", tags: "x");
            docs.Save(doc);
            docs.Delete(doc.Id);
            Check.True(docs.FindById(doc.Id) is null, "doit être supprimé");
        }),

        new("Dossiers : enregistrer, lister, enfants, supprimer", () =>
        {
            var (docs, folders) = create();
            var parent = Folder.Create(Guid.NewGuid(), FolderName.Of("Parent"), null, Now);
            var child = Folder.Create(Guid.NewGuid(), FolderName.Of("Enfant"), parent.Id, Now);
            folders.Save(parent);
            folders.Save(child);

            Check.Equal(parent.Id, folders.FindById(child.Id)!.ParentId, "parent");
            Check.Equal(2, folders.FindAll().Count, "nombre");
            Check.True(folders.HasChildren(parent.Id), "parent a des enfants");
            Check.True(!folders.HasChildren(child.Id), "enfant sans enfants");

            docs.Save(Doc("Dedans", child.Id));
            Check.True(docs.AnyInFolder(child.Id), "dossier contient un document");

            folders.Delete(parent.Id);
            Check.True(folders.FindById(parent.Id) is null, "supprimé");
        }),
    ];
}
