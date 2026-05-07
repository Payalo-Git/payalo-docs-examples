using Microsoft.Extensions.Options;
using PayAlo.Sample.Web.Components;
using PayAlo.Sample.Web.Endpoints;
using PayAlo.Sample.Web.Integration;
using PayAlo.Sample.Web.Services;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Optional gitignored override file. Lets you stash the API key in JSON without
    // leaking it. Loaded after the host's defaults so its values win.
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    // PayAlo:* values come from appsettings.json, appsettings.Local.json, environment
    // variables, and `dotnet user-secrets`. Secrets are enabled automatically in Development.
    builder.Services
        .AddOptions<PayAloOptions>()
        .Bind(builder.Configuration.GetSection(PayAloOptions.SectionName));

    // Typed HttpClient for the gateway:
    //   - ApiKeyHandler injects X-Api-Key on every outbound call.
    //   - The standard resilience handler adds retry + circuit breaker for transient
    //     faults; by default it only retries idempotent methods (GET/HEAD), so our
    //     POST initiation calls are NEVER silently retried — protects merchantReference.
    builder.Services.AddTransient<ApiKeyHandler>();
    builder.Services.AddHttpClient<PayAloClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PayAloOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                // Trailing slash matters: HttpClient.BaseAddress strips the last path segment
                // when resolving relative URIs without one.
                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
            }
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/problem+json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PayAlo.Sample.Web/1.0");
        })
        .AddHttpMessageHandler<ApiKeyHandler>()
        .AddStandardResilienceHandler();

    // DEMO-ONLY in-memory webhook log. Swap for a database-backed implementation in
    // production so callbacks survive restarts and can be deduped across replicas.
    builder.Services.AddSingleton<WebhookStore>();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Inbound webhook from the gateway. Mapped after UseAntiforgery so we can opt out
    // via .DisableAntiforgery() in the endpoint registration.
    app.MapPayAloWebhook();

    // Friendly heads-up if the API key is missing — the home page already explains it
    // visually, but the terminal is usually the first place a developer looks.
    var startupOptions = app.Services.GetRequiredService<IOptions<PayAloOptions>>().Value;
    if (string.IsNullOrWhiteSpace(startupOptions.ApiKey))
        app.Logger.LogWarning("PayAlo:ApiKey is not set — every gateway call will 401. See README §Quick start.");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Allow the test project to host the app via WebApplicationFactory<Program>.
public partial class Program;
