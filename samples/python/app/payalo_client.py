"""Typed PayAlo gateway client + DTOs + error type.

One method per documented endpoint. We use httpx with a single AsyncClient (created at
app startup, closed at shutdown) and inject the X-Api-Key header as a default for every
outbound request — the equivalent of the .NET sample's ApiKeyHandler DelegatingHandler.
"""

from __future__ import annotations

from datetime import datetime
from typing import Any

import httpx
from pydantic import BaseModel, ConfigDict, Field

from .config import settings


# --------------------------------------------------------------------------------------
# DTOs that mirror the V2 contract.
# --------------------------------------------------------------------------------------

class _Camel(BaseModel):
    """Base model: serialises C# / JS-style camelCase wire fields, keeps Python snake_case
    on the Python side. Set ``populate_by_name`` so consumers can pass either form.
    """
    model_config = ConfigDict(populate_by_name=True, extra="allow")


def _camel(name: str) -> str:
    parts = name.split("_")
    return parts[0] + "".join(p.title() for p in parts[1:])


class _CamelOut(_Camel):
    model_config = ConfigDict(
        populate_by_name=True,
        alias_generator=_camel,
        extra="allow",
    )


class MoneyDto(_CamelOut):
    value: float
    currency: str


class MsisdnPartyDto(_CamelOut):
    id: str
    msisdn: str
    first_name: str | None = None
    last_name: str | None = None
    email: str | None = None


class PayInRequest(_CamelOut):
    amount: MoneyDto
    payer: MsisdnPartyDto
    country: str
    result_url: str
    merchant_reference: str
    reconciliation_reference: str | None = None
    labels: dict[str, str] | None = None


class PayOutRequest(_CamelOut):
    amount: MoneyDto
    payee: MsisdnPartyDto
    country: str
    result_url: str
    merchant_reference: str
    reconciliation_reference: str | None = None
    labels: dict[str, str] | None = None


class WebPayInRequest(PayInRequest):
    success_redirect_url: str | None = None
    error_redirect_url: str | None = None


class QrPayInRequest(PayInRequest):
    display_qr_url: str


class DirectTaxPayoutRequest(_CamelOut):
    amount: MoneyDto
    country: str
    result_url: str
    merchant_reference: str
    reconciliation_reference: str  # required for tax payouts
    labels: dict[str, str] | None = None


class InitiatePaymentResponse(_CamelOut):
    status: str
    gateway_reference: str
    merchant_reference: str
    reconciliation_reference: str
    created_at: datetime


class WebPayInResponse(InitiatePaymentResponse):
    page_url: str
    page_open_mode: str


class ProviderDataDto(_CamelOut):
    name: str
    title: str
    fee: MoneyDto | None = None
    party_data: dict[str, str] | None = None
    error_code: str | None = None
    error_message: str | None = None


class MmoTransactionDto(_CamelOut):
    status: str
    type: str
    flow: str
    gateway_reference: str
    # Null for push (provider-initiated) transactions — no merchant request to echo.
    merchant_reference: str | None = None
    # Null for push transactions; otherwise the value you submitted, or the gateway's
    # fallback to gateway_reference.
    reconciliation_reference: str | None = None
    provider_reference: str | None = None
    party: MsisdnPartyDto
    method: str
    country: str
    requested_amount: MoneyDto
    final_amount: MoneyDto | None = None
    labels: dict[str, str] | None = None
    created_at: datetime
    completed_at: datetime | None = None
    completion_source: str | None = None
    error_code: str | None = None
    error_message: str | None = None
    provider_data: ProviderDataDto | None = None


class _RecordsPages(BaseModel):
    next: str | None = None
    previous: str | None = None


class RecordsResponse(BaseModel):
    data: list[MmoTransactionDto]
    pages: _RecordsPages


class ProblemDetails(BaseModel):
    type: str | None = None
    title: str | None = None
    status: int
    detail: str | None = None
    error_code: str | None = Field(default=None, alias="errorCode")

    model_config = ConfigDict(populate_by_name=True)


# --------------------------------------------------------------------------------------
# Error wrapper
# --------------------------------------------------------------------------------------

class PayAloApiError(Exception):
    def __init__(
        self,
        status_code: int,
        problem: ProblemDetails | None,
        raw_body: str | None,
        trace_id: str | None,
    ) -> None:
        self.status_code = status_code
        self.problem = problem
        self.raw_body = raw_body
        self.trace_id = trace_id
        super().__init__(self._build_message())

    @property
    def error_code(self) -> str | None:
        return self.problem.error_code if self.problem else None

    @property
    def title(self) -> str | None:
        return self.problem.title if self.problem else None

    @property
    def detail(self) -> str | None:
        return self.problem.detail if self.problem else None

    @property
    def is_retryable(self) -> bool:
        # 4xx (except 408/429) should never be retried — retrying with the same
        # merchantReference would burn the idempotency key.
        return self.status_code >= 500 or self.status_code in (408, 429)

    def _build_message(self) -> str:
        if self.problem:
            return (
                f"PayAlo gateway returned {self.status_code}: "
                f"[{self.problem.error_code}] {self.problem.detail}"
            )
        snippet = f" Body: {self.raw_body[:500]}" if self.raw_body else ""
        return f"PayAlo gateway returned {self.status_code}.{snippet}"


# --------------------------------------------------------------------------------------
# Client
# --------------------------------------------------------------------------------------

class PayAloClient:
    """Thin async wrapper around httpx.AsyncClient. One method per documented endpoint."""

    def __init__(self, http: httpx.AsyncClient) -> None:
        self._http = http

    async def create_direct_payin(self, method: str, body: PayInRequest) -> InitiatePaymentResponse:
        return await self._post(f"direct/payin/{method}", body, InitiatePaymentResponse)

    async def create_direct_payout(self, method: str, body: PayOutRequest) -> InitiatePaymentResponse:
        return await self._post(f"direct/payout/{method}", body, InitiatePaymentResponse)

    async def create_web_payin(self, method: str, body: WebPayInRequest) -> WebPayInResponse:
        return await self._post(f"web/payin/{method}", body, WebPayInResponse)

    async def create_qr_payin(self, method: str, body: QrPayInRequest) -> InitiatePaymentResponse:
        return await self._post(f"qr/payin/{method}", body, InitiatePaymentResponse)

    async def create_direct_tax_payout(self, method: str, body: DirectTaxPayoutRequest) -> InitiatePaymentResponse:
        return await self._post(f"direct/taxpayout/{method}", body, InitiatePaymentResponse)

    async def get_by_gateway_reference(self, ref: str) -> MmoTransactionDto:
        return await self._get(f"status/{ref}", MmoTransactionDto)

    async def get_by_merchant_reference(self, ref: str) -> MmoTransactionDto:
        return await self._get(f"status/mref/{ref}", MmoTransactionDto)

    async def get_records(
        self,
        *,
        from_: datetime,
        to: datetime,
        type_: str | None = None,
        method: str | None = None,
        status: str | None = None,
        page_size: int | None = None,
        page_cursor: str | None = None,
    ) -> RecordsResponse:
        # Cursor pagination: cursor encodes the original filters, so on follow-up pages
        # we send only `page` and let the gateway re-apply the original filter set.
        params: dict[str, str] = {}
        if page_cursor:
            params["page"] = page_cursor
        else:
            params["from"] = from_.isoformat()
            params["to"] = to.isoformat()
            if type_:
                params["type"] = type_
            if method:
                params["method"] = method
            if status:
                params["status"] = status
            if page_size is not None:
                params["pageSize"] = str(page_size)
        return await self._get("records", RecordsResponse, params=params)

    # ----------------------------------------------------------------------------------
    # Internal helpers
    # ----------------------------------------------------------------------------------

    async def _post(self, path: str, body: BaseModel, response_model: type[BaseModel]) -> Any:
        response = await self._http.post(
            path,
            json=body.model_dump(by_alias=True, exclude_none=True),
        )
        return self._handle(response, response_model)

    async def _get(
        self,
        path: str,
        response_model: type[BaseModel] | None,
        *,
        params: dict[str, str] | None = None,
    ) -> Any:
        response = await self._http.get(path, params=params)
        return self._handle(response, response_model)

    def _handle(self, response: httpx.Response, response_model: type[BaseModel] | None) -> Any:
        trace_id = response.headers.get("traceparent")
        if response.is_success:
            payload = response.json() if response.content else None
            if response_model is None:
                return payload
            return response_model.model_validate(payload)

        # Error path: parse Problem Details if it looks like JSON; otherwise surface the raw body.
        problem: ProblemDetails | None = None
        raw_body: str | None = None
        ctype = response.headers.get("content-type", "")
        if "json" in ctype.lower():
            try:
                problem = ProblemDetails.model_validate(response.json())
            except (ValueError, KeyError):
                raw_body = response.text
        else:
            raw_body = response.text
        raise PayAloApiError(response.status_code, problem, raw_body, trace_id)


def build_async_client() -> httpx.AsyncClient:
    """Construct the configured httpx client. Called once at app startup."""
    base = settings.base_url if settings.base_url.endswith("/") else settings.base_url + "/"
    headers = {
        "Accept": "application/json, application/problem+json",
        "User-Agent": "PayAlo.Sample.Python/1.0",
    }
    if settings.api_key:
        headers["X-Api-Key"] = settings.api_key
    return httpx.AsyncClient(base_url=base, headers=headers, timeout=30.0)
