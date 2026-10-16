using System.Net;
using Ged.Application.Dtos;

namespace Ged.Web;

/// <summary>Petits utilitaires de génération HTML (tout est échappé pour éviter l'injection de code).</summary>
internal static class Html
{
    public static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

    public static string U(string text) => Uri.EscapeDataString(text);

    public static string Date(DateTime? value) => value is { } d ? d.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";

    public static string Tags(IEnumerable<string> tags)
        => string.Concat(tags.Select(t => $"""<a class="tag" href="/?tag={U(t)}">{E(t)}</a>"""));

    public static string FolderOptions(IReadOnlyList<FolderDto> folders, Guid? selected, string rootLabel)
        => $"""<option value="">{E(rootLabel)}</option>"""
           + string.Concat(folders.Select(f =>
               $"""<option value="{f.Id}"{(f.Id == selected ? " selected" : "")}>{E(f.Name)}</option>"""));

    public static string Layout(string title, string body, string? ok, string? error) => $$"""
        <!doctype html>
        <html lang="fr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{E(title)}} — GED</title>
          <style>
            :root { --bg:#f6f7f9; --card:#fff; --text:#1d2330; --muted:#677084; --line:#e2e5ea;
                    --accent:#2f6fde; --danger:#c8372d; --ok:#1f8a4c; }
            * { box-sizing: border-box; }
            body { margin:0; font:15px/1.5 system-ui, sans-serif; background:var(--bg); color:var(--text); }
            header { background:#1d2330; color:#fff; padding:12px 24px; display:flex; gap:24px; align-items:center; flex-wrap:wrap; }
            header a { color:#cfd6e4; text-decoration:none; } header a:hover { color:#fff; }
            header strong { font-size:18px; color:#fff; }
            main { max-width:1100px; margin:24px auto; padding:0 16px; }
            h1 { font-size:22px; margin:0 0 16px; } h2 { font-size:17px; margin:0 0 12px; }
            .card { background:var(--card); border:1px solid var(--line); border-radius:8px; padding:16px; margin-bottom:16px; }
            .grid { display:grid; grid-template-columns: 240px 1fr; gap:16px; }
            @media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
            table { width:100%; border-collapse:collapse; }
            th, td { text-align:left; padding:8px; border-bottom:1px solid var(--line); vertical-align:middle; }
            th { color:var(--muted); font-weight:600; font-size:13px; }
            .muted { color:var(--muted); font-size:13px; }
            a { color:var(--accent); }
            .tag { display:inline-block; background:#e8effc; color:var(--accent); border-radius:12px; padding:1px 10px;
                   margin:2px 4px 2px 0; font-size:13px; text-decoration:none; }
            form.inline { display:inline; }
            .row { display:flex; gap:8px; flex-wrap:wrap; align-items:end; margin-bottom:8px; }
            label { display:flex; flex-direction:column; font-size:13px; color:var(--muted); gap:4px; }
            input, select { font:inherit; padding:6px 8px; border:1px solid var(--line); border-radius:6px; background:#fff; }
            button { font:inherit; padding:6px 14px; border-radius:6px; border:1px solid var(--accent);
                     background:var(--accent); color:#fff; cursor:pointer; }
            button.secondary { background:#fff; color:var(--accent); }
            button.danger { background:#fff; color:var(--danger); border-color:var(--danger); }
            button.link { background:none; border:none; color:var(--danger); padding:0 4px; }
            .flash { padding:10px 14px; border-radius:6px; margin-bottom:16px; }
            .flash.ok { background:#e5f5ec; color:var(--ok); } .flash.err { background:#fbe9e7; color:var(--danger); }
            ul.folders { list-style:none; padding:0; margin:0; } ul.folders li { padding:3px 0; }
            ul.folders a.active { font-weight:700; }
          </style>
        </head>
        <body>
          <header>
            <strong>GED</strong>
            <a href="/">Documents</a>
            <a href="/folders">Dossiers</a>
            <a href="/trash">Corbeille</a>
          </header>
          <main>
            {{(ok is null ? "" : $"<div class=\"flash ok\">{E(ok)}</div>")}}
            {{(error is null ? "" : $"<div class=\"flash err\">{E(error)}</div>")}}
            {{body}}
          </main>
        </body>
        </html>
        """;
}
