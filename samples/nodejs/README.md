# PayAlo Merchant Gateway v2 — Node.js sample

Node.js + Express + TypeScript reference for the PayAlo Merchant Gateway v2.

Same surface as the [.NET sample](../dotnet/): Direct Pay-In, Web Pay-In (HPP), Direct
Pay-Out, QR Pay-In, Direct Tax Payout, status lookup, records, webhook receiver with
`X-API-KEY` validation and idempotent dedupe, RFC 7807 error parsing.

## TL;DR

```bash
cd samples/nodejs
cp .env.example .env             # then edit PAYALO_API_KEY
npm install
npm run dev
```

Open <http://localhost:3000>.

## Prerequisites

- Node.js 20+ (`node --version` ≥ 20.0.0).
- A PayAlo brand API key.
- *(For webhooks)* a public HTTPS tunnel like ngrok — the gateway can't reach `localhost`.

## Configuration

| Env var | Required | Default |
|---|---|---|
| `PAYALO_API_KEY` | yes | *(unset)* |
| `PAYALO_BASE_URL` | no | `https://dev-api-internal.payalo.com/gateway/mmo/v2/` |
| `PAYALO_WEBHOOK_PATH` | no | `/webhooks/payalo` |
| `PAYALO_PUBLIC_BASE_URL` | recommended | *(unset)* — your public tunnel URL |
| `PORT` | no | `3000` |

`.env` is loaded automatically via `dotenv` and is gitignored.

## Webhooks

1. `npm run dev` — note the port (default `3000`).
2. `ngrok http http://localhost:3000` — copy the public HTTPS URL.
3. Set `PAYALO_PUBLIC_BASE_URL` and restart.
4. The Webhooks page shows the URL the gateway should reach. Submit a flow, then watch
   the page — it lists every callback received.

## Project layout

```
samples/nodejs/
  src/
    server.ts              # Express setup + all routes
    config.ts              # PAYALO_* env-var loader
    payaloClient.ts        # typed gateway client + DTOs + PayAloApiError
    errorMessages.ts       # async-error-code → friendly text
    webhookEndpoint.ts     # POST /webhooks/payalo (X-API-KEY check + dedupe)
    webhookStore.ts        # in-memory webhook log (DEMO ONLY)
    merchantRef.ts         # one-line UUID generator
  views/                   # EJS templates (one per page + small partials)
  package.json
  tsconfig.json
```

## Notes

- Every form regenerates `merchantReference` after each submit (success or failure) so
  a quick re-submit doesn't trip the gateway's duplicate-reference rule.
- The webhook store is in-memory and DEMO-ONLY. Swap the import in `server.ts` for a
  database-backed store in production.
- No build step — `npm run dev` runs TypeScript directly via `tsx`. There is no `dist/`.
