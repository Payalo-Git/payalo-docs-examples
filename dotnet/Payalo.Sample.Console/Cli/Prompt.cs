using System.Globalization;
using SystemConsole = System.Console;

namespace Payalo.Sample.Console.Cli;

// Interactive prompts used by command handlers when a value isn't supplied via CLI flags.
// Each Ask* method writes a labelled prompt to stdout, reads a line from stdin, and
// validates the input. Empty input + a `defaultValue` returns the default; otherwise
// the prompt re-asks. `PromptContext.NonInteractive` (set when stdin is redirected)
// disables prompting and surfaces a clear error instead of hanging in CI.
public static class Prompt
{
    public static bool NonInteractive { get; set; } = SystemConsole.IsInputRedirected;

    public static string AskString(string label, string? defaultValue = null, bool required = true)
    {
        while (true)
        {
            EnsureInteractive(label);
            var input = ReadLineWithLabel(label, defaultValue);
            if (string.IsNullOrEmpty(input))
            {
                if (defaultValue is not null) return defaultValue;
                if (!required) return "";
                Output.Warn("A value is required.");
                continue;
            }
            return input;
        }
    }

    public static decimal AskDecimal(string label, decimal? defaultValue = null)
    {
        while (true)
        {
            EnsureInteractive(label);
            var input = ReadLineWithLabel(label, defaultValue?.ToString(CultureInfo.InvariantCulture));
            if (string.IsNullOrEmpty(input) && defaultValue is not null)
                return defaultValue.Value;

            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            Output.Warn("Please enter a numeric value (e.g. 500.00).");
        }
    }

    public static int AskInt(string label, int? defaultValue = null)
    {
        while (true)
        {
            EnsureInteractive(label);
            var input = ReadLineWithLabel(label, defaultValue?.ToString(CultureInfo.InvariantCulture));
            if (string.IsNullOrEmpty(input) && defaultValue is not null)
                return defaultValue.Value;

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            Output.Warn("Please enter an integer.");
        }
    }

    public static DateTimeOffset AskDateTime(string label, DateTimeOffset defaultValue)
    {
        while (true)
        {
            EnsureInteractive(label);
            var input = ReadLineWithLabel(label, defaultValue.ToString("o"));
            if (string.IsNullOrEmpty(input)) return defaultValue;

            if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }

            Output.Warn("Please enter an ISO 8601 timestamp (e.g. 2026-05-08T00:00:00Z).");
        }
    }

    private static string? ReadLineWithLabel(string label, string? defaultValue)
    {
        var hint = defaultValue is null ? "" : $" [{defaultValue}]";
        SystemConsole.Write($"{label}{hint}: ");
        return SystemConsole.ReadLine()?.Trim();
    }

    private static void EnsureInteractive(string label)
    {
        if (!NonInteractive) return;
        throw new InvalidOperationException(
            $"Missing required input for '{label}' and stdin is not interactive. " +
            "Pass the value via a CLI flag (run the command with --help to see options).");
    }
}
