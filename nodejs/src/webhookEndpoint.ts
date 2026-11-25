import express, { type Request, type Response } from "express";
import { timingSafeEqual, createHash } from "node:crypto";
import { config } from "./config.js";
import { tryRecord } from "./webhookStore.js";
import type { MmoTransactionDto } from "./payaloClient.js";

// Inbound webhook receiver — equivalent of the .NET WebhookEndpoint minimal API.
//
// The gateway:
//   - POSTs application/json to your `resultUrl`,
//   - includes the `X-API-KEY` header set to *your* brand API key,
//   - delivers the same V2 MmoTransactionDto shape returned by GET /status/{ref},
//   - expects any 2xx response within 15 seconds; the response body is optional and informational only,
//   - makes ONE attempt per terminal state change (no retry): reconcile with Status or Records if a delivery fails,
//   - may redeliver a previously-acknowledged callback in rare cases (be idempotent).
//
// The same router also receives the QR data that Payalo POSTs to `displayQrUrl` on QR Pay-In
// (`<webhookPath>/qr`). That request carries the same X-API-KEY header.

export function webhookRouter(): express.Router {
  const router = express.Router();

  // Use the raw-body parser so we can preserve the bytes for the demo UI alongside the
  // parsed view. We then `JSON.parse` ourselves.
  router.post(config.webhookPath, express.text({ type: "application/json", limit: "1mb" }), handle);
  router.post(`${config.webhookPath}/qr`, express.json({ limit: "1mb" }), handleQr);
  return router;
}

// QR data request: Payalo POSTs { gatewayReference, merchantReference, qrCode, deepLink, message,
// expiresAt } to `displayQrUrl`. Answer 2xx as soon as the data arrives, then show the QR code.
function handleQr(req: Request, res: Response): void {
  const presented = (req.header("x-api-key") ?? "").trim();
  if (!presented || !fixedTimeEquals(presented, config.apiKey)) {
    console.warn(`Rejected QR data request from ${req.ip} — X-API-KEY missing or mismatched`);
    res.status(401).end();
    return;
  }
  const body = (req.body ?? {}) as Record<string, unknown>;
  console.log(
    `QR data received: gatewayReference=${String(body.gatewayReference)} qrCode=${body.qrCode ? "yes" : "no"} deepLink=${body.deepLink ? "yes" : "no"} expiresAt=${String(body.expiresAt)}`,
  );
  res.status(200).json({ status: "ok" });
}

function handle(req: Request, res: Response): void {
  // 1) Authenticate. The only signal is X-API-KEY echoing our own brand key.
  //    Constant-time comparison prevents timing-based key probing.
  const presented = (req.header("x-api-key") ?? "").trim();
  if (!presented || !fixedTimeEquals(presented, config.apiKey)) {
    console.warn(`Rejected webhook from ${req.ip} — X-API-KEY missing or mismatched`);
    res.status(401).end();
    return;
  }

  // 2) Parse the buffered body.
  const raw = typeof req.body === "string" ? req.body : "";
  let payload: MmoTransactionDto;
  try {
    payload = JSON.parse(raw) as MmoTransactionDto;
  } catch {
    console.warn("Webhook body was not valid JSON");
    res.status(400).json({ status: "error" });
    return;
  }
  const pushId = (payload as unknown as { id?: string } | null)?.id;
  if (!payload?.gatewayReference && pushId) {
    // A `push` callback carries the V1 body: read GET /status/{id} for the details.
    console.log(`Push callback received: id=${pushId} — read GET /status/${pushId}`);
    res.status(200).json({ status: "ok" });
    return;
  }
  if (!payload?.gatewayReference) {
    console.warn("Webhook body parsed but is missing required `gatewayReference` field");
    res.status(400).json({ status: "error" });
    return;
  }

  // 3) Persist (idempotently). Returns false if we'd already seen this gatewayReference with this status.
  const isNew = tryRecord(payload, raw, req.ip ?? "unknown");
  console.log(
    `Webhook ${isNew ? "accepted" : "deduplicated (same gatewayReference and status)"}: gatewayReference=${payload.gatewayReference} status=${payload.status} duplicate=${!isNew}`,
  );

  // 4) Always reply "ok" within 15s. Heavy work belongs in a background queue, not
  //    inline here. A non-2xx answer is not retried by the gateway: reconcile with Status or Records.
  res.status(200).json({ status: "ok" });
}

// Constant-time byte comparison. Compares SHA-256 hashes so the input length doesn't
// leak via the comparison time.
function fixedTimeEquals(a: string, b: string): boolean {
  const aHash = createHash("sha256").update(a).digest();
  const bHash = createHash("sha256").update(b).digest();
  return timingSafeEqual(aHash, bHash);
}
