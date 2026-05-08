using System.Text.Json;
using SystemConsole = System.Console;

namespace Payalo.Sample.Console.Cli;

// Console output helpers. Centralised so commands stay free of System.Console plumbing
// (and so we can swap in colour/no-colour modes without editing every command).
public static class Output
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Banner(string text)
    {
        WithColor(ConsoleColor.Cyan, () =>
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine($"=== {text} ===");
            SystemConsole.WriteLine();
        });
    }

    public static void Info(string message) => SystemConsole.WriteLine(message);

    public static void Success(string message) =>
        WithColor(ConsoleColor.Green, () => SystemConsole.WriteLine(message));

    public static void Warn(string message) =>
        WithColor(ConsoleColor.Yellow, () => SystemConsole.Error.WriteLine(message));

    public static void Error(string message) =>
        WithColor(ConsoleColor.Red, () => SystemConsole.Error.WriteLine(message));

    // Pretty-print any object as indented JSON. Used after a successful API call so the
    // sample reader can see the full server response without needing a debugger attached.
    public static void Json<T>(T value)
    {
        SystemConsole.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void WithColor(ConsoleColor color, Action write)
    {
        var previous = SystemConsole.ForegroundColor;
        try
        {
            SystemConsole.ForegroundColor = color;
            write();
        }
        finally
        {
            SystemConsole.ForegroundColor = previous;
        }
    }
}
