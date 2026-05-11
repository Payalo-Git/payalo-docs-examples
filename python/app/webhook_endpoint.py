"""Inbound webhook receiver — equivalent of the .NET WebhookEndpoint minimal API.

The gateway:
  - POSTs application/json to your ``resultUrl``,
  - includes the ``X-API-KEY`` header set to *your* brand API key,
  - delivers the same V2 ``MmoTransactionDto`` shape returned by GET /status/{ref},
  - expects any 2xx response within 15 seconds; the response body is optional and informational only,
  - retries up to 5 times with linear backoff on non-2xx,
  - may redeliver an already-acknowledged callback in rare cases (be idempotent).
"""

from __future__ import annotations

import hashlib
import hmac
import json
import logging
from typing import Any

from fastapi import APIRouter, Header, Request, Response, status

from .config import settings
from .webhook_store import store

router = APIRouter()
log = logging.getLogger("payalo.webhook")


def _fixed_time_equals(a: str, b: str) -> bool:
    """Constant-time string comparison via SHA-256 hashes so input length doesn't leak."""
    a_hash = hashlib.sha256(a.encode("utf-8")).digest()
    b_hash = hashlib.sha256(b.encode("utf-8")).digest()
    return hmac.compare_digest(a_hash, b_hash)


@router.post(settings.webhook_path)
async def receive_webhook(
    request: Request,
    x_api_key: str | None = Header(default=None, alias="X-API-KEY"),
) -> Response:
    # 1) Authenticate. The only signal is X-API-KEY echoing our own brand key.
    if not x_api_key or not _fixed_time_equals(x_api_key, settings.api_key):
        log.warning("Rejected webhook from %s — X-API-KEY missing or mismatched", request.client.host if request.client else "?")
        return Response(status_code=status.HTTP_401_UNAUTHORIZED)

    # 2) Parse the body. We keep the raw bytes for the demo UI alongside the parsed view.
    raw_bytes = await request.body()
    raw_body = raw_bytes.decode("utf-8", errors="replace")
    try:
        payload: dict[str, Any] = json.loads(raw_body)
    except json.JSONDecodeError:
        log.warning("Webhook body was not valid JSON")
        return Response(
            content=json.dumps({"status": "error"}),
            media_type="application/json",
            status_code=status.HTTP_400_BAD_REQUEST,
        )

    if not payload.get("gatewayReference"):
        log.warning("Webhook body parsed but is missing required `gatewayReference` field")
        return Response(
            content=json.dumps({"status": "error"}),
            media_type="application/json",
            status_code=status.HTTP_400_BAD_REQUEST,
        )

    # 3) Persist (idempotently). Returns False on duplicate delivery.
    source_ip = request.client.host if request.client else "unknown"
    is_new = store.try_record(payload, raw_body, source_ip)
    log.info(
        "Webhook %s: gatewayReference=%s status=%s duplicate=%s",
        "accepted" if is_new else "deduplicated",
        payload.get("gatewayReference"),
        payload.get("status"),
        not is_new,
    )

    # 4) Always reply "ok" within 15s. Heavy work belongs in a background queue, not
    #    inline here. Acknowledging duplicates as "ok" stops the gateway from retrying.
    return Response(
        content=json.dumps({"status": "ok"}),
        media_type="application/json",
        status_code=status.HTTP_200_OK,
    )
