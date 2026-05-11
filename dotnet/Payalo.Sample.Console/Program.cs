using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Commands;
using Payalo.Sample.Console.Integration;
using Serilog;
using SystemConsole = System.Console;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var parsed = ParsedArgs.Parse(args);

    if (parsed.Command is null or "help" or "--help" or "-h")
    {
        HelpText.PrintRoot();
        return 0;
    }

    // Validate the command up front — saves building the host (and firing config
    // warnings) just to print "Unknown command".
    var knownCommands = new[]
    {
        "payin-direct", "payin-web", "payin-qr",
        "payout-direct", "payout-tax",
        "status", "records",
    };
    if (!knownCommands.Contains(parsed.Command))
    {
        Output.Error($"Unknown command: {parsed.Command}");
        HelpText.PrintRoot();
        return 1;
    }

    if (parsed.Help)
    {
        HelpText.PrintForCommand(parsed.Command);
        return 0;
    }

    var builder = Host.CreateApplicationBuilder();

    // Optional gitignored override file. Mirrors the web sample's appsettings.Local.json
    // pattern so users can stash an API key in JSON without leaking it.
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    // Enable user-secrets in Development (mirrors `dotnet user-secrets set "Payalo:ApiKey" "..."`).
    if (builder.Environment.IsDevelopment())
        builder.Configuration.AddUserSecrets<Program>(optional: true);

    builder.Services.AddSerilog((sp, lc) => lc.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

    builder.Services
        .AddOptions<PayaloOptions>()
        .Bind(builder.Configuration.GetSection(PayaloOptions.SectionName));

    // Same wiring as the web sample: ApiKeyHandler injects X-Api-Key, the standard
    // resilience handler retries idempotent methods only — POST initiation is never
    // silently re-sent, protecting the merchantReference idempotency window.
    builder.Services.AddTransient<ApiKeyHandler>();
    builder.Services.AddHttpClient<PayaloClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PayaloOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
            }
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/problem+json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Payalo.Sample.Console/1.0");
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddStandardResilienceHandler();

    // Each command is a thin DI-resolvable type. Adding a new command means: write a
    // new class, register it here, dispatch on its name in the switch below.
    builder.Services.AddTransient<PayInDirectCommand>();
    builder.Services.AddTransient<PayInWebCommand>();
    builder.Services.AddTransient<PayInQrCommand>();
    builder.Services.AddTransient<PayOutDirectCommand>();
    builder.Services.AddTransient<PayOutTaxCommand>();
    builder.Services.AddTransient<StatusCommand>();
    builder.Services.AddTransient<RecordsCommand>();

    using var host = builder.Build();

    var startupOptions = host.Services.GetRequiredService<IOptions<PayaloOptions>>().Value;
    if (string.IsNullOrWhiteSpace(startupOptions.ApiKey))
    {
        Output.Warn("Payalo:ApiKey is not set — every gateway call will return 401.");
        Output.Warn("Set it via `dotnet user-secrets set \"Payalo:ApiKey\" \"...\"`, an env var (Payalo__ApiKey),");
        Output.Warn("or a gitignored appsettings.Local.json next to appsettings.json.");
    }

    using var cts = new CancellationTokenSource();
    SystemConsole.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var sp = host.Services;
    try
    {
        switch (parsed.Command)
        {
            case "payin-direct":
                await sp.GetRequiredService<PayInDirectCommand>().ExecuteAsync(parsed, cts.Token);
                break;
            case "payin-web":
                await sp.GetRequiredService<PayInWebCommand>().ExecuteAsync(parsed, cts.Token);
                break;
            case "payin-qr":
                await sp.GetRequiredService<PayInQrCommand>().ExecuteAsync(parsed, cts.Token);
                break;
            case "payout-direct":
                await sp.GetRequiredService<PayOutDirectCommand>().ExecuteAsync(parsed, cts.Token);
                break;
            case "payout-tax":
                await sp.GetRequiredService<PayOutTaxCommand>().ExecuteAsync(parsed, cts.Token);
                break;
            case "status":
                await sp.GetRequiredService<StatusCommand>().ExecuteAsync(parsed, cts.Token);
                break;
            case "records":
                await sp.GetRequiredService<RecordsCommand>().ExecuteAsync(parsed, cts.Token);
                break;
            default:
                // Unreachable — validated against knownCommands above.
                throw new InvalidOperationException($"Command '{parsed.Command}' is in the known list but has no dispatch entry.");
        }
    }
    catch (PayaloApiException ex)
    {
        // RFC 7807 error from the gateway. Show the friendly explanation when we have
        // a known error code, else surface the raw detail / body.
        Output.Error($"Gateway returned {(int)ex.StatusCode} {ex.StatusCode}");
        if (ex.ErrorCode is not null)
        {
            var friendly = PayaloErrorMessages.Describe(ex.ErrorCode, ex.Detail);
            Output.Error($"  [{ex.ErrorCode}] {friendly}");
        }
        else if (!string.IsNullOrEmpty(ex.RawBody))
        {
            Output.Error($"  {ex.RawBody}");
        }
        if (!string.IsNullOrEmpty(ex.TraceId))
            Output.Error($"  trace: {ex.TraceId}");
        return 2;
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        Output.Warn("Cancelled.");
        return 130;
    }

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

// Anchor for AddUserSecrets<Program>().
public partial class Program;
