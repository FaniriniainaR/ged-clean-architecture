using Ged.Domain.Common;
using Ged.Domain.Entities;
using Ged.Domain.ValueObjects;

namespace Ged.Tests;

public static class DomainTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc);

    private static StoredFile File(char hashChar = 'a', string name = "facture.pdf")
        => new(Guid.NewGuid().ToString("N"), name, MimeType.FromFileName(name), FileSize.Of(1000),
            Checksum.Of(new string(hashChar, 64)));

    private static Document NewDocument()
        => Document.Create(Guid.NewGuid(), DocumentTitle.Of("Facture"), null, [], File(), Now);

    public static IEnumerable<TestCase> All() =>
    [
        new("Tag normalisé : ' Facture ' == 'FACTURE'", () =>
            Check.Equal(Tag.Of(" Facture "), Tag.Of("FACTURE"))),

        new("Tag vide refusé", () => Check.Throws<InvalidValueException>(() => Tag.Of("   "))),

        new("Titre vide refusé", () => Check.Throws<InvalidValueException>(() => DocumentTitle.Of(" "))),

        new("Titre trop long refusé", () =>
            Check.Throws<InvalidValueException>(() => DocumentTitle.Of(new string('x', 201)))),

        new("Fichier .exe refusé", () =>
            Check.Throws<UnsupportedFileTypeException>(() => MimeType.FromFileName("virus.exe"))),

        new("Fichier sans extension refusé", () =>
            Check.Throws<UnsupportedFileTypeException>(() => MimeType.FromFileName("README"))),

        new("Extension en majuscules acceptée", () =>
            Check.Equal("application/pdf", MimeType.FromFileName("SCAN.PDF").Value)),

        new("Fichier vide refusé", () => Check.Throws<InvalidValueException>(() => FileSize.Of(0))),

        new("Fichier de 25 Mo refusé (max 20 Mo)", () =>
            Check.Throws<FileTooLargeException>(() =>
                DocumentPolicy.Default.EnsureSizeAllowed(FileSize.Of(25L * 1024 * 1024)))),

        new("Empreinte invalide refusée", () => Check.Throws<InvalidValueException>(() => Checksum.Of("abc"))),

        new("Nouveau document = version 1", () =>
        {
            var doc = NewDocument();
            Check.Equal(1, doc.Versions.Count, "nombre de versions");
            Check.Equal(1, doc.CurrentVersion.Number, "version courante");
        }),

        new("Ajouter une version incrémente le numéro", () =>
        {
            var doc = NewDocument();
            doc.AddVersion(File('b'), Now.AddDays(1));
            Check.Equal(2, doc.CurrentVersion.Number, "version courante");
            Check.Equal(2, doc.Versions.Count, "nombre de versions");
        }),

        new("Version identique à la version courante refusée", () =>
        {
            var doc = NewDocument();
            Check.Throws<DuplicateVersionException>(() => doc.AddVersion(File('a'), Now));
        }),

        new("Document dans la corbeille non modifiable", () =>
        {
            var doc = NewDocument();
            doc.Trash(Now);
            Check.Throws<DocumentTrashedException>(() => doc.AddTag(Tag.Of("x")));
            Check.Throws<DocumentTrashedException>(() => doc.AddVersion(File('b'), Now));
        }),

        new("Restaurer un document actif refusé", () =>
            Check.Throws<DocumentNotTrashedException>(() => NewDocument().Restore())),

        new("Purge : 31 jours oui, 10 jours non", () =>
        {
            var doc = NewDocument();
            doc.Trash(Now);
            Check.True(!doc.IsPurgeable(Now.AddDays(10), DocumentPolicy.Default), "10 jours : ne doit pas être purgé");
            Check.True(doc.IsPurgeable(Now.AddDays(31), DocumentPolicy.Default), "31 jours : doit être purgé");
        }),

        new("Un tag ajouté deux fois n'apparaît qu'une fois", () =>
        {
            var doc = NewDocument();
            doc.AddTag(Tag.Of("Facture"));
            doc.AddTag(Tag.Of("facture"));
            Check.Equal(1, doc.Tags.Count, "nombre de tags");
        }),

        new("Nom de dossier avec '/' refusé", () => Check.Throws<InvalidValueException>(() => FolderName.Of("a/b"))),
    ];
}
