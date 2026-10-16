using Ged.Application.Dtos;
using Ged.Domain.ValueObjects;
using static Ged.Web.Html;

namespace Ged.Web;

/// <summary>Génération des pages HTML à partir des DTO renvoyés par les cas d'utilisation.</summary>
internal static class Pages
{
    private static readonly string Accept = string.Join(",", MimeType.AllowedExtensions.Select(e => "." + e));

    public static string Home(
        IReadOnlyList<DocumentDto> documents, IReadOnlyList<FolderDto> folders,
        string? text, string? tag, string? type, Guid? folderId, long maxFileSizeMb)
    {
        var folderNames = folders.ToDictionary(f => f.Id, f => f.Name);

        var folderLinks = $"""<li><a href="/" class="{(folderId is null ? "active" : "")}">Tous les documents</a></li>"""
            + string.Concat(folders.Select(f =>
                $"""<li><a href="/?folder={f.Id}" class="{(f.Id == folderId ? "active" : "")}">📁 {E(f.Name)}</a></li>"""));

        var typeOptions = """<option value="">Tous les types</option>"""
            + string.Concat(MimeType.AllowedExtensions.Select(e =>
                $"""<option value="{e}"{(e == type ? " selected" : "")}>{e}</option>"""));

        var rows = documents.Count == 0
            ? """<tr><td colspan="6" class="muted">Aucun document.</td></tr>"""
            : string.Concat(documents.Select(d => $"""
                <tr>
                  <td><a href="/documents/{d.Id}">{E(d.Title)}</a><div class="muted">{E(d.CurrentVersion.FileName)}</div></td>
                  <td>{E(d.FolderId is { } f && folderNames.TryGetValue(f, out var n) ? n : "—")}</td>
                  <td>{Tags(d.Tags)}</td>
                  <td>v{d.CurrentVersion.Number}</td>
                  <td>{E(d.CurrentVersion.Size)}</td>
                  <td class="muted">{Date(d.CurrentVersion.CreatedAt)}</td>
                </tr>
                """));

        return $"""
            <div class="grid">
              <aside>
                <div class="card"><h2>Dossiers</h2><ul class="folders">{folderLinks}</ul></div>
              </aside>
              <section>
                <div class="card">
                  <form method="get" action="/" class="row">
                    <label>Titre<input name="q" value="{E(text)}" placeholder="Rechercher…"></label>
                    <label>Tag<input name="tag" value="{E(tag)}"></label>
                    <label>Type<select name="type">{typeOptions}</select></label>
                    {(folderId is { } fid ? $"""<input type="hidden" name="folder" value="{fid}">""" : "")}
                    <button>Rechercher</button>
                    <a href="/">Réinitialiser</a>
                  </form>
                  <table>
                    <tr><th>Titre</th><th>Dossier</th><th>Tags</th><th>Version</th><th>Taille</th><th>Modifié</th></tr>
                    {rows}
                  </table>
                  <p class="muted">{documents.Count} document(s)</p>
                </div>
                <div class="card">
                  <h2>Ajouter un document</h2>
                  <form method="post" action="/documents" enctype="multipart/form-data">
                    <div class="row">
                      <label>Fichier ({E(string.Join(", ", MimeType.AllowedExtensions))} — {maxFileSizeMb} Mo max)
                        <input type="file" name="file" accept="{Accept}" required></label>
                    </div>
                    <div class="row">
                      <label>Titre (facultatif)<input name="title" placeholder="Nom du fichier par défaut"></label>
                      <label>Dossier<select name="folder">{FolderOptions(folders, folderId, "Racine")}</select></label>
                      <label>Tags (séparés par des virgules)<input name="tags" placeholder="facture, 2026"></label>
                      <button>Envoyer</button>
                    </div>
                  </form>
                </div>
              </section>
            </div>
            """;
    }

    public static string Document(DocumentDto d, IReadOnlyList<FolderDto> folders)
    {
        var folderName = d.FolderId is { } fid ? folders.FirstOrDefault(f => f.Id == fid)?.Name : null;
        var trashed = d.Status == "Trashed";

        var versions = string.Concat(d.Versions.Reverse().Select(v => $"""
            <tr>
              <td>v{v.Number}{(v.Number == d.CurrentVersion.Number ? " <span class=\"muted\">(courante)</span>" : "")}</td>
              <td>{E(v.FileName)}</td>
              <td>{E(v.Size)}</td>
              <td class="muted">{Date(v.CreatedAt)}</td>
              <td class="muted" title="{E(v.Checksum)}">{E(v.Checksum[..12])}…</td>
              <td>{(trashed ? "" : $"""<a href="/documents/{d.Id}/download?version={v.Number}">Télécharger</a>""")}</td>
            </tr>
            """));

        var tags = string.Concat(d.Tags.Select(t => trashed
            ? $"""<span class="tag">{E(t)}</span>"""
            : $"""
              <form class="inline" method="post" action="/documents/{d.Id}/tags">
                <input type="hidden" name="remove" value="{E(t)}">
                <span class="tag">{E(t)}<button class="link" title="Retirer">×</button></span>
              </form>
              """));

        var actions = trashed
            ? $"""
              <div class="card">
                <p>Ce document est dans la corbeille depuis le {Date(d.TrashedAt)}.</p>
                <form method="post" action="/documents/{d.Id}/restore"><button>Restaurer</button></form>
              </div>
              """
            : $"""
              <div class="card">
                <h2>Nouvelle version</h2>
                <form method="post" action="/documents/{d.Id}/versions" enctype="multipart/form-data" class="row">
                  <input type="file" name="file" accept="{Accept}" required>
                  <button>Ajouter la version</button>
                </form>
              </div>
              <div class="card">
                <h2>Modifier</h2>
                <form method="post" action="/documents/{d.Id}/tags" class="row">
                  <label>Ajouter des tags<input name="add" placeholder="facture, 2026"></label>
                  <button class="secondary">Ajouter</button>
                </form>
                <form method="post" action="/documents/{d.Id}/rename" class="row">
                  <label>Titre<input name="title" value="{E(d.Title)}" required></label>
                  <button class="secondary">Renommer</button>
                </form>
                <form method="post" action="/documents/{d.Id}/move" class="row">
                  <label>Dossier<select name="folder">{FolderOptions(folders, d.FolderId, "Racine")}</select></label>
                  <button class="secondary">Déplacer</button>
                </form>
                <form method="post" action="/documents/{d.Id}/trash" class="row">
                  <button class="danger">Mettre à la corbeille</button>
                </form>
              </div>
              """;

        return $"""
            <p><a href="/">← Documents</a></p>
            <h1>{E(d.Title)}</h1>
            <div class="card">
              <p>
                <span class="muted">Dossier :</span> {E(folderName ?? "Racine")} ·
                <span class="muted">Créé le :</span> {Date(d.CreatedAt)} ·
                <span class="muted">Type :</span> {E(d.CurrentVersion.MimeType)}
              </p>
              <p><span class="muted">Tags :</span> {(d.Tags.Count == 0 ? "<span class=\"muted\">aucun</span>" : tags)}</p>
              {(trashed ? "" : $"""<p><a href="/documents/{d.Id}/download"><button>Télécharger la version courante</button></a></p>""")}
            </div>
            <div class="card">
              <h2>Historique des versions</h2>
              <table>
                <tr><th>Version</th><th>Fichier</th><th>Taille</th><th>Date</th><th>Empreinte SHA-256</th><th></th></tr>
                {versions}
              </table>
            </div>
            {actions}
            """;
    }

    public static string Folders(IReadOnlyList<FolderDto> folders)
    {
        var names = folders.ToDictionary(f => f.Id, f => f.Name);
        var rows = folders.Count == 0
            ? """<tr><td colspan="3" class="muted">Aucun dossier.</td></tr>"""
            : string.Concat(folders.Select(f => $"""
                <tr>
                  <td><a href="/?folder={f.Id}">📁 {E(f.Name)}</a></td>
                  <td>{E(f.ParentId is { } p && names.TryGetValue(p, out var n) ? n : "—")}</td>
                  <td>
                    <form class="inline" method="post" action="/folders/{f.Id}/delete">
                      <button class="danger">Supprimer</button>
                    </form>
                  </td>
                </tr>
                """));

        return $"""
            <h1>Dossiers</h1>
            <div class="card">
              <table><tr><th>Nom</th><th>Dossier parent</th><th></th></tr>{rows}</table>
            </div>
            <div class="card">
              <h2>Nouveau dossier</h2>
              <form method="post" action="/folders" class="row">
                <label>Nom<input name="name" required></label>
                <label>Dossier parent<select name="parent">{FolderOptions(folders, null, "Aucun (racine)")}</select></label>
                <button>Créer</button>
              </form>
            </div>
            """;
    }

    public static string Trash(IReadOnlyList<DocumentDto> documents, int retentionDays)
    {
        var rows = documents.Count == 0
            ? """<tr><td colspan="3" class="muted">La corbeille est vide.</td></tr>"""
            : string.Concat(documents.Select(d => $"""
                <tr>
                  <td><a href="/documents/{d.Id}">{E(d.Title)}</a></td>
                  <td class="muted">{Date(d.TrashedAt)}</td>
                  <td>
                    <form class="inline" method="post" action="/documents/{d.Id}/restore">
                      <button class="secondary">Restaurer</button>
                    </form>
                  </td>
                </tr>
                """));

        return $"""
            <h1>Corbeille</h1>
            <div class="card">
              <table><tr><th>Titre</th><th>Supprimé le</th><th></th></tr>{rows}</table>
            </div>
            <div class="card">
              <p class="muted">Les documents restés plus de {retentionDays} jours dans la corbeille peuvent être purgés
                 (suppression définitive des données et des fichiers).</p>
              <form method="post" action="/trash/purge"><button class="danger">Purger maintenant</button></form>
            </div>
            """;
    }

    public static string Error(string message) => $"""
        <h1>Oups</h1>
        <div class="card"><p>{E(message)}</p><p><a href="/">← Retour aux documents</a></p></div>
        """;
}
