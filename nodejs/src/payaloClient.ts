import axios, { type AxiosInstance, AxiosError } from "axios";
import { config } from "./config.js";

// ----------------------------------------------------------------------------------
// DTOs that mirror the V2 contract. We hand-write them here rather than auto-generate
// from OpenAPI so the sample is self-contained.
// ----------------------------------------------------------------------------------

export interface MoneyDto {
  value: number;
  currency: string;
}

export interface MsisdnPartyDto {
  id: string;
  msisdn: string;
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
}

export interface PayInRequest {
  amount: MoneyDto;
  payer: MsisdnPartyDto;
  country: string;
  resultUrl: string;
  merchantReference: string;
  reconciliationReference?: string | null;
  labels?: Record<string, string>;
}

export interface PayOutRequest {
  amount: MoneyDto;
  payee: MsisdnPartyDto;
  country: string;
  resultUrl: string;
  merchantReference: string;
  reconciliationReference?: string | null;
  labels?: Record<string, string>;
}

export interface WebPayInRequest extends PayInRequest {
  successRedirectUrl?: string | null;
  errorRedirectUrl?: string | null;
}

export interface QrPayInRequest extends PayInRequest {
  displayQrUrl: string;
}

export interface DirectTaxPayoutRequest {
  amount: MoneyDto;
  country: string;
  resultUrl: string;
  merchantReference: string;
  reconciliationReference: string;
  labels?: Record<string, string>;
}

export interface InitiatePaymentResponse {
  status: string;
  gatewayReference: string;
  merchantReference: string;
  reconciliationReference: string;
  createdAt: string;
}

export interface WebPayInResponse extends InitiatePaymentResponse {
  pageUrl: string;
  pageOpenMode: string;
}

export interface ProviderDataDto {
  name: string;
  title: string;
  fee?: MoneyDto | null;
  partyData?: Record<string, string> | null;
  errorCode?: string | null;
  errorMessage?: string | null;
}

export interface MmoTransactionDto {
  status: string;
  type: string;
  flow: string;
  gatewayReference: string;
  // Null for push (provider-initiated) transactions — no merchant request to echo.
  merchantReference: string | null;
  // Null for push transactions; otherwise the value you submitted, or the gateway's
  // fallback to gatewayReference.
  reconciliationReference: string | null;
  providerReference?: string | null;
  party: MsisdnPartyDto;
  method: string;
  country: string;
  requestedAmount: MoneyDto;
  finalAmount?: MoneyDto | null;
  labels?: Record<string, string> | null;
  createdAt: string;
  completedAt?: string | null;
  completionSource?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  providerData?: ProviderDataDto | null;
}

export interface RecordsResponse {
  data: MmoTransactionDto[];
  pages: { next: string | null; previous: string | null };
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
  errorCode?: string;
}

// ----------------------------------------------------------------------------------
// Error wrapper
// ----------------------------------------------------------------------------------

export class PayaloApiError extends Error {
  readonly statusCode: number;
  readonly problem: ProblemDetails | null;
  readonly rawBody: string | null;
  readonly traceId: string | null;

  constructor(statusCode: number, problem: ProblemDetails | null, rawBody: string | null, traceId: string | null) {
    super(buildMessage(statusCode, problem, rawBody));
    this.statusCode = statusCode;
    this.problem = problem;
    this.rawBody = rawBody;
    this.traceId = traceId;
  }

  get errorCode(): string | undefined { return this.problem?.errorCode; }
  get title(): string | undefined { return this.problem?.title; }
  get detail(): string | undefined { return this.problem?.detail; }

  // 4xx (except 408/429) should never be retried automatically — the request itself
  // is bad, and retrying with the same merchantReference would burn the idempotency key.
  get isRetryable(): boolean {
    return this.statusCode >= 500 || this.statusCode === 408 || this.statusCode === 429;
  }
}

function buildMessage(statusCode: number, problem: ProblemDetails | null, rawBody: string | null): string {
  if (problem) return `Payalo gateway returned ${statusCode}: [${problem.errorCode}] ${problem.detail}`;
  const snippet = rawBody ? ` Body: ${rawBody.slice(0, 500)}` : "";
  return `Payalo gateway returned ${statusCode}.${snippet}`;
}

// ----------------------------------------------------------------------------------
// Typed client. One method per documented endpoint.
// ----------------------------------------------------------------------------------

export class PayaloClient {
  private readonly http: AxiosInstance;

  constructor() {
    const baseUrl = config.baseUrl.endsWith("/") ? config.baseUrl : config.baseUrl + "/";
    this.http = axios.create({
      baseURL: baseUrl,
      headers: {
        Accept: "application/json, application/problem+json",
        "User-Agent": "Payalo.Sample.Node/1.0",
      },
      // Resolve all responses (don't throw on 4xx/5xx) so we can surface a typed error.
      validateStatus: () => true,
    });

    // Inject the X-Api-Key on every request — the equivalent of the .NET sample's
    // ApiKeyHandler DelegatingHandler.
    this.http.interceptors.request.use((req) => {
      if (config.apiKey) req.headers.set("X-Api-Key", config.apiKey);
      return req;
    });
  }

  createDirectPayIn(method: string, body: PayInRequest) {
    return this.post<InitiatePaymentResponse>(`direct/payin/${encodeURIComponent(method)}`, body);
  }

  createDirectPayOut(method: string, body: PayOutRequest) {
    return this.post<InitiatePaymentResponse>(`direct/payout/${encodeURIComponent(method)}`, body);
  }

  createWebPayIn(method: string, body: WebPayInRequest) {
    return this.post<WebPayInResponse>(`web/payin/${encodeURIComponent(method)}`, body);
  }

  createQrPayIn(method: string, body: QrPayInRequest) {
    return this.post<InitiatePaymentResponse>(`qr/payin/${encodeURIComponent(method)}`, body);
  }

  createDirectTaxPayout(method: string, body: DirectTaxPayoutRequest) {
    return this.post<InitiatePaymentResponse>(`direct/taxpayout/${encodeURIComponent(method)}`, body);
  }

  getByGatewayReference(ref: string) {
    return this.get<MmoTransactionDto>(`status/${encodeURIComponent(ref)}`);
  }

  getByMerchantReference(ref: string) {
    return this.get<MmoTransactionDto>(`status/mref/${encodeURIComponent(ref)}`);
  }

  // The gateway's records endpoint uses cursor pagination: the cursor encodes the
  // original filter set, so on follow-up pages we send only `page`.
  getRecords(params: {
    from: Date;
    to: Date;
    type?: string;
    method?: string;
    status?: string;
    pageSize?: number;
    pageCursor?: string;
  }) {
    const query: Record<string, string> = {};
    if (params.pageCursor) {
      query.page = params.pageCursor;
    } else {
      query.from = params.from.toISOString();
      query.to = params.to.toISOString();
      if (params.type) query.type = params.type;
      if (params.method) query.method = params.method;
      if (params.status) query.status = params.status;
      if (params.pageSize !== undefined) query.pageSize = String(params.pageSize);
    }
    const qs = new URLSearchParams(query).toString();
    return this.get<RecordsResponse>(`records?${qs}`);
  }

  // ----------------------------------------------------------------------------------
  // Internal helpers
  // ----------------------------------------------------------------------------------

  private async post<TResponse>(path: string, body: unknown): Promise<TResponse> {
    try {
      const response = await this.http.post<TResponse | ProblemDetails>(path, body);
      return this.handle<TResponse>(response.status, response.data, response.headers as Record<string, string>);
    } catch (e) {
      throw this.wrapNetworkError(e);
    }
  }

  private async get<TResponse>(path: string): Promise<TResponse> {
    try {
      const response = await this.http.get<TResponse | ProblemDetails>(path);
      return this.handle<TResponse>(response.status, response.data, response.headers as Record<string, string>);
    } catch (e) {
      throw this.wrapNetworkError(e);
    }
  }

  private handle<TResponse>(status: number, body: unknown, headers: Record<string, string>): TResponse {
    const traceId = headers["traceparent"] ?? null;
    if (status >= 200 && status < 300) {
      return body as TResponse;
    }
    // Body is either a parsed ProblemDetails or whatever the gateway returned. axios
    // auto-parses application/json + application/problem+json, so when the body has an
    // `errorCode` we treat it as Problem Details.
    const problem = body && typeof body === "object" && "errorCode" in body
      ? (body as ProblemDetails)
      : null;
    const rawBody = problem ? null : (typeof body === "string" ? body : JSON.stringify(body));
    throw new PayaloApiError(status, problem, rawBody, traceId);
  }

  private wrapNetworkError(e: unknown): Error {
    if (e instanceof AxiosError) {
      // No HTTP response (DNS/timeout/socket close). Surface as a 0-status error.
      return new PayaloApiError(0, null, e.message, null);
    }
    return e instanceof Error ? e : new Error(String(e));
  }
}
