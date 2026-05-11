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
//   - retries up to 5 times on non-2xx with linear backoff,
//   - may redeliver a previously-acknowledged callback in rare cases (be idempotent).

export function webhookRouter(): express.Router {
  const router = express.Router();

  // Use the raw-body parser so we can preserve the bytes for the demo UI alongside the
  // parsed view. We then `JSON.parse` ourselves.
  router.post(config.webhookPath, express.text({ type: "application/json", limit: "1mb" }), handle);
  return router;
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
  if (!payload?.gatewayReference) {
    console.warn("Webhook body parsed but is missing required `gatewayReference` field");
    res.status(400).json({ status: "error" });
    return;
  }

  // 3) Persist (idempotently). Returns false if we'd already seen this gatewayReference.
  const isNew = tryRecord(payload, raw, req.ip ?? "unknown");
  console.log(
    `Webhook ${isNew ? "accepted" : "deduplicated"}: gatewayReference=${payload.gatewayReference} status=${payload.status} duplicate=${!isNew}`,
  );

  // 4) Always reply "ok" within 15s. Heavy work belongs in a background queue, not
  //    inline here. Acknowledging duplicates as "ok" stops the gateway from retrying.
  res.status(200).json({ status: "ok" });
}

// Constant-time byte comparison. Compares SHA-256 hashes so the input length doesn't
// leak via the comparison time.
function fixedTimeEquals(a: string, b: string): boolean {
  const aHash = createHash("sha256").update(a).digest();
  const bHash = createHash("sha256").update(b).digest();
  return timingSafeEqual(aHash, bHash);
}
