# Payalo Merchant Gateway v2 — Python sample

Python + FastAPI + httpx reference for the Payalo Merchant Gateway v2.

Same surface as the [.NET sample](../dotnet/) and the [Node.js sample](../nodejs/):
Direct Pay-In, Web Pay-In (HPP), Direct Pay-Out, QR Pay-In, Direct Tax Payout, status
lookup, records, webhook receiver with `X-API-KEY` validation and idempotent dedupe,
RFC 7807 error parsing.

## TL;DR

```bash
cd python
python -m venv .venv
source .venv/bin/activate     # Windows: .venv\Scripts\activate
pip install -r requirements.txt

cp .env.example .env          # then edit PAYALO_API_KEY
uvicorn app.main:app --reload
```

Open <http://localhost:8000>.

## Prerequisites

- Python 3.12+ (`python --version` ≥ 3.12).
- A Payalo brand API key.
- *(For webhooks)* a public HTTPS tunnel like ngrok — the gateway can't reach `localhost`.

## Configuration

| Env var | Required | Default |
|---|---|---|
| `PAYALO_API_KEY` | yes | *(unset)* |
| `PAYALO_BASE_URL` | no | `https://dev-api-internal.payalo.com/gateway/mmo/v2/` |
| `PAYALO_WEBHOOK_PATH` | no | `/webhooks/payalo` |
| `PAYALO_PUBLIC_BASE_URL` | recommended | *(unset)* — your public tunnel URL |
| `PORT` | no | `8000` |

`.env` is loaded automatically via `pydantic-settings` and is gitignored.

## Webhooks

1. `uvicorn app.main:app --reload` — note the port (default `8000`).
2. `ngrok http http://localhost:8000` — copy the public HTTPS URL.
3. Set `PAYALO_PUBLIC_BASE_URL` and restart.
4. The Webhooks page shows the URL the gateway should reach. Submit a flow, then
   refresh that page to see captured callbacks.

## Project layout

```
python/
  app/
    main.py                # FastAPI app + all routes
    config.py              # PAYALO_* env-var loader (pydantic-settings)
    payalo_client.py       # typed gateway client + DTOs + PayaloApiError
    error_messages.py      # async-error-code → friendly text
    webhook_endpoint.py    # POST /webhooks/payalo (X-API-KEY check + dedupe)
    webhook_store.py       # in-memory webhook log (DEMO ONLY)
    merchant_ref.py        # one-line UUID generator
  templates/               # Jinja2 templates (one per page + small partials)
  requirements.txt
```

## Notes

- Every form regenerates `merchantReference` after each submit (success or failure) so
  a quick re-submit doesn't trip the gateway's duplicate-reference rule.
- The webhook store is in-memory and DEMO-ONLY. Swap the `store` import in
  `webhook_endpoint.py` for a database-backed implementation in production.
- Pydantic models use `populate_by_name = True` plus a camelCase alias generator so
  the wire payloads match the documented JSON while the Python side stays snake_case.
