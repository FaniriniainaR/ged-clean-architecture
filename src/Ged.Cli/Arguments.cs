namespace Ged.Cli;

/// <summary>Analyse minimale des arguments : valeurs positionnelles et options « --nom valeur » (répétables).</summary>
public sealed class Arguments
{
    private static readonly HashSet<string> Flags = ["trash"];

    private readonly List<string> _positional = new();
    private readonly Dictionary<string, List<string>> _options = new(StringComparer.OrdinalIgnoreCase);

    public Arguments(IEnumerable<string> args)
    {
        using var e = args.GetEnumerator();
        while (e.MoveNext())
        {
            if (!e.Current.StartsWith("--"))
            {
                _positional.Add(e.Current);
                continue;
            }

            var name = e.Current[2..];
            var value = "true";
            if (!Flags.Contains(name))
            {
                if (!e.MoveNext()) throw new ArgumentException($"Valeur manquante pour --{name}.");
                value = e.Current;
            }
            if (!_options.TryGetValue(name, out var values)) _options[name] = values = new List<string>();
            values.Add(value);
        }
    }

    public int Count => _positional.Count;

    public string At(int index, string label)
        => index < _positional.Count ? _positional[index] : throw new ArgumentException($"Argument manquant : <{label}>.");

    public string? OptionalAt(int index) => index < _positional.Count ? _positional[index] : null;

    public Guid IdAt(int index, string label = "id") => ParseId(At(index, label));

    public string? Option(string name) => _options.TryGetValue(name, out var v) ? v[^1] : null;

    public IReadOnlyList<string> Options(string name) => _options.TryGetValue(name, out var v) ? v : [];

    public bool Has(string name) => _options.ContainsKey(name);

    public Guid? IdOption(string name) => Option(name) is { } value ? ParseId(value) : null;

    public int? IntOption(string name)
        => Option(name) is { } value
            ? int.TryParse(value, out var n) ? n : throw new ArgumentException($"--{name} doit être un nombre.")
            : null;

    private static Guid ParseId(string value)
        => Guid.TryParse(value, out var id) ? id : throw new ArgumentException($"Identifiant invalide : {value}.");
}
