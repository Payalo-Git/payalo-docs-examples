# PayAlo Merchant Gateway v2 — .NET (Blazor) sample

Blazor Web App on .NET 9 reference for the PayAlo Merchant Gateway v2.

Same surface as the [Node.js sample](../nodejs/) and the [Python sample](../python/):
Direct Pay-In, Web Pay-In (HPP), Direct Pay-Out, QR Pay-In, Direct Tax Payout, status
lookup, records, webhook receiver with `X-API-KEY` validation and idempotent dedupe,
RFC 7807 error parsing.

## TL;DR

```bash
cd samples/dotnet/PayAlo.Sample.Web
dotnet user-secrets set "PayAlo:ApiKey" "pk_live_replace_me"
dotnet run
```

Open <https://localhost:5044>.

## Prerequisites

- .NET 9 SDK (`dotnet --version` ≥ 9.0.300).
- A PayAlo brand API key.
- *(For webhooks)* a public HTTPS tunnel like ngrok — the gateway can't reach `localhost`.
- *(Optional)* Docker Desktop for `docker compose`.

## Configuration

| Key | Required | Default |
|---|---|---|
| `PayAlo:BaseUrl` | yes | `https://dev-api-internal.payalo.com/gateway/mmo/v2/` |
| `PayAlo:ApiKey` | yes | *(unset)* |
| `PayAlo:WebhookPath` | no | `/webhooks/payalo` |
| `PayAlo:PublicBaseUrl` | recommended | *(unset)* — your public tunnel URL |

Set values via any of:

- `dotnet user-secrets set "PayAlo:ApiKey" "..."` (development)
- environment variables, replacing `:` with `__` (`PayAlo__ApiKey=...`)
- a gitignored `appsettings.Local.json` next to `appsettings.json`
  (template at `PayAlo.Sample.Web/appsettings.Local.example.json`)

## Webhooks

1. `dotnet run` — note the HTTPS port (default `5044`).
2. `ngrok http https://localhost:5044` — copy the public HTTPS URL.
3. Set `PayAlo:PublicBaseUrl` to that URL and restart.
4. The Webhooks page shows the public webhook URL the gateway should reach.

## Run with Docker

```bash
cp .env.example .env   # then edit PAYALO__APIKEY and PAYALO__PUBLICBASEURL
docker compose up --build
```

Container exposes port 8080.

## Project layout

```
samples/dotnet/
  PayAlo.Sample.Web/
    Components/
      Pages/         # one Razor page per flow
      Shared/        # ProblemDetailsAlert, StatusBadge
    Endpoints/
      WebhookEndpoint.cs           # POST /webhooks/payalo (Minimal API)
    Integration/
      Models/        # DTOs that mirror the V2 contract
      PayAloClient.cs              # typed HttpClient wrapping every demonstrated endpoint
      ApiKeyHandler.cs             # DelegatingHandler that injects X-Api-Key
      ProblemDetailsException.cs   # surfaces RFC 7807 errors as a typed exception
      PayAloErrorMessages.cs       # maps async error codes to friendly copy
      PayAloOptions.cs             # strongly-typed config
    Services/
      WebhookStore.cs              # in-memory webhook log (DEMO ONLY)
    Program.cs                     # wiring: HttpClient + ApiKeyHandler + resilience + endpoints
  tests/PayAlo.Sample.Web.Tests/   # xUnit + WireMock — 2 client tests + 2 webhook tests
  PayAlo.IntegrationExamples.slnx
  Dockerfile
  docker-compose.yml
```

## Tests + CI

```bash
dotnet test PayAlo.IntegrationExamples.slnx
```

GitHub Actions runs build + tests against this solution on every push (see
`.github/workflows/ci.yml` at the repo root).

## Notes

- Every form regenerates `merchantReference` after each submit (success or failure) so
  a quick re-submit doesn't trip the gateway's duplicate-reference rule.
- Retries live in the typed-client pipeline (`AddStandardResilienceHandler`). The
  default policy retries idempotent methods only — POST initiation calls are never
  silently re-sent, which protects the merchantReference idempotency window.
- The webhook store is in-memory and DEMO-ONLY. Swap `Program.cs`'s singleton
  registration for a database-backed implementation in production so callbacks survive
  restart and dedupe across replicas.
