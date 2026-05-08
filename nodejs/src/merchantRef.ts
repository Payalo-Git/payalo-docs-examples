import { randomUUID } from "node:crypto";

// Generate a fresh merchantReference. The gateway's deduplication store retains used
// IDs for ~15 minutes; never reuse one, even after a failure.
export function newMerchantRef(prefix: string): string {
  return `${prefix}-${randomUUID().replace(/-/g, "")}`;
}
