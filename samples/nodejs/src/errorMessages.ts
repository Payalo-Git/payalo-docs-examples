// Maps the documented async transaction error codes (delivered in callbacks and on the
// status endpoint) to user-friendly explanations.
//
// Source of truth: docs/merchant-gateway/v2/async-errors.md.
// Anything not listed falls back to the gateway's own `errorMessage`. New codes may be
// added in future — treat unknown codes as not-retryable and surface a generic error.

const messages: Record<string, string> = {
  // User-side failures — caused by something on the end user's side.
  user_insufficient_funds: "The end user does not have enough funds. Ask them to top up and try again.",
  user_limit_exceeded: "The end user has hit a daily, weekly, or monthly account limit.",
  user_authorization_failed: "The end user entered an invalid PIN or OTP.",
  user_timeout: "The end user did not approve the prompt in time (STK push expired).",
  user_invalid_msisdn_for_method: "The MSISDN is not registered for this payment method.",
  user_account_block: "The end user's account is blocked by the provider.",
  user_too_many_requests: "The end user has triggered too many attempts in a short window.",
  user_cancelled: "The end user cancelled the prompt.",
  user_unknown_error: "Provider reported an unspecified user-side error.",

  // Provider-side failures — upstream provider rejected or didn't respond.
  provider_unavailable: "The provider's system was down or unreachable.",
  provider_timeout: "The provider didn't respond within the gateway's wait window.",
  provider_noresponse_from_method: "The specific payment method didn't return a status.",
  provider_connection_error: "Network-level error contacting the provider.",
  provider_too_many_requests: "The provider rate-limited us. Back off and retry.",
  provider_bad_request: "The provider rejected the request shape — usually a configuration mismatch.",
  provider_wrong_configuration: "The provider returned a configuration error.",
  provider_transactionid_duplicate: "The provider's own dedup window rejected the reference. Retry with a fresh merchantReference.",
  provider_cancelled: "The provider cancelled the transaction (anti-fraud, manual review, etc.).",
  provider_limit_exceeded: "The provider's per-transaction or daily limit applied.",
  provider_unknown_error: "Provider returned an error we couldn't classify.",

  // Risk and authentication.
  risk_blocked: "Risk engine blocked the transaction.",
  security_authentication_failed: "The credentials presented to the provider were rejected.",
  security_authorization_failed: "The caller does not have permission for this operation.",

  // Configuration failures — usually a setup mismatch on PayAlo's side. Report to support.
  config_unsupported_currency: "The currency is not configured for this method/brand.",
  config_unsupported_country: "The country is not configured.",
  config_unsupported_payment_method: "The method is not enabled for this brand.",
  config_method_route_not_found: "No route exists for the method/country/currency triple.",
  config_method_transaction_min_limit: "Amount is below the configured minimum.",
  config_method_transaction_max_limit: "Amount is above the configured maximum.",
  config_credentials_missing: "The brand's provider credentials are missing — contact support.",

  // Merchant-side failures.
  merchant_disabled: "The brand has been disabled. Contact your account manager.",
  merchant_insufficient_funds: "The merchant float used for pay-outs has insufficient balance.",
  merchant_limit_exceeded: "The merchant has hit a configured limit.",
  merchant_invalid_credentials: "The merchant credentials at the provider are invalid.",
  merchant_cancelled_by_risk: "A risk rule cancelled this transaction.",
  merchant_transactionid_duplicate: "Duplicate `merchantReference`. Retry with a new reference.",
  merchant_unknown_error: "Merchant-side error we couldn't classify.",

  // Gateway and lifecycle.
  transaction_expired: "The gateway-side timeout fired. The transaction was auto-marked failed.",
  transaction_not_found: "Status lookup failed because the transaction does not exist for this brand.",
  gateway_too_many_requests: "The gateway rate-limited the request. Back off and retry.",
  hpp_open_timeout: "The hosted payment page session was created but never opened in time.",

  // Synchronous (request-time) error codes — see docs §5 Error Handling.
  validation_failed: "One or more fields failed validation. Check the detail message.",
  bad_request: "The request body is malformed.",
  unauthorized: "API key is missing or invalid.",
  not_found: "Resource not found.",
  business_logic_error: "A business rule was violated.",
  internal_server_error: "Unexpected server-side error. Safe to retry after a short delay.",
};

export function describeError(code: string | null | undefined, fallback?: string | null): string {
  if (code && messages[code]) return messages[code];
  return fallback ?? "Unknown error.";
}
