import express, { type Request, type Response } from "express";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { config, resultUrl } from "./config.js";
import { PayaloApiError, PayaloClient } from "./payaloClient.js";
import { describeError } from "./errorMessages.js";
import { newMerchantRef } from "./merchantRef.js";
import { snapshot, clear as clearWebhooks } from "./webhookStore.js";
import { webhookRouter } from "./webhookEndpoint.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const client = new PayaloClient();

const app = express();
// Make the HTML-escaper available to every EJS view. Used when interpolating user input
// into the raw-HTML `extraFields` blobs that the form partial emits unescaped via <%- %>.
app.locals.escapeHtml = escapeHtml;
app.set("view engine", "ejs");
app.set("views", path.resolve(__dirname, "..", "views"));
app.use(express.urlencoded({ extended: false }));
// Webhook router is mounted FIRST so it gets the raw JSON body, before the urlencoded parser.
app.use(webhookRouter());

// Pretty-print errors so demo pages can render a meaningful problem-details panel.
function renderError(res: Response, page: string, locals: Record<string, unknown>, error: PayaloApiError): void {
  res.render(page, {
    ...locals,
    error: {
      status: error.statusCode,
      title: error.title,
      detail: error.detail ?? error.message,
      errorCode: error.errorCode,
      friendly: describeError(error.specificCode, describeError(error.errorCode, "Check the request body and try again.")),
      isRetryable: error.isRetryable,
      traceId: error.traceId,
    },
  });
}

// Friendly heads-up if the API key is missing — surfaces in the home page table too.
if (!config.apiKey) {
  console.warn("PAYALO_API_KEY is not set — every gateway call will 401. See README §TL;DR.");
}
if (!config.publicBaseUrl) {
  console.warn("PAYALO_PUBLIC_BASE_URL is not set — callbacks will go to https://example.com and be lost.");
}

// ----------------------------------------------------------------------------------
// Home — config check + live connectivity probe.
// ----------------------------------------------------------------------------------
app.get("/", (_req, res) => {
  res.render("home", { config, connectivity: null, error: null });
});

app.post("/connectivity-check", async (_req, res) => {
  try {
    const now = new Date();
    const fromDate = new Date(now.getTime() - 5 * 60 * 1000);
    const page = await client.getRecords({ from: fromDate, to: now, pageSize: 1 });
    res.render("home", { config, connectivity: { count: page.data.length }, error: null });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(res, "home", { config, connectivity: null }, e);
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// Direct Pay-In
// ----------------------------------------------------------------------------------
app.get("/direct-payin", (_req, res) => {
  res.render("direct-payin", { form: defaultDirectPayIn(), response: null, error: null });
});

app.post("/direct-payin", async (req, res) => {
  const form = readDirectPayIn(req);
  try {
    const response = await client.createDirectPayIn(form.method, {
      amount: { value: form.value, currency: form.currency },
      payer: { id: form.userId, msisdn: form.msisdn },
      country: form.country,
      resultUrl: form.resultUrl,
      merchantReference: form.merchantReference,
    });
    // Bump the next reference so a re-submit doesn't trip the dedupe rule.
    res.render("direct-payin", { form: { ...form, merchantReference: newMerchantRef("dep") }, response, error: null });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(res, "direct-payin", { submittedReference: form.merchantReference, form: { ...form, merchantReference: newMerchantRef("dep") }, response: null }, e);
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// Web Pay-In (HPP)
// ----------------------------------------------------------------------------------
app.get("/web-payin", (_req, res) => {
  res.render("web-payin", { form: defaultWebPayIn(), response: null, error: null });
});

app.post("/web-payin", async (req, res) => {
  const form = readWebPayIn(req);
  try {
    const response = await client.createWebPayIn(form.method, {
      amount: { value: form.value, currency: form.currency },
      payer: { id: form.userId, msisdn: form.msisdn },
      country: form.country,
      resultUrl: form.resultUrl,
      successRedirectUrl: form.successRedirectUrl || undefined,
      errorRedirectUrl: form.errorRedirectUrl || undefined,
      preferredLanguage: form.preferredLanguage || undefined,
      merchantReference: form.merchantReference,
    });
    res.render("web-payin", { form: { ...form, merchantReference: newMerchantRef("web") }, response, error: null });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(res, "web-payin", { submittedReference: form.merchantReference, form: { ...form, merchantReference: newMerchantRef("web") }, response: null }, e);
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// Direct Pay-Out
// ----------------------------------------------------------------------------------
app.get("/direct-payout", (_req, res) => {
  res.render("direct-payout", { form: defaultDirectPayOut(), response: null, error: null });
});

app.post("/direct-payout", async (req, res) => {
  const form = readDirectPayOut(req);
  try {
    const response = await client.createDirectPayOut(form.method, {
      amount: { value: form.value, currency: form.currency },
      payee: { id: form.userId, msisdn: form.msisdn },
      country: form.country,
      resultUrl: form.resultUrl,
      merchantReference: form.merchantReference,
    });
    res.render("direct-payout", { form: { ...form, merchantReference: newMerchantRef("payout") }, response, error: null });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(res, "direct-payout", { submittedReference: form.merchantReference, form: { ...form, merchantReference: newMerchantRef("payout") }, response: null }, e);
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// QR Pay-In
// ----------------------------------------------------------------------------------
app.get("/qr-payin", (_req, res) => {
  res.render("qr-payin", { form: defaultQrPayIn(), response: null, error: null });
});

app.post("/qr-payin", async (req, res) => {
  const form = readQrPayIn(req);
  try {
    const response = await client.createQrPayIn(form.method, {
      amount: { value: form.value, currency: form.currency },
      payer: { id: form.userId, msisdn: form.msisdn },
      country: form.country,
      resultUrl: form.resultUrl,
      displayQrUrl: form.displayQrUrl,
      merchantReference: form.merchantReference,
    });
    res.render("qr-payin", { form: { ...form, merchantReference: newMerchantRef("qr") }, response, error: null });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(res, "qr-payin", { submittedReference: form.merchantReference, form: { ...form, merchantReference: newMerchantRef("qr") }, response: null }, e);
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// Direct Tax Payout
// ----------------------------------------------------------------------------------
app.get("/direct-tax-payout", (_req, res) => {
  res.render("direct-tax-payout", { form: defaultDirectTaxPayout(), response: null, error: null });
});

app.post("/direct-tax-payout", async (req, res) => {
  const form = readDirectTaxPayout(req);
  try {
    const response = await client.createDirectTaxPayout(form.method, {
      amount: { value: form.value, currency: form.currency },
      country: form.country,
      resultUrl: form.resultUrl,
      merchantReference: form.merchantReference,
      reconciliationReference: form.reconciliationReference,
    });
    res.render("direct-tax-payout", {
      form: {
        ...form,
        merchantReference: newMerchantRef("tax"),
        reconciliationReference: `TAX-${new Date().toISOString().slice(0, 7).replace("-", "")}-${newMerchantRef("").slice(1, 7)}`,
      },
      response,
      error: null,
    });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(
        res,
        "direct-tax-payout",
        {
          submittedReference: form.merchantReference,
          form: {
            ...form,
            merchantReference: newMerchantRef("tax"),
            reconciliationReference: `TAX-${new Date().toISOString().slice(0, 7).replace("-", "")}-${newMerchantRef("").slice(1, 7)}`,
          },
          response: null,
        },
        e,
      );
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// Status
// ----------------------------------------------------------------------------------
app.get("/status", async (req, res) => {
  const gw = (req.query.gw as string | undefined)?.trim();
  const mref = (req.query.mref as string | undefined)?.trim();
  if (!gw && !mref) {
    return res.render("status", { transaction: null, error: null, gw: "", mref: "" });
  }
  try {
    const transaction = gw
      ? await client.getByGatewayReference(gw)
      : await client.getByMerchantReference(mref!);
    res.render("status", { transaction, friendly: describeError, error: null, gw: gw ?? "", mref: mref ?? "" });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(res, "status", { transaction: null, gw: gw ?? "", mref: mref ?? "" }, e);
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// Records
// ----------------------------------------------------------------------------------
app.get("/records", async (req, res) => {
  const filters = readRecordFilters(req);
  if (!filters.searched) {
    return res.render("records", { filters, page: null, error: null });
  }
  try {
    const page = await client.getRecords({
      from: new Date(`${filters.from}Z`),
      to: new Date(`${filters.to}Z`),
      type: filters.type || undefined,
      method: filters.method || undefined,
      status: filters.status || undefined,
      pageSize: filters.pageSize,
      pageCursor: filters.cursor || undefined,
    });
    res.render("records", { filters, page, error: null });
  } catch (e) {
    if (e instanceof PayaloApiError) {
      return renderError(res, "records", { filters, page: null }, e);
    }
    throw e;
  }
});

// ----------------------------------------------------------------------------------
// Webhooks
// ----------------------------------------------------------------------------------
app.get("/webhooks", (_req, res) => {
  res.render("webhooks", { config, entries: snapshot() });
});

app.post("/webhooks/clear", (_req, res) => {
  clearWebhooks();
  res.redirect("/webhooks");
});

// ----------------------------------------------------------------------------------
// Form helpers — small read/default pairs per page so the route handlers stay short.
// ----------------------------------------------------------------------------------

function defaultDirectPayIn() {
  return {
    method: "mpesa-ke",
    value: 500,
    currency: "KES",
    userId: "user-42",
    msisdn: "+254712345678",
    country: "KE",
    resultUrl: resultUrl(),
    merchantReference: newMerchantRef("dep"),
  };
}
function readDirectPayIn(req: Request) {
  return {
    method: String(req.body.method ?? "").trim(),
    value: Number(req.body.value),
    currency: String(req.body.currency ?? "").trim(),
    userId: String(req.body.userId ?? "").trim(),
    msisdn: String(req.body.msisdn ?? "").trim(),
    country: String(req.body.country ?? "").trim(),
    resultUrl: String(req.body.resultUrl ?? "").trim(),
    merchantReference: String(req.body.merchantReference ?? "").trim(),
  };
}

function defaultWebPayIn() {
  const publicBase = config.publicBaseUrl ? config.publicBaseUrl.replace(/\/$/, "") : "https://example.com";
  return {
    method: "card-eu",
    value: 25,
    currency: "EUR",
    userId: "user-42",
    msisdn: "+491234567890",
    country: "DE",
    resultUrl: resultUrl(),
    successRedirectUrl: `${publicBase}/status`,
    errorRedirectUrl: `${publicBase}/status`,
    preferredLanguage: "fr-FR",
    merchantReference: newMerchantRef("web"),
  };
}
function readWebPayIn(req: Request) {
  return {
    ...readDirectPayIn(req),
    successRedirectUrl: String(req.body.successRedirectUrl ?? "").trim(),
    errorRedirectUrl: String(req.body.errorRedirectUrl ?? "").trim(),
    preferredLanguage: String(req.body.preferredLanguage ?? "").trim(),
  };
}

function defaultDirectPayOut() {
  return { ...defaultDirectPayIn(), value: 1000, merchantReference: newMerchantRef("payout") };
}
function readDirectPayOut(req: Request) {
  return readDirectPayIn(req);
}

function defaultQrPayIn() {
  const publicBase = config.publicBaseUrl ? config.publicBaseUrl.replace(/\/$/, "") : "https://example.com";
  return {
    ...defaultDirectPayIn(),
    displayQrUrl: `${publicBase}${config.webhookPath}/qr`,
    merchantReference: newMerchantRef("qr"),
  };
}
function readQrPayIn(req: Request) {
  return {
    ...readDirectPayIn(req),
    displayQrUrl: String(req.body.displayQrUrl ?? "").trim(),
  };
}

function defaultDirectTaxPayout() {
  return {
    method: "tax-ke",
    value: 15000,
    currency: "KES",
    country: "KE",
    resultUrl: resultUrl(),
    merchantReference: newMerchantRef("tax"),
    reconciliationReference: `TAX-${new Date().toISOString().slice(0, 7).replace("-", "")}-${newMerchantRef("").slice(1, 7)}`,
  };
}
function readDirectTaxPayout(req: Request) {
  return {
    method: String(req.body.method ?? "").trim(),
    value: Number(req.body.value),
    currency: String(req.body.currency ?? "").trim(),
    country: String(req.body.country ?? "").trim(),
    resultUrl: String(req.body.resultUrl ?? "").trim(),
    merchantReference: String(req.body.merchantReference ?? "").trim(),
    reconciliationReference: String(req.body.reconciliationReference ?? "").trim(),
  };
}

function readRecordFilters(req: Request) {
  const fromIso = (req.query.from as string | undefined) ?? new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString().slice(0, 16);
  const toIso = (req.query.to as string | undefined) ?? new Date().toISOString().slice(0, 16);
  return {
    from: fromIso,
    to: toIso,
    type: ((req.query.type as string | undefined) ?? "").trim(),
    method: ((req.query.method as string | undefined) ?? "").trim(),
    status: ((req.query.status as string | undefined) ?? "").trim(),
    pageSize: Number(req.query.pageSize ?? 50),
    cursor: ((req.query.page as string | undefined) ?? "").trim(),
    // First load (no `searched` flag) shows just the filter form. Click Search to query.
    searched: req.query.searched === "1" || !!req.query.page,
  };
}

// HTML-escape a value before interpolating it into a raw HTML string (e.g. the
// `extraFields` markup the form partial emits unescaped via <%- %>). Guards against
// reflected XSS from user-supplied form values.
function escapeHtml(value: unknown): string {
  const map: Record<string, string> = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" };
  return String(value ?? "").replace(/[&<>"']/g, (c) => map[c] ?? c);
}

app.listen(config.port, () => {
  console.log(`Payalo Node sample listening on http://localhost:${config.port}`);
});
