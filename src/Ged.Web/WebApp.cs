using System.Data.Common;
using System.Net;
using System.Text;
using Ged.Application.Common;
using Ged.Application.UseCases;
using Ged.Bootstrap;
using Ged.Domain.Common;

namespace Ged.Web;

/// <summary>
/// Routage des requêtes HTTP vers les cas d'utilisation. Comme la CLI, cette couche ne contient
/// aucune règle métier : elle traduit HTTP ⇄ cas d'utilisation.
/// </summary>
public sealed class WebApp(CompositionRoot app)
{
    private long MaxBodyBytes => app.Policy.MaxFileSizeBytes + 1024 * 1024; // + marge pour les champs du formulaire

    public void Handle(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var segments = request.Url!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var back = "/"; // page où revenir après un formulaire (succès ou erreur)

        try
        {
            switch (request.HttpMethod, segments)
            {
                case ("GET", []):
                    var folderId = OptionalId(request.QueryString["folder"]);
                    var query = new SearchQuery(
                        request.QueryString["q"], request.QueryString["tag"], request.QueryString["type"], folderId);
                    Page(context, "Documents", Pages.Home(
                        app.SearchDocuments.Execute(query), app.ListFolders.Execute(),
                        query.Text, query.Tag, query.Type, folderId, app.Policy.MaxFileSizeBytes / (1024 * 1024)));
                    break;

                case ("GET", ["documents", var id]):
                    var document = app.GetDocument.Execute(Id(id));
                    Page(context, document.Title, Pages.Document(document, app.ListFolders.Execute()));
                    break;

                case ("GET", ["documents", var id, "download"]):
                    Download(response, Id(id), request.QueryString["version"]);
                    break;

                case ("POST", ["documents"]):
                {
                    var form = FormData.Read(request, MaxBodyBytes);
                    var file = form.File("file") ?? throw new ArgumentException("Choisissez un fichier.");
                    var created = app.UploadDocument.Execute(new UploadDocumentCommand(
                        file.FileName, new MemoryStream(file.Content), form["title"], OptionalId(form["folder"]),
                        SplitTags(form["tags"])));
                    Redirect(response, $"/documents/{created.Id}", ok: "Document ajouté.");
                    break;
                }

                case ("POST", ["documents", var id, "versions"]):
                {
                    back = $"/documents/{id}";
                    var form = FormData.Read(request, MaxBodyBytes);
                    var file = form.File("file") ?? throw new ArgumentException("Choisissez un fichier.");
                    var updated = app.AddVersion.Execute(Id(id), file.FileName, new MemoryStream(file.Content));
                    Redirect(response, back, ok: $"Version {updated.CurrentVersion.Number} ajoutée.");
                    break;
                }

                case ("POST", ["documents", var id, "tags"]):
                {
                    back = $"/documents/{id}";
                    var form = FormData.Read(request, MaxBodyBytes);
                    var remove = form["remove"] is { } r ? new[] { r } : [];
                    app.TagDocument.Execute(Id(id), SplitTags(form["add"]), remove);
                    Redirect(response, back, ok: "Tags mis à jour.");
                    break;
                }

                case ("POST", ["documents", var id, "rename"]):
                    back = $"/documents/{id}";
                    app.RenameDocument.Execute(Id(id), FormData.Read(request, MaxBodyBytes)["title"] ?? "");
                    Redirect(response, back, ok: "Document renommé.");
                    break;

                case ("POST", ["documents", var id, "move"]):
                    back = $"/documents/{id}";
                    app.MoveDocument.Execute(Id(id), OptionalId(FormData.Read(request, MaxBodyBytes)["folder"]));
                    Redirect(response, back, ok: "Document déplacé.");
                    break;

                case ("POST", ["documents", var id, "trash"]):
                    back = $"/documents/{id}";
                    app.TrashDocument.Execute(Id(id));
                    Redirect(response, "/trash", ok: "Document placé dans la corbeille.");
                    break;

                case ("POST", ["documents", var id, "restore"]):
                    back = "/trash";
                    app.RestoreDocument.Execute(Id(id));
                    Redirect(response, $"/documents/{id}", ok: "Document restauré.");
                    break;

                case ("GET", ["trash"]):
                    Page(context, "Corbeille", Pages.Trash(
                        app.SearchDocuments.Execute(new SearchQuery(InTrash: true)), app.Policy.TrashRetentionDays));
                    break;

                case ("POST", ["trash", "purge"]):
                    back = "/trash";
                    Redirect(response, back, ok: $"{app.PurgeTrash.Execute()} document(s) supprimé(s) définitivement.");
                    break;

                case ("GET", ["folders"]):
                    Page(context, "Dossiers", Pages.Folders(app.ListFolders.Execute()));
                    break;

                case ("POST", ["folders"]):
                {
                    back = "/folders";
                    var form = FormData.Read(request, MaxBodyBytes);
                    var folder = app.CreateFolder.Execute(form["name"] ?? "", OptionalId(form["parent"]));
                    Redirect(response, back, ok: $"Dossier « {folder.Name} » créé.");
                    break;
                }

                case ("POST", ["folders", var id, "delete"]):
                    back = "/folders";
                    app.DeleteFolder.Execute(Id(id));
                    Redirect(response, back, ok: "Dossier supprimé.");
                    break;

                default:
                    Page(context, "Introuvable", Pages.Error("Page introuvable."), 404);
                    break;
            }
        }
        catch (Exception e) when (e is DomainException or NotFoundException or ArgumentException)
        {
            if (request.HttpMethod == "POST")
                Redirect(response, back, error: e.Message);
            else
                Page(context, "Erreur", Pages.Error(e.Message), e is NotFoundException ? 404 : 400);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[{request.HttpMethod} {request.Url!.AbsolutePath}] {e}");
            var message = e is DbException ? $"Erreur de base de données : {e.Message}" : "Erreur interne du serveur.";
            try { Page(context, "Erreur", Pages.Error(message), 500); } catch { /* réponse déjà commencée */ }
        }
        finally
        {
            try { response.Close(); } catch { /* client déconnecté */ }
        }
    }

    private void Download(HttpListenerResponse response, Guid id, string? version)
    {
        int? number = int.TryParse(version, out var n) ? n : null;
        var file = app.DownloadDocument.Execute(id, number);
        using var content = file.Content;

        response.ContentType = file.MimeType;
        response.AddHeader("Content-Disposition",
            $"attachment; filename=\"{AsciiFileName(file.FileName)}\"; filename*=UTF-8''{Uri.EscapeDataString(file.FileName)}");
        if (content.CanSeek) response.ContentLength64 = content.Length;
        content.CopyTo(response.OutputStream);
    }

    private static void Page(HttpListenerContext context, string title, string body, int status = 200)
    {
        var query = context.Request.QueryString;
        var bytes = Encoding.UTF8.GetBytes(Html.Layout(title, body, query["ok"], query["err"]));
        var response = context.Response;
        response.StatusCode = status;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
    }

    // Après un formulaire : redirection (motif Post/Redirect/Get) avec un message à afficher.
    private static void Redirect(HttpListenerResponse response, string path, string? ok = null, string? error = null)
    {
        var message = ok is not null ? $"ok={Uri.EscapeDataString(ok)}" : $"err={Uri.EscapeDataString(error ?? "")}";
        response.StatusCode = 303;
        response.RedirectLocation = $"{path}{(path.Contains('?') ? '&' : '?')}{message}";
    }

    private static Guid Id(string value)
        => Guid.TryParse(value, out var id) ? id : throw new ArgumentException($"Identifiant invalide : {value}.");

    private static Guid? OptionalId(string? value) => string.IsNullOrWhiteSpace(value) ? null : Id(value);

    private static string[] SplitTags(string? value)
        => (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string AsciiFileName(string name)
        => new(name.Select(c => c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_').ToArray());
}
