namespace Payalo.Sample.Console.Integration;

// Maps the documented async transaction error codes (returned in callbacks and on the
// status endpoint) to user-friendly explanations.
//
// Anything not in this dictionary falls back to the gateway's own `errorMessage`.
// New codes may be added in future — treat unknown codes as not-retryable and surface
// a generic error rather than failing open.
public static class PayaloErrorMessages
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // User-side failures.
        ["user_insufficient_funds"]         = "The end user does not have enough funds. Ask them to top up and try again.",
        ["user_limit_exceeded"]             = "The end user has hit a daily, weekly, or monthly account limit.",
        ["user_authorization_failed"]       = "The end user entered an invalid PIN or OTP.",
        ["user_timeout"]                    = "The end user did not approve the prompt in time (STK push expired).",
        ["user_invalid_msisdn_for_method"]  = "The MSISDN is not registered for this payment method.",
        ["user_account_block"]              = "The end user's account is blocked by the provider.",
        ["user_too_many_requests"]          = "The end user has triggered too many attempts in a short window.",
        ["user_cancelled"]                  = "The end user cancelled the prompt.",
        ["user_unknown_error"]              = "Provider reported an unspecified user-side error.",

        // Provider-side failures.
        ["provider_unavailable"]            = "The provider's system was down or unreachable.",
        ["provider_timeout"]                = "The provider didn't respond within the gateway's wait window.",
        ["provider_noresponse_from_method"] = "The specific payment method didn't return a status.",
        ["provider_connection_error"]       = "Network-level error contacting the provider.",
        ["provider_too_many_requests"]      = "The provider rate-limited us. Back off and retry.",
        ["provider_bad_request"]            = "The provider rejected the request shape — usually a configuration mismatch.",
        ["provider_wrong_configuration"]    = "The provider returned a configuration error.",
        ["provider_transactionid_duplicate"]= "The provider's own dedup window rejected the reference. Retry with a fresh merchantReference.",
        ["provider_cancelled"]              = "The provider cancelled the transaction (anti-fraud, manual review, etc.).",
        ["provider_limit_exceeded"]         = "The provider's per-transaction or daily limit applied.",
        ["provider_unknown_error"]          = "Provider returned an error we couldn't classify.",

        // Risk and authentication.
        ["risk_blocked"]                    = "Risk engine blocked the transaction.",
        ["security_authentication_failed"]  = "The credentials presented to the provider were rejected.",
        ["security_authorization_failed"]   = "The caller does not have permission for this operation.",

        // Configuration failures.
        ["config_unsupported_currency"]         = "The currency is not configured for this method/brand.",
        ["config_unsupported_country"]          = "The country is not configured.",
        ["config_unsupported_payment_method"]   = "The method is not enabled for this brand.",
        ["config_method_route_not_found"]       = "No route exists for the method/country/currency triple.",
        ["config_method_transaction_min_limit"] = "Amount is below the configured minimum.",
        ["config_method_transaction_max_limit"] = "Amount is above the configured maximum.",
        ["config_credentials_missing"]          = "The brand's provider credentials are missing — contact support.",

        // Merchant-side failures.
        ["merchant_disabled"]                  = "The brand has been disabled. Contact your account manager.",
        ["merchant_insufficient_funds"]        = "The merchant float used for pay-outs has insufficient balance.",
        ["merchant_limit_exceeded"]            = "The merchant has hit a configured limit.",
        ["merchant_invalid_credentials"]       = "The merchant credentials at the provider are invalid.",
        ["merchant_cancelled_by_risk"]         = "A risk rule cancelled this transaction.",
        ["merchant_reference_duplicate"]       = "Another payment of your brand already uses this `merchantReference`. Read Payment Status by `merchantReference` before you send a new payment.",
        ["merchant_unknown_error"]             = "Merchant-side error we couldn't classify.",

        // Gateway and lifecycle.
        ["transaction_expired"]                = "The gateway-side timeout fired. The transaction was auto-marked failed.",
        ["transaction_not_found"]              = "The provider did not find the transaction.",
        ["gateway_too_many_requests"]          = "The gateway rate-limited the request. Back off and retry.",
        ["hpp_open_timeout"]                   = "The hosted payment page session was created but never opened in time.",
        ["transaction_placement_expired"]      = "Payalo did not send the payment to a provider in time, so no provider was contacted. If Payment Status shows this code with `completedAt` null, the answer may not be final yet: wait a minute and read the status again before you start a new payment for the same order.",
        ["provider_overloaded"]                = "Too many of your payments for the same payment method and provider were already waiting to be sent. Payalo did not send this one to the provider.",
        ["transaction_placement_failed_due_to_circuit_breaker_settings"] = "Too many recent requests from Payalo to the provider failed, so Payalo stopped sending payments to it for a short time. Payalo did not send this payment to the provider.",
        ["merchant_not_available"]             = "Payalo tried several times to deliver the QR data to your `displayQrUrl`. Your endpoint never answered with a `2xx` status (a success status, such as `200`).",
        ["merchant_required_parameter_missing"] = "Payalo refused your `displayQrUrl` when it tried to deliver the QR data, so the payment failed. For example, the create request accepts an `http://` URL, but Payalo delivers QR data only to `https`.",

        // Synchronous (request-time) error codes.
        ["validation_failed"]      = "One or more fields failed validation. Check the detail message.",
        ["bad_request"]            = "The request body is malformed.",
        ["unauthorized"]           = "API key is missing or invalid.",
        ["not_found"]              = "Resource not found.",
        ["business_logic_error"]   = "A business rule was violated.",
        ["internal_server_error"]  = "Unexpected server-side error. Payalo may still have registered the payment: read Payment Status by `merchantReference` before you send the request again.",
    };

    public static string Describe(string? code, string? fallback = null) =>
        code is not null && Map.TryGetValue(code, out var msg)
            ? msg
            : fallback ?? "Unknown error.";
}
