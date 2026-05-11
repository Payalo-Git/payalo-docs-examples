namespace Payalo.Sample.Console.Cli;

// Tiny long-flag arg parser. Sample-grade — handles `--key value`, `--key=value`,
// bare flags (`--help`), and positional tokens. Not exhaustive (no short flags, no
// argument validation), but enough for a command-style CLI.
public sealed class ParsedArgs
{
    public string? Command { get; init; }
    public IReadOnlyDictionary<string, string?> Options { get; init; } =
        new Dictionary<string, string?>();
    public IReadOnlyList<string> Positional { get; init; } = Array.Empty<string>();

    public bool Help =>
        Options.ContainsKey("--help") || Options.ContainsKey("-h");

    public string? Get(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (Options.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }

    public bool Has(params string[] keys) =>
        keys.Any(Options.ContainsKey);

    public static ParsedArgs Parse(string[] args)
    {
        string? command = null;
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (token.StartsWith("--"))
            {
                var eq = token.IndexOf('=');
                if (eq > 0)
                {
                    options[token[..eq]] = token[(eq + 1)..];
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    options[token] = args[++i];
                }
                else
                {
                    options[token] = "true";
                }
            }
            else if (token == "-h")
            {
                options["-h"] = "true";
            }
            else if (command is null)
            {
                command = token;
            }
            else
            {
                positional.Add(token);
            }
        }

        return new ParsedArgs
        {
            Command = command,
            Options = options,
            Positional = positional,
        };
    }
}
