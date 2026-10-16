using System.Data.Common;
using Ged.Application.Common;
using Ged.Application.Dtos;
using Ged.Application.UseCases;
using Ged.Cli;
using Ged.Domain.Common;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintHelp();
    return 0;
}

try
{
    var app = new CompositionRoot(AppConfig.Load());
    var a = new Arguments(args.Skip(1));

    switch (args[0])
    {
        case "upload":
        {
            var path = a.At(0, "fichier");
            using var file = File.OpenRead(path);
            var doc = app.UploadDocument.Execute(new UploadDocumentCommand(
                Path.GetFileName(path), file, a.Option("title"), a.IdOption("folder"), a.Options("tag")));
            Console.WriteLine($"Document ajouté : {doc.Id}");
            PrintDocument(doc);
            break;
        }
        case "version":
        {
            var path = a.At(1, "fichier");
            using var file = File.OpenRead(path);
            var doc = app.AddVersion.Execute(a.IdAt(0), Path.GetFileName(path), file);
            Console.WriteLine($"Version {doc.CurrentVersion.Number} ajoutée.");
            break;
        }
        case "download":
        {
            var result = app.DownloadDocument.Execute(a.IdAt(0), a.IntOption("version"));
            var destination = a.OptionalAt(1) ?? result.FileName;
            using (result.Content)
            using (var output = File.Create(destination))
                result.Content.CopyTo(output);
            Console.WriteLine($"Fichier enregistré : {destination}");
            break;
        }
        case "show":
            PrintDocument(app.GetDocument.Execute(a.IdAt(0)));
            break;
        case "tag":
            PrintDocument(app.TagDocument.Execute(a.IdAt(0), a.Options("add"), a.Options("remove")));
            break;
        case "rename":
            PrintDocument(app.RenameDocument.Execute(a.IdAt(0), a.At(1, "titre")));
            break;
        case "move":
            PrintDocument(app.MoveDocument.Execute(a.IdAt(0), a.IdOption("folder")));
            break;
        case "search":
        case "list":
            PrintList(app.SearchDocuments.Execute(new SearchQuery(
                a.OptionalAt(0), a.Option("tag"), a.Option("type"), a.IdOption("folder"), a.Has("trash"))));
            break;
        case "trash":
            app.TrashDocument.Execute(a.IdAt(0));
            Console.WriteLine("Document placé dans la corbeille.");
            break;
        case "restore":
            app.RestoreDocument.Execute(a.IdAt(0));
            Console.WriteLine("Document restauré.");
            break;
        case "purge":
            Console.WriteLine($"{app.PurgeTrash.Execute()} document(s) supprimé(s) définitivement.");
            break;
        case "folder":
            RunFolder(app, a);
            break;
        default:
            Console.Error.WriteLine($"Commande inconnue : {args[0]}. Tapez « ged help ».");
            return 1;
    }
    return 0;
}
catch (Exception e) when (e is DomainException or NotFoundException or ArgumentException or IOException)
{
    Console.Error.WriteLine($"Erreur : {e.Message}");
    return 1;
}
catch (DbException e)
{
    Console.Error.WriteLine($"Erreur de base de données : {e.Message}");
    return 2;
}

static void RunFolder(CompositionRoot app, Arguments a)
{
    switch (a.At(0, "create|list|delete"))
    {
        case "create":
            var folder = app.CreateFolder.Execute(a.At(1, "nom"), a.IdOption("parent"));
            Console.WriteLine($"Dossier créé : {folder.Id}  {folder.Name}");
            break;
        case "list":
            var folders = app.ListFolders.Execute();
            if (folders.Count == 0) Console.WriteLine("Aucun dossier.");
            foreach (var f in folders)
                Console.WriteLine($"{f.Id}  {f.Name}{(f.ParentId is { } p ? $"  (parent {p})" : "")}");
            break;
        case "delete":
            app.DeleteFolder.Execute(a.IdAt(1));
            Console.WriteLine("Dossier supprimé.");
            break;
        default:
            throw new ArgumentException("Sous-commande attendue : create, list ou delete.");
    }
}

static void PrintDocument(DocumentDto d)
{
    Console.WriteLine($"""
          Id       : {d.Id}
          Titre    : {d.Title}
          Dossier  : {d.FolderId?.ToString() ?? "(racine)"}
          Statut   : {d.Status}{(d.TrashedAt is { } t ? $" depuis le {t:yyyy-MM-dd}" : "")}
          Tags     : {(d.Tags.Count == 0 ? "-" : string.Join(", ", d.Tags))}
          Versions :
        """);
    foreach (var v in d.Versions)
        Console.WriteLine($"    v{v.Number}  {v.CreatedAt:yyyy-MM-dd HH:mm}  {v.FileName}  {v.Size}  {v.Checksum[..12]}…");
}

static void PrintList(IReadOnlyList<DocumentDto> docs)
{
    if (docs.Count == 0)
    {
        Console.WriteLine("Aucun document.");
        return;
    }
    foreach (var d in docs)
    {
        var tags = d.Tags.Count == 0 ? "" : $"  [{string.Join(", ", d.Tags)}]";
        Console.WriteLine($"{d.Id}  {d.Title,-30}  v{d.CurrentVersion.Number}  {d.CurrentVersion.Size,8}{tags}");
    }
    Console.WriteLine($"{docs.Count} document(s).");
}

static void PrintHelp() => Console.WriteLine("""
    GED - gestion électronique de documents

    Documents
      ged upload <fichier> [--title T] [--folder ID] [--tag X]...   Ajouter un document
      ged version <id> <fichier>                                    Ajouter une nouvelle version
      ged download <id> [destination] [--version N]                 Télécharger (version courante par défaut)
      ged show <id>                                                 Détails et historique des versions
      ged tag <id> [--add X]... [--remove Y]...                     Gérer les tags
      ged rename <id> <titre>                                       Renommer
      ged move <id> [--folder ID]                                   Déplacer (sans --folder : racine)
      ged search [texte] [--tag X] [--type pdf] [--folder ID] [--trash]
      ged trash <id> | ged restore <id> | ged purge                 Corbeille

    Dossiers
      ged folder create <nom> [--parent ID]
      ged folder list
      ged folder delete <id>

    La base de données se choisit dans ged.config.json (ou le fichier indiqué par GED_CONFIG).
    """);
