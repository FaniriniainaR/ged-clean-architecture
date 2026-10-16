using System.Net;
using System.Text;
using System.Web;

namespace Ged.Web;

public sealed record UploadedFile(string FileName, byte[] Content);

/// <summary>
/// Lecture des formulaires HTML : « application/x-www-form-urlencoded » et « multipart/form-data » (envoi de fichiers).
/// Le format multipart est analysé à la main.
/// </summary>
public sealed class FormData
{
    private readonly Dictionary<string, string> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UploadedFile> _files = new(StringComparer.OrdinalIgnoreCase);

    public string? this[string name] => _fields.TryGetValue(name, out var v) && v.Length > 0 ? v : null;

    public UploadedFile? File(string name) => _files.GetValueOrDefault(name);

    public static FormData Read(HttpListenerRequest request, long maxBodyBytes)
    {
        if (request.ContentLength64 > maxBodyBytes)
            throw new ArgumentException($"Envoi trop volumineux (maximum {maxBodyBytes / (1024 * 1024)} Mo).");

        var body = ReadBody(request.InputStream, maxBodyBytes);
        var contentType = request.ContentType ?? "";
        var form = new FormData();

        if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            form.ParseMultipart(body, Boundary(contentType));
        else
        {
            var values = HttpUtility.ParseQueryString(Encoding.UTF8.GetString(body));
            foreach (var key in values.AllKeys.OfType<string>())
                form._fields[key] = values[key] ?? "";
        }
        return form;
    }

    private static byte[] ReadBody(Stream input, long max)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            memory.Write(buffer, 0, read);
            if (memory.Length > max)
                throw new ArgumentException($"Envoi trop volumineux (maximum {max / (1024 * 1024)} Mo).");
        }
        return memory.ToArray();
    }

    private static string Boundary(string contentType)
    {
        var part = contentType.Split(';').Select(p => p.Trim())
            .FirstOrDefault(p => p.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Formulaire multipart sans « boundary ».");
        return part["boundary=".Length..].Trim('"');
    }

    // Structure : --boundary\r\n En-têtes\r\n\r\n Contenu \r\n--boundary ... --boundary--
    private void ParseMultipart(byte[] body, string boundary)
    {
        var delimiter = Encoding.ASCII.GetBytes("--" + boundary);
        var headerEnd = "\r\n\r\n"u8.ToArray();
        var data = body.AsSpan();

        var start = data.IndexOf(delimiter);
        while (start >= 0)
        {
            var partStart = start + delimiter.Length;
            if (data[partStart..].StartsWith("--"u8)) break; // délimiteur final
            partStart += 2; // \r\n

            var next = data[partStart..].IndexOf(delimiter);
            if (next < 0) break;
            var part = data.Slice(partStart, next - 2); // sans le \r\n qui précède le délimiteur

            var split = part.IndexOf(headerEnd);
            if (split >= 0)
            {
                var headers = Encoding.UTF8.GetString(part[..split]);
                var content = part[(split + headerEnd.Length)..].ToArray();
                AddPart(headers, content);
            }
            start = partStart + next;
        }
    }

    private void AddPart(string headers, byte[] content)
    {
        var disposition = headers.Split("\r\n")
            .FirstOrDefault(h => h.StartsWith("Content-Disposition", StringComparison.OrdinalIgnoreCase)) ?? "";
        var name = Attribute(disposition, "name");
        if (name is null) return;

        var fileName = Attribute(disposition, "filename");
        if (fileName is null)
            _fields[name] = Encoding.UTF8.GetString(content);
        else if (fileName.Length > 0)
            _files[name] = new UploadedFile(Path.GetFileName(fileName.Replace('\\', '/')), content);
    }

    private static string? Attribute(string header, string attribute)
    {
        foreach (var piece in header.Split(';').Select(p => p.Trim()))
        {
            if (piece.StartsWith(attribute + "=", StringComparison.OrdinalIgnoreCase))
                return piece[(attribute.Length + 1)..].Trim('"');
        }
        return null;
    }
}
