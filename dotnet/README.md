# Payalo Merchant Gateway v2 — .NET samples

Two .NET 9 references in one solution, sharing the same typed `HttpClient` +
`ApiKeyHandler` + standard resilience patterns:

- **[`Payalo.Sample.Web/`](Payalo.Sample.Web/)** — Blazor Web App. Full surface
  including the inbound webhook receiver. **This is what you want for end-to-end
  testing.**
- **[`Payalo.Sample.Console/`](Payalo.Sample.Console/README.md)** — CLI. Every
  submit + lookup flow as a subcommand (no webhook receiver — see the Blazor sample
  for that).

Same surface as the [Node.js sample](../nodejs/) and the [Python sample](../python/):
Direct Pay-In, Web Pay-In (HPP), Direct Pay-Out, QR Pay-In, Direct Tax Payout, status
lookup, records, RFC 7807 error parsing. The Blazor sample additionally hosts a
webhook receiver with `X-API-KEY` validation and idempotent dedupe.

## TL;DR

```bash
# Blazor sample
cd dotnet/Payalo.Sample.Web
dotnet user-secrets set "Payalo:ApiKey" "pk_live_replace_me"
dotnet run                                 # browse https://localhost:5044

# Console sample
cd ../Payalo.Sample.Console
dotnet user-secrets set "Payalo:ApiKey" "pk_live_replace_me"
dotnet run -- help                         # discover commands
dotnet run -- payin-direct                 # interactive prompts
```

## Prerequisites

- .NET 9 SDK (`dotnet --version` ≥ 9.0.300).
- A Payalo brand API key.
- *(For webhooks)* a public HTTPS tunnel like ngrok — the gateway can't reach `localhost`.
- *(Optional)* Docker Desktop for `docker compose`.

## Configuration

| Key | Required | Default |
|---|---|---|
| `Payalo:BaseUrl` | yes | `https://dev-api-internal.payalo.com/gateway/mmo/v2/` |
| `Payalo:ApiKey` | yes | *(unset)* |
| `Payalo:WebhookPath` | no | `/webhooks/payalo` |
| `Payalo:PublicBaseUrl` | recommended | *(unset)* — your public tunnel URL |

Set values via any of:

- `dotnet user-secrets set "Payalo:ApiKey" "..."` (development)
- environment variables, replacing `:` with `__` (`Payalo__ApiKey=...`)
- a gitignored `appsettings.Local.json` next to `appsettings.json`
  (template at `Payalo.Sample.Web/appsettings.Local.example.json`)

## Webhooks

1. `dotnet run` — note the HTTPS port (default `5044`).
2. `ngrok http https://localhost:5044` — copy the public HTTPS URL.
3. Set `Payalo:PublicBaseUrl` to that URL and restart.
4. The Webhooks page shows the public webhook URL the gateway should reach.

## Run with Docker

```bash
cp .env.example .env   # then edit PAYALO__APIKEY and PAYALO__PUBLICBASEURL
docker compose up --build
```

Container exposes port 8080.

## Project layout

```
dotnet/
  Payalo.Sample.Web/                # Blazor sample
    Components/Pages/               # one Razor page per flow
    Components/Shared/              # ProblemDetailsAlert, StatusBadge
    Endpoints/WebhookEndpoint.cs    # POST /webhooks/payalo (Minimal API)
    Integration/                    # typed client + DTOs + ApiKeyHandler + RFC 7807 surface
    Services/WebhookStore.cs        # in-memory webhook log (DEMO ONLY)
    Program.cs                      # wiring
  Payalo.Sample.Console/            # CLI sample (see Console/README.md)
    Cli/                            # arg parser, prompts, output formatting, help text
    Commands/                       # one class per subcommand
    Integration/                    # near-mirror of the web sample's Integration/
    Program.cs                      # host wiring + command dispatch
  tests/Payalo.Sample.Web.Tests/    # xUnit + WireMock — 2 client tests + 2 webhook tests
  Payalo.IntegrationExamples.slnx
  Dockerfile                        # used by Payalo.Sample.Web
  docker-compose.yml
```

## Tests

```bash
dotnet test Payalo.IntegrationExamples.slnx
```

The repository has no CI workflow yet, so run the tests locally before you change the samples.

## Notes

- Every form regenerates `merchantReference` after each submit (success or failure), so
  a quick re-submit does not reuse a reference.
- Retries live in the typed-client pipeline (`AddStandardResilienceHandler`). The default
  policy retries every method, so `Program.cs` turns retries off for POST: a create is never
  sent twice by the pipeline.
- The webhook store is in-memory and DEMO-ONLY. Swap `Program.cs`'s singleton
  registration for a database-backed implementation in production so callbacks survive
  restart and dedupe across replicas.
