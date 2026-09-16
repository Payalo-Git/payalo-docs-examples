# Payalo Merchant Gateway v2 — .NET console sample

A small command-line client for the Payalo Merchant Gateway v2, built on .NET 9 with
the same typed `HttpClient` + `ApiKeyHandler` + Polly resilience pipeline as the
[Blazor sample](../Payalo.Sample.Web/) next door.

Covers every documented submit + lookup endpoint:

- Direct Pay-In, Web Pay-In (HPP), Direct Pay-Out, QR Pay-In, Direct Tax Payout
- Status lookup (by gateway or merchant reference)
- Records (paginated bulk reconciliation)
- RFC 7807 Problem Details parsing with friendly messages for every documented
  async error code

The console sample does **not** host an inbound webhook receiver — see the Blazor
sample if you want end-to-end webhook testing.

## TL;DR

```bash
cd dotnet/Payalo.Sample.Console
dotnet user-secrets set "Payalo:ApiKey" "pk_live_replace_me"

dotnet run -- help
dotnet run -- payin-direct                 # interactive prompts for required values
dotnet run -- payin-direct --help          # see all flags for a command
```

## Prerequisites

- .NET 9 SDK (`dotnet --version` ≥ 9.0.300).
- A Payalo brand API key.

## Configuration

| Key | Required | Default |
|---|---|---|
| `Payalo:BaseUrl` | yes | `https://api.payalo.com/gateway/mmo/v2/` |
| `Payalo:ApiKey` | yes | *(unset)* |
| `Payalo:DefaultResultUrl` | recommended | `https://example.com/webhooks/payalo` |

Set values via any of:

- `dotnet user-secrets set "Payalo:ApiKey" "..."` (development)
- environment variables, replacing `:` with `__` (`Payalo__ApiKey=...`)
- a gitignored `appsettings.Local.json` next to `appsettings.json`
  (template at `appsettings.Local.example.json`)

`DefaultResultUrl` is the webhook URL populated on every submit when you don't pass
`--result-url` explicitly. The console can't host a webhook receiver itself; point
this at the Blazor sample (or the Node/Python samples) behind ngrok if you want the
gateway to deliver terminal callbacks somewhere you can inspect.

## Commands

| Command | Endpoint |
|---|---|
| `payin-direct` | `POST /direct/payin/{method}` |
| `payin-web` | `POST /web/payin/{method}` (Hosted Payment Page) |
| `payin-qr` | `POST /qr/payin/{method}` |
| `payout-direct` | `POST /direct/payout/{method}` |
| `payout-tax` | `POST /direct/taxpayout/{method}` |
| `status` | `GET /status/{ref}` or `GET /status/mref/{ref}` |
| `records` | `GET /records` (paginated) |

Every submit command auto-generates a fresh `merchantReference` (`sample-<guid>`)
when you don't pass `--merchant-ref` — so an immediate re-submit can never trip the
gateway's duplicate-reference rule. Each command supports `--help` for its own flags.

### Examples

```bash
# Direct Pay-In with all values via flags
dotnet run -- payin-direct \
  --method mpesa-ke \
  --amount 100.00 --currency KES --country KE \
  --party-id user-123 --party-msisdn +254712345678 \
  --result-url https://your-host.example.com/webhooks/payalo

# Status lookup by merchant reference
dotnet run -- status --mref sample-<guid>

# Records: first page in a date range
dotnet run -- records \
  --from 2026-05-01T00:00:00Z --to 2026-05-08T00:00:00Z \
  --type payin --status success

# Records: subsequent page using the cursor returned above
dotnet run -- records --page-cursor <cursor>
```

Run any command with no flags to be prompted interactively.

## Project layout

```
Payalo.Sample.Console/
  Program.cs                       # Host wiring + command dispatch + error handling
  Cli/
    ArgParser.cs                   # Minimal long-flag parser
    HelpText.cs                    # Static usage strings
    Output.cs                      # Banner / Success / Warn / Error / Json writers
    Prompt.cs                      # Interactive value prompts (skipped when stdin redirected)
  Commands/
    SubmitInputs.cs                # Shared "common" + party gathering for submit commands
    PayInDirectCommand.cs
    PayInWebCommand.cs
    PayInQrCommand.cs
    PayOutDirectCommand.cs
    PayOutTaxCommand.cs
    StatusCommand.cs
    RecordsCommand.cs
  Integration/
    PayaloClient.cs                # Typed HttpClient — one method per endpoint
    ApiKeyHandler.cs               # DelegatingHandler that injects X-Api-Key
    PayaloOptions.cs               # Strongly-typed config
    PayaloErrorMessages.cs         # Maps async error codes to friendly copy
    ProblemDetailsException.cs     # Typed RFC 7807 surface
    Models/                        # DTOs that mirror the V2 contract
  appsettings.json
  appsettings.Local.example.json
```

The `Integration/` folder is intentionally a near-mirror of the Blazor sample's
equivalent — each sample is meant to be readable in isolation, so duplication is
preferred over a shared library here.

## Notes

- The same retry/circuit-breaker pipeline as the web sample. POSTs are NOT retried
  by default — protects the `merchantReference` idempotency window.
- `PayaloApiException` is caught at the top level so RFC 7807 errors come out as a
  red one-liner with the friendly explanation, the error code, and the W3C trace
  parent if the gateway returned one (useful in support tickets).
- Full V2 API reference: <https://docs.payalo.com>.
