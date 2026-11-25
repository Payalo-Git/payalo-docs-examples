"""Inbound webhook receiver — equivalent of the .NET WebhookEndpoint minimal API.

The gateway:
  - POSTs application/json to your ``resultUrl``,
  - includes the ``X-API-KEY`` header set to *your* brand API key,
  - delivers the same V2 ``MmoTransactionDto`` shape returned by GET /status/{ref},
  - expects any 2xx response within 15 seconds; the response body is optional and informational only,
  - makes ONE attempt per terminal state change and does not retry: if a delivery fails,
    reconcile with Status or Records,
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


@router.post(settings.webhook_path + "/qr")
async def receive_qr_data(
    request: Request,
    x_api_key: str | None = Header(default=None, alias="X-API-KEY"),
) -> Response:
    """Receives the QR data of a QR pay-in (the ``displayQrUrl`` request).

    Payalo POSTs JSON with gatewayReference, merchantReference, qrCode, deepLink, message and
    expiresAt (any of the last four can be null). Answer with a 2xx status as soon as the data
    arrives; a real checkout would then show the QR code. Payalo sends up to 6 requests, so the
    same data can arrive more than once.
    """
    if not x_api_key or not _fixed_time_equals(x_api_key, settings.api_key):
        log.warning("Rejected QR data request — X-API-KEY missing or mismatched")
        return Response(status_code=status.HTTP_401_UNAUTHORIZED)
    try:
        data: dict[str, Any] = json.loads((await request.body()).decode("utf-8", errors="replace"))
    except json.JSONDecodeError:
        log.warning("QR data request body was not valid JSON")
        return Response(status_code=status.HTTP_400_BAD_REQUEST)
    if not isinstance(data, dict):
        log.warning("QR data request body was not a JSON object")
        return Response(status_code=status.HTTP_400_BAD_REQUEST)
    log.info(
        "QR data received: gatewayReference=%s qrCode=%s deepLink=%s expiresAt=%s",
        data.get("gatewayReference"),
        "yes" if data.get("qrCode") else "no",
        "yes" if data.get("deepLink") else "no",
        data.get("expiresAt"),
    )
    return Response(status_code=status.HTTP_200_OK)


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

    if not payload.get("gatewayReference") and payload.get("id"):
        # A push callback has the older body: `id` and `status`, no gatewayReference.
        log.info("Push callback for id=%s: read GET /status/{id} for the full transaction", payload.get("id"))
        return Response(
            content=json.dumps({"status": "ok"}),
            media_type="application/json",
            status_code=status.HTTP_200_OK,
        )

    if not payload.get("gatewayReference"):
        log.warning("Webhook body parsed but is missing required `gatewayReference` field")
        return Response(
            content=json.dumps({"status": "error"}),
            media_type="application/json",
            status_code=status.HTTP_400_BAD_REQUEST,
        )

    # 3) Persist (idempotently). Returns False when the same gatewayReference and status
    #    arrive again.
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
    #    inline here. The gateway makes one attempt and does not retry, so a failed
    #    delivery is only recovered by reading Status or Records.
    return Response(
        content=json.dumps({"status": "ok"}),
        media_type="application/json",
        status_code=status.HTTP_200_OK,
    )
