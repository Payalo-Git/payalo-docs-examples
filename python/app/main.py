"""FastAPI application entry. All routes for the demo flows live here.

Run with: ``uvicorn app.main:app --reload``
"""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager
from datetime import datetime, timedelta
from typing import Any

from fastapi import FastAPI, Form, Request
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.templating import Jinja2Templates

from .config import settings
from .error_messages import describe
from .merchant_ref import new_merchant_ref
from .payalo_client import (
    DirectTaxPayoutRequest,
    MoneyDto,
    MsisdnPartyDto,
    PayaloApiError,
    PayaloClient,
    PayInRequest,
    PayOutRequest,
    QrPayInRequest,
    WebPayInRequest,
    build_async_client,
)
from .webhook_endpoint import router as webhook_router
from .webhook_store import store

log = logging.getLogger("payalo")
logging.basicConfig(level=logging.INFO)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Build/teardown the shared httpx.AsyncClient that powers the typed PayaloClient."""
    if not settings.api_key:
        log.warning("PAYALO_API_KEY is not set — every gateway call will 401. See README §TL;DR.")
    http = build_async_client()
    app.state.client = PayaloClient(http)
    try:
        yield
    finally:
        await http.aclose()


app = FastAPI(title="Payalo Python Sample", lifespan=lifespan)
templates = Jinja2Templates(directory="templates")
templates.env.globals["describe"] = describe
app.include_router(webhook_router)


def _client(request: Request) -> PayaloClient:
    return request.app.state.client


def _render_error(error: PayaloApiError) -> dict[str, Any]:
    return {
        "status": error.status_code,
        "title": error.title,
        "detail": error.detail or str(error),
        "error_code": error.error_code,
        "friendly": describe(error.error_code, "Check the request body and try again."),
        "is_retryable": error.is_retryable,
        "trace_id": error.trace_id,
    }


# --------------------------------------------------------------------------------------
# Home — config check + live connectivity probe
# --------------------------------------------------------------------------------------
@app.get("/", response_class=HTMLResponse)
async def home(request: Request) -> HTMLResponse:
    return templates.TemplateResponse(
        request, "home.html", {"config": settings, "connectivity": None, "error": None}
    )


@app.post("/connectivity-check", response_class=HTMLResponse)
async def connectivity_check(request: Request) -> HTMLResponse:
    try:
        now = datetime.utcnow()
        page = await _client(request).get_records(
            from_=now - timedelta(minutes=5),
            to=now,
            page_size=1,
        )
        return templates.TemplateResponse(
            request,
            "home.html",
            {"config": settings, "connectivity": {"count": len(page.data)}, "error": None},
        )
    except PayaloApiError as e:
        return templates.TemplateResponse(
            request,
            "home.html",
            {"config": settings, "connectivity": None, "error": _render_error(e)},
        )


# --------------------------------------------------------------------------------------
# Direct Pay-In
# --------------------------------------------------------------------------------------
@app.get("/direct-payin", response_class=HTMLResponse)
async def direct_payin_form(request: Request) -> HTMLResponse:
    return templates.TemplateResponse(
        request,
        "direct-payin.html",
        {"form": _default_payin("dep"), "response": None, "error": None},
    )


@app.post("/direct-payin", response_class=HTMLResponse)
async def direct_payin_submit(
    request: Request,
    method: str = Form(...),
    value: float = Form(...),
    currency: str = Form(...),
    userId: str = Form(...),
    msisdn: str = Form(...),
    country: str = Form(...),
    resultUrl: str = Form(...),
    merchantReference: str = Form(...),
) -> HTMLResponse:
    body = PayInRequest(
        amount=MoneyDto(value=value, currency=currency),
        payer=MsisdnPartyDto(id=userId, msisdn=msisdn),
        country=country,
        result_url=resultUrl,
        merchant_reference=merchantReference,
    )
    return await _submit(
        request, "direct-payin.html", "dep",
        lambda c: c.create_direct_payin(method.strip(), body),
        form_overrides={
            "method": method, "value": value, "currency": currency, "user_id": userId,
            "msisdn": msisdn, "country": country, "result_url": resultUrl,
        },
    )


# --------------------------------------------------------------------------------------
# Direct Pay-Out
# --------------------------------------------------------------------------------------
@app.get("/direct-payout", response_class=HTMLResponse)
async def direct_payout_form(request: Request) -> HTMLResponse:
    return templates.TemplateResponse(
        request,
        "direct-payout.html",
        {"form": _default_payout(), "response": None, "error": None},
    )


@app.post("/direct-payout", response_class=HTMLResponse)
async def direct_payout_submit(
    request: Request,
    method: str = Form(...),
    value: float = Form(...),
    currency: str = Form(...),
    userId: str = Form(...),
    msisdn: str = Form(...),
    country: str = Form(...),
    resultUrl: str = Form(...),
    merchantReference: str = Form(...),
) -> HTMLResponse:
    body = PayOutRequest(
        amount=MoneyDto(value=value, currency=currency),
        payee=MsisdnPartyDto(id=userId, msisdn=msisdn),
        country=country,
        result_url=resultUrl,
        merchant_reference=merchantReference,
    )
    return await _submit(
        request, "direct-payout.html", "payout",
        lambda c: c.create_direct_payout(method.strip(), body),
        form_overrides={
            "method": method, "value": value, "currency": currency, "user_id": userId,
            "msisdn": msisdn, "country": country, "result_url": resultUrl,
        },
    )


# --------------------------------------------------------------------------------------
# Web Pay-In (HPP)
# --------------------------------------------------------------------------------------
@app.get("/web-payin", response_class=HTMLResponse)
async def web_payin_form(request: Request) -> HTMLResponse:
    return templates.TemplateResponse(
        request,
        "web-payin.html",
        {"form": _default_web_payin(), "response": None, "error": None},
    )


@app.post("/web-payin", response_class=HTMLResponse)
async def web_payin_submit(
    request: Request,
    method: str = Form(...),
    value: float = Form(...),
    currency: str = Form(...),
    userId: str = Form(...),
    msisdn: str = Form(...),
    country: str = Form(...),
    resultUrl: str = Form(...),
    successRedirectUrl: str = Form(""),
    errorRedirectUrl: str = Form(""),
    preferredLanguage: str = Form(""),
    merchantReference: str = Form(...),
) -> HTMLResponse:
    body = WebPayInRequest(
        amount=MoneyDto(value=value, currency=currency),
        payer=MsisdnPartyDto(id=userId, msisdn=msisdn),
        country=country,
        result_url=resultUrl,
        success_redirect_url=successRedirectUrl or None,
        error_redirect_url=errorRedirectUrl or None,
        preferred_language=preferredLanguage or None,
        merchant_reference=merchantReference,
    )
    return await _submit(
        request, "web-payin.html", "web",
        lambda c: c.create_web_payin(method.strip(), body),
        form_overrides={
            "method": method, "value": value, "currency": currency, "user_id": userId,
            "msisdn": msisdn, "country": country, "result_url": resultUrl,
            "success_redirect_url": successRedirectUrl, "error_redirect_url": errorRedirectUrl,
            "preferred_language": preferredLanguage,
        },
    )


# --------------------------------------------------------------------------------------
# QR Pay-In
# --------------------------------------------------------------------------------------
@app.get("/qr-payin", response_class=HTMLResponse)
async def qr_payin_form(request: Request) -> HTMLResponse:
    return templates.TemplateResponse(
        request,
        "qr-payin.html",
        {"form": _default_qr_payin(), "response": None, "error": None},
    )


@app.post("/qr-payin", response_class=HTMLResponse)
async def qr_payin_submit(
    request: Request,
    method: str = Form(...),
    value: float = Form(...),
    currency: str = Form(...),
    userId: str = Form(...),
    msisdn: str = Form(...),
    country: str = Form(...),
    resultUrl: str = Form(...),
    displayQrUrl: str = Form(...),
    merchantReference: str = Form(...),
) -> HTMLResponse:
    body = QrPayInRequest(
        amount=MoneyDto(value=value, currency=currency),
        payer=MsisdnPartyDto(id=userId, msisdn=msisdn),
        country=country,
        result_url=resultUrl,
        display_qr_url=displayQrUrl,
        merchant_reference=merchantReference,
    )
    return await _submit(
        request, "qr-payin.html", "qr",
        lambda c: c.create_qr_payin(method.strip(), body),
        form_overrides={
            "method": method, "value": value, "currency": currency, "user_id": userId,
            "msisdn": msisdn, "country": country, "result_url": resultUrl,
            "display_qr_url": displayQrUrl,
        },
    )


# --------------------------------------------------------------------------------------
# Direct Tax Payout
# --------------------------------------------------------------------------------------
@app.get("/direct-tax-payout", response_class=HTMLResponse)
async def direct_tax_payout_form(request: Request) -> HTMLResponse:
    return templates.TemplateResponse(
        request,
        "direct-tax-payout.html",
        {"form": _default_tax_payout(), "response": None, "error": None},
    )


@app.post("/direct-tax-payout", response_class=HTMLResponse)
async def direct_tax_payout_submit(
    request: Request,
    method: str = Form(...),
    value: float = Form(...),
    currency: str = Form(...),
    country: str = Form(...),
    resultUrl: str = Form(...),
    merchantReference: str = Form(...),
    reconciliationReference: str = Form(...),
) -> HTMLResponse:
    body = DirectTaxPayoutRequest(
        amount=MoneyDto(value=value, currency=currency),
        country=country,
        result_url=resultUrl,
        merchant_reference=merchantReference,
        reconciliation_reference=reconciliationReference,
    )
    try:
        response = await _client(request).create_direct_tax_payout(method.strip(), body)
        next_form = _default_tax_payout()
        return templates.TemplateResponse(
            request,
            "direct-tax-payout.html",
            {"form": next_form, "response": response, "error": None},
        )
    except PayaloApiError as e:
        next_form = {
            "method": method, "value": value, "currency": currency, "country": country,
            "result_url": resultUrl,
            "merchant_reference": new_merchant_ref("tax"),
            "reconciliation_reference": _new_tax_recon(),
        }
        return templates.TemplateResponse(
            request,
            "direct-tax-payout.html",
            {"form": next_form, "response": None, "error": _render_error(e)},
        )


# --------------------------------------------------------------------------------------
# Status
# --------------------------------------------------------------------------------------
@app.get("/status", response_class=HTMLResponse)
async def status_page(request: Request, gw: str = "", mref: str = "") -> HTMLResponse:
    if not gw and not mref:
        return templates.TemplateResponse(
            request, "status.html", {"transaction": None, "error": None, "gw": "", "mref": ""}
        )
    try:
        client = _client(request)
        transaction = (
            await client.get_by_gateway_reference(gw.strip())
            if gw
            else await client.get_by_merchant_reference(mref.strip())
        )
        return templates.TemplateResponse(
            request,
            "status.html",
            {"transaction": transaction, "error": None, "gw": gw, "mref": mref},
        )
    except PayaloApiError as e:
        return templates.TemplateResponse(
            request,
            "status.html",
            {"transaction": None, "error": _render_error(e), "gw": gw, "mref": mref},
        )


# --------------------------------------------------------------------------------------
# Records
# --------------------------------------------------------------------------------------
@app.get("/records", response_class=HTMLResponse)
async def records_page(request: Request) -> HTMLResponse:
    qp = request.query_params
    now = datetime.utcnow()
    filters: dict[str, Any] = {
        "from": qp.get("from") or (now - timedelta(days=1)).strftime("%Y-%m-%dT%H:%M"),
        "to": qp.get("to") or now.strftime("%Y-%m-%dT%H:%M"),
        "type": (qp.get("type") or "").strip(),
        "method": (qp.get("method") or "").strip(),
        "status": (qp.get("status") or "").strip(),
        "page_size": int(qp.get("pageSize") or 50),
        "cursor": (qp.get("page") or "").strip(),
    }
    searched = bool(qp.get("searched")) or bool(filters["cursor"])

    if not searched:
        return templates.TemplateResponse(
            request, "records.html", {"filters": filters, "page_data": None, "error": None}
        )

    try:
        page_data = await _client(request).get_records(
            from_=datetime.fromisoformat(filters["from"]),
            to=datetime.fromisoformat(filters["to"]),
            type_=filters["type"] or None,
            method=filters["method"] or None,
            status=filters["status"] or None,
            page_size=filters["page_size"],
            page_cursor=filters["cursor"] or None,
        )
        return templates.TemplateResponse(
            request, "records.html", {"filters": filters, "page_data": page_data, "error": None}
        )
    except PayaloApiError as e:
        return templates.TemplateResponse(
            request,
            "records.html",
            {"filters": filters, "page_data": None, "error": _render_error(e)},
        )


# --------------------------------------------------------------------------------------
# Webhooks
# --------------------------------------------------------------------------------------
@app.get("/webhooks", response_class=HTMLResponse)
async def webhooks_page(request: Request) -> HTMLResponse:
    return templates.TemplateResponse(
        request, "webhooks.html", {"config": settings, "entries": store.snapshot()}
    )


@app.post("/webhooks/clear")
async def webhooks_clear() -> RedirectResponse:
    store.clear()
    return RedirectResponse(url="/webhooks", status_code=303)


# --------------------------------------------------------------------------------------
# Form helpers
# --------------------------------------------------------------------------------------

async def _submit(
    request: Request,
    template: str,
    prefix: str,
    call,
    form_overrides: dict[str, Any],
) -> HTMLResponse:
    """Common pay-in/pay-out submit helper. Handles success + PayaloApiError, and
    auto-bumps the merchantReference on the next-form so a quick re-submit doesn't
    trip the gateway's duplicate-reference rule.
    """
    try:
        response = await call(_client(request))
        next_form = {**form_overrides, "merchant_reference": new_merchant_ref(prefix)}
        return templates.TemplateResponse(
            request, template, {"form": next_form, "response": response, "error": None}
        )
    except PayaloApiError as e:
        next_form = {**form_overrides, "merchant_reference": new_merchant_ref(prefix)}
        return templates.TemplateResponse(
            request, template, {"form": next_form, "response": None, "error": _render_error(e)}
        )


def _default_payin(prefix: str) -> dict[str, Any]:
    return {
        "method": "mpesa-ke",
        "value": 500,
        "currency": "KES",
        "user_id": "user-42",
        "msisdn": "+254712345678",
        "country": "KE",
        "result_url": settings.result_url,
        "merchant_reference": new_merchant_ref(prefix),
    }


def _default_payout() -> dict[str, Any]:
    return {**_default_payin("payout"), "value": 1000}


def _default_web_payin() -> dict[str, Any]:
    base = settings.public_base_url.rstrip("/") if settings.public_base_url else "https://example.com"
    return {
        **_default_payin("web"),
        "value": 25,
        "currency": "EUR",
        "country": "DE",
        "msisdn": "+491234567890",
        "success_redirect_url": f"{base}/payment/success",
        "error_redirect_url": f"{base}/payment/error",
        "preferred_language": "fr-FR",
    }


def _default_qr_payin() -> dict[str, Any]:
    base = settings.public_base_url.rstrip("/") if settings.public_base_url else "https://example.com"
    return {**_default_payin("qr"), "display_qr_url": f"{base}/qr/render"}


def _default_tax_payout() -> dict[str, Any]:
    return {
        "method": "tax-ke",
        "value": 15000,
        "currency": "KES",
        "country": "KE",
        "result_url": settings.result_url,
        "merchant_reference": new_merchant_ref("tax"),
        "reconciliation_reference": _new_tax_recon(),
    }


def _new_tax_recon() -> str:
    return f"TAX-{datetime.utcnow().strftime('%Y%m')}-{new_merchant_ref('').lstrip('-')[:6]}"
