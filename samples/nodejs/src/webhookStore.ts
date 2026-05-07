// In-memory log of received gateway callbacks. DEMO ONLY — wipes on restart.
//
// A production integration writes each entry to a database keyed on the gateway's
// `gatewayReference` so callbacks are durable, dedupable across replicas, and
// reconcilable via /records.

import type { MmoTransactionDto } from "./payaloClient.js";

export interface WebhookEntry {
  receivedAt: Date;
  sourceIp: string;
  payload: MmoTransactionDto;
  rawBody: string;
}

const MAX_ENTRIES = 200;

const byReference = new Map<string, WebhookEntry>();
const insertionOrder: string[] = [];

// Atomic insert-if-absent. Returns true when newly stored, false on duplicate.
// Keyed on the gateway-assigned `gatewayReference` per the v2 callbacks docs.
export function tryRecord(payload: MmoTransactionDto, rawBody: string, sourceIp: string): boolean {
  if (!payload.gatewayReference) return false;
  if (byReference.has(payload.gatewayReference)) return false;

  byReference.set(payload.gatewayReference, {
    receivedAt: new Date(),
    sourceIp,
    payload,
    rawBody,
  });
  insertionOrder.unshift(payload.gatewayReference);

  while (insertionOrder.length > MAX_ENTRIES) {
    const oldest = insertionOrder.pop();
    if (oldest) byReference.delete(oldest);
  }
  return true;
}

// Newest first.
export function snapshot(): WebhookEntry[] {
  return insertionOrder
    .map((reference) => byReference.get(reference))
    .filter((e): e is WebhookEntry => e !== undefined);
}

export function clear(): void {
  byReference.clear();
  insertionOrder.length = 0;
}
