import { randomUUID } from "node:crypto";

// Generate a fresh merchantReference. The gateway keeps a registered reference used
// forever; never reuse one for a new payment.
export function newMerchantRef(prefix: string): string {
  return `${prefix}-${randomUUID().replace(/-/g, "")}`;
}
