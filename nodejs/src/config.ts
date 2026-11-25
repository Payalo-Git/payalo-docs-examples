import "dotenv/config";

// Strongly-typed configuration loaded from PAYALO_* environment variables.
// Mirrors the .NET sample's PayaloOptions.
export interface PayaloConfig {
  baseUrl: string;
  apiKey: string;
  webhookPath: string;
  publicBaseUrl: string;
  port: number;
}

export const config: PayaloConfig = {
  baseUrl: process.env.PAYALO_BASE_URL ?? "https://api.payalo.com/gateway/mmo/v2/",
  apiKey: process.env.PAYALO_API_KEY ?? "",
  webhookPath: process.env.PAYALO_WEBHOOK_PATH ?? "/webhooks/payalo",
  publicBaseUrl: process.env.PAYALO_PUBLIC_BASE_URL ?? "",
  port: Number(process.env.PORT ?? 3000),
};

// Helper used in form pages to pre-fill the resultUrl.
export function resultUrl(): string {
  const base = config.publicBaseUrl ? config.publicBaseUrl.replace(/\/$/, "") : "https://example.com";
  return `${base}${config.webhookPath}`;
}
