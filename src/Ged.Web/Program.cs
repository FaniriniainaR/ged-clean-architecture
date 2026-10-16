using System.Net;
using Ged.Bootstrap;
using Ged.Web;

// Serveur web écrit avec HttpListener (bibliothèque standard .NET) : aucun framework.
var config = AppConfig.Load();
var app = new CompositionRoot(config);
var site = new WebApp(app);

var url = args.FirstOrDefault() ?? config.WebUrl;
if (!url.EndsWith('/')) url += "/";

var listener = new HttpListener();
listener.Prefixes.Add(url);
listener.Start();
Console.WriteLine($"GED web : {url}   (base : {config.Database.Provider}) — Ctrl+C pour arrêter");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    listener.Stop();
};

while (listener.IsListening)
{
    HttpListenerContext context;
    try
    {
        context = listener.GetContext();
    }
    catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
    {
        break;
    }
    _ = Task.Run(() => site.Handle(context));
}
