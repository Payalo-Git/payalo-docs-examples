# PayAlo Merchant Gateway v2 — Integration Examples

Reference applications for the PayAlo Merchant Gateway v2 in three idiomatic stacks.
All three samples cover the same surface — pick the one closest to your production codebase
and read the per-sample README for setup.

| Sample | Stack | README |
|---|---|---|
| **`samples/dotnet/`** | Blazor Web App on .NET 9, typed `HttpClient` + Polly resilience | [README](samples/dotnet/README.md) |
| **`samples/nodejs/`** | TypeScript + Express + EJS, `axios` client | [README](samples/nodejs/README.md) |
| **`samples/python/`** | Python 3.12 + FastAPI + Jinja2, `httpx` client | [README](samples/python/README.md) |

The full V2 API reference is in the sister repo
[`payalo-docs`](https://github.com/Payalo-Git/payalo-docs).

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
cd samples/dotnet/PayAlo.Sample.Web
dotnet user-secrets set "PayAlo:ApiKey" "pk_live_replace_me"
dotnet run
```

Open <https://localhost:5044>.

### Node.js (Express + TypeScript)

```bash
cd samples/nodejs
cp .env.example .env       # then edit PAYALO_API_KEY
npm install
npm run dev
```

Open <http://localhost:3000>.

### Python (FastAPI)

```bash
cd samples/python
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
`PAYALO_PUBLIC_BASE_URL` (or `PayAlo:PublicBaseUrl` in .NET) at the tunnel hostname,
restart the app, and submit a flow. The Webhooks page shows everything the receiver
captures.

---

## Repository layout

```
payalo-integration-examples/
  samples/
    dotnet/          # Blazor + .NET 9 sample, with xUnit tests + Dockerfile + compose
    nodejs/          # TypeScript + Express + EJS sample
    python/          # Python 3.12 + FastAPI + Jinja2 sample
  .github/workflows/ # GitHub Actions CI (.NET build + tests)
```

Tests + CI currently cover the .NET sample only. The Node and Python samples are
intentionally test-light to keep them readable; they can grow tests when there's
production demand.
