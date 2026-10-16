using System.Text;
using Ged.Application.Common;
using Ged.Application.UseCases;
using Ged.Domain.Common;
using Ged.Domain.ValueObjects;
using Ged.Infrastructure.InMemory;
using Ged.Infrastructure.Services;

namespace Ged.Tests;

/// <summary>Cas d'utilisation testés avec des adaptateurs en mémoire : ni disque, ni base de données.</summary>
public static class UseCaseTests
{
    private sealed class Sut
    {
        public readonly InMemoryDocumentRepository Documents = new();
        public readonly InMemoryFolderRepository Folders = new();
        public readonly InMemoryFileStorage Storage = new();
        public readonly FakeClock Clock = new(new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc));
        public readonly DocumentPolicy Policy = new(MaxFileSizeBytes: 100, TrashRetentionDays: 30);
        public readonly FileIntake Intake;

        public Sut() => Intake = new FileIntake(Storage, new Sha256ChecksumCalculator(), Policy);

        public UploadDocument Upload => new(Documents, Folders, Storage, Intake, Clock);
        public AddVersion AddVersion => new(Documents, Storage, Intake, Clock);
        public DownloadDocument Download => new(Documents, Storage);
        public SearchDocuments Search => new(Documents);
        public TrashDocument Trash => new(Documents, Clock);
        public RestoreDocument Restore => new(Documents);
        public PurgeTrash Purge => new(Documents, Storage, Clock, Policy);
        public MoveDocument Move => new(Documents, Folders);
        public CreateFolder CreateFolder => new(Folders, Clock);
        public DeleteFolder DeleteFolder => new(Folders, Documents);
    }

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));

    public static IEnumerable<TestCase> All() =>
    [
        new("Upload : titre déduit du nom de fichier", () =>
        {
            var sut = new Sut();
            var doc = sut.Upload.Execute(new UploadDocumentCommand("facture-octobre.pdf", Content("pdf")));
            Check.Equal("facture-octobre", doc.Title, "titre");
            Check.Equal(1, sut.Storage.Count, "fichiers stockés");
        }),

        new("Upload : même titre dans le même dossier refusé, sans fichier orphelin", () =>
        {
            var sut = new Sut();
            sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1"), Title: "Contrat"));
            Check.Throws<DuplicateTitleException>(() =>
                sut.Upload.Execute(new UploadDocumentCommand("b.pdf", Content("2"), Title: "contrat")));
            Check.Equal(1, sut.Storage.Count, "fichiers stockés");
        }),

        new("Upload : même titre dans un autre dossier accepté", () =>
        {
            var sut = new Sut();
            var folder = sut.CreateFolder.Execute("2026");
            sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1"), Title: "Contrat"));
            sut.Upload.Execute(new UploadDocumentCommand("b.pdf", Content("2"), Title: "Contrat", FolderId: folder.Id));
        }),

        new("Upload : fichier trop gros refusé avant d'être stocké", () =>
        {
            var sut = new Sut();
            Check.Throws<FileTooLargeException>(() =>
                sut.Upload.Execute(new UploadDocumentCommand("gros.pdf", Content(new string('x', 101)))));
            Check.Equal(0, sut.Storage.Count, "fichiers stockés");
        }),

        new("Upload : dossier inexistant refusé", () =>
        {
            var sut = new Sut();
            Check.Throws<NotFoundException>(() =>
                sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1"), FolderId: Guid.NewGuid())));
        }),

        new("Nouvelle version identique refusée et fichier supprimé", () =>
        {
            var sut = new Sut();
            var doc = sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("même contenu")));
            Check.Throws<DuplicateVersionException>(() => sut.AddVersion.Execute(doc.Id, "a.pdf", Content("même contenu")));
            Check.Equal(1, sut.Storage.Count, "fichiers stockés");
        }),

        new("Téléchargement d'une ancienne version", () =>
        {
            var sut = new Sut();
            var doc = sut.Upload.Execute(new UploadDocumentCommand("a.txt", Content("v1")));
            sut.AddVersion.Execute(doc.Id, "a.txt", Content("v2"));

            using var current = new StreamReader(sut.Download.Execute(doc.Id).Content);
            using var first = new StreamReader(sut.Download.Execute(doc.Id, 1).Content);
            Check.Equal("v2", current.ReadToEnd(), "version courante");
            Check.Equal("v1", first.ReadToEnd(), "version 1");
        }),

        new("Recherche par texte, tag et type", () =>
        {
            var sut = new Sut();
            sut.Upload.Execute(new UploadDocumentCommand("Facture EDF.pdf", Content("1"), Tags: ["Facture"]));
            sut.Upload.Execute(new UploadDocumentCommand("Photo.png", Content("2"), Tags: ["perso"]));
            Check.Equal(1, sut.Search.Execute(new SearchQuery(Text: "edf")).Count, "texte");
            Check.Equal(1, sut.Search.Execute(new SearchQuery(Tag: "FACTURE")).Count, "tag");
            Check.Equal(1, sut.Search.Execute(new SearchQuery(Type: "png")).Count, "type");
            Check.Equal(2, sut.Search.Execute(new SearchQuery()).Count, "tout");
        }),

        new("Corbeille : document caché puis restauré", () =>
        {
            var sut = new Sut();
            var doc = sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1")));
            sut.Trash.Execute(doc.Id);
            Check.Equal(0, sut.Search.Execute(new SearchQuery()).Count, "actifs");
            Check.Equal(1, sut.Search.Execute(new SearchQuery(InTrash: true)).Count, "corbeille");
            sut.Restore.Execute(doc.Id);
            Check.Equal(1, sut.Search.Execute(new SearchQuery()).Count, "actifs après restauration");
        }),

        new("Purge après 30 jours : document et fichiers supprimés", () =>
        {
            var sut = new Sut();
            var doc = sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1")));
            sut.AddVersion.Execute(doc.Id, "a.pdf", Content("2"));
            sut.Trash.Execute(doc.Id);

            sut.Clock.Advance(TimeSpan.FromDays(10));
            Check.Equal(0, sut.Purge.Execute(), "purgés à 10 jours");

            sut.Clock.Advance(TimeSpan.FromDays(21));
            Check.Equal(1, sut.Purge.Execute(), "purgés à 31 jours");
            Check.Equal(0, sut.Storage.Count, "fichiers restants");
        }),

        new("Dossier non vide : suppression refusée", () =>
        {
            var sut = new Sut();
            var folder = sut.CreateFolder.Execute("Factures");
            sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1"), FolderId: folder.Id));
            Check.Throws<FolderNotEmptyException>(() => sut.DeleteFolder.Execute(folder.Id));
        }),

        new("Déplacement vers un dossier où le titre existe déjà refusé", () =>
        {
            var sut = new Sut();
            var folder = sut.CreateFolder.Execute("Archives");
            sut.Upload.Execute(new UploadDocumentCommand("a.pdf", Content("1"), Title: "Bilan", FolderId: folder.Id));
            var doc = sut.Upload.Execute(new UploadDocumentCommand("b.pdf", Content("2"), Title: "Bilan"));
            Check.Throws<DuplicateTitleException>(() => sut.Move.Execute(doc.Id, folder.Id));
        }),
    ];
}
