# Payalo Merchant Gateway v2 — Integration Examples

Reference applications for the Payalo Merchant Gateway v2 in three idiomatic stacks.
All three samples cover the same surface — pick the one closest to your production codebase
and read the per-sample README for setup.

| Sample | Stack | README |
|---|---|---|
| **`dotnet/Payalo.Sample.Web/`** | Blazor Web App on .NET 9, typed `HttpClient` + Polly resilience | [README](dotnet/README.md) |
| **`dotnet/Payalo.Sample.Console/`** | .NET 9 CLI sharing the same typed-client patterns (no webhook receiver) | [README](dotnet/Payalo.Sample.Console/README.md) |
| **`nodejs/`** | TypeScript + Express + EJS, `axios` client | [README](nodejs/README.md) |
| **`python/`** | Python 3.12 + FastAPI + Jinja2, `httpx` client | [README](python/README.md) |

The full V2 API reference is on the documentation site at
[docs.payalo.com](http://docs.payalo.com).

---

## What every sample covers

- Direct Pay-In, Web Pay-In (HPP), Direct Pay-Out, QR Pay-In, Direct Tax Payout
- Status lookup (by gateway or merchant reference)
- Records (paginated bulk reconciliation)
- Inbound webhook receiver with `X-API-KEY` validation and idempotent dedupe
- RFC 7807 Problem Details parsing with friendly messages for every documented async error code
- Auto-bumping `merchantReference` after every submit so duplicate-reference errors are impossible

The samples are intentionally small (~600–1500 LoC each) and KISS-first — each one is a
self-contained integration reference an intermediate developer can read in an afternoon.

---

## Quick start

### .NET (Blazor)

```bash
cd dotnet/Payalo.Sample.Web
dotnet user-secrets set "Payalo:ApiKey" "pk_live_replace_me"
dotnet run
```

Open <https://localhost:5044>.

### .NET (Console CLI)

```bash
cd dotnet/Payalo.Sample.Console
dotnet user-secrets set "Payalo:ApiKey" "pk_live_replace_me"
dotnet run -- help
```

### Node.js (Express + TypeScript)

```bash
cd nodejs
cp .env.example .env       # then edit PAYALO_API_KEY
npm install
npm run dev
```

Open <http://localhost:3000>.

### Python (FastAPI)

```bash
cd python
python -m venv .venv
source .venv/bin/activate  # Windows: .venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env       # then edit PAYALO_API_KEY
uvicorn app.main:app --reload
```

Open <http://localhost:8000>.

---

## Webhooks (any sample)

The gateway can't reach `localhost`. Run a public HTTPS tunnel
([ngrok](https://ngrok.com/) is the easiest), point your sample's
`PAYALO_PUBLIC_BASE_URL` (or `Payalo:PublicBaseUrl` in .NET) at the tunnel hostname,
restart the app, and submit a flow. The Webhooks page shows everything the receiver
captures.

---

## Repository layout

```
payalo-integration-examples/
  dotnet/                     # .NET 9 solution
    Payalo.Sample.Web/        # Blazor sample (xUnit tests + Dockerfile + compose)
    Payalo.Sample.Console/    # CLI sample (no webhook receiver)
    tests/
  nodejs/                     # TypeScript + Express + EJS sample
  python/                     # Python 3.12 + FastAPI + Jinja2 sample
  .github/workflows/          # GitHub Actions CI (.NET build + tests)
```

Tests + CI currently cover the .NET sample only. The Node and Python samples are
intentionally test-light to keep them readable; they can grow tests when there's
production demand.
