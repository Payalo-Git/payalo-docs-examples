using Microsoft.Extensions.Options;
using Payalo.Sample.Console.Cli;
using Payalo.Sample.Console.Integration;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Commands;

// Shared input gathering for the five submit-style commands. Each command reads its
// own variant on top of these baseline values (party for payin/payout, redirect URLs
// for web, displayQrUrl for QR, etc.) — keeping the common fields here means the
// per-command files stay focused on the bits that actually differ.
internal static class SubmitInputs
{
    public sealed record CommonValues(
        string Method,
        MoneyDto Amount,
        string Country,
        string MerchantReference,
        string? ReconciliationReference,
        string ResultUrl);

    public static CommonValues GatherCommon(ParsedArgs args, IOptions<PayaloOptions> options)
    {
        var defaultResultUrl = options.Value.DefaultResultUrl;

        var method = args.Get("--method")
            ?? Prompt.AskString("Payment method (e.g. mpesa-ke)");

        var amountValue = args.Get("--amount") is { } amountStr && decimal.TryParse(amountStr,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedAmount)
            ? parsedAmount
            : Prompt.AskDecimal("Amount");

        var currency = args.Get("--currency")
            ?? Prompt.AskString("Currency (ISO 4217, e.g. KES)");

        var country = args.Get("--country")
            ?? Prompt.AskString("Country (ISO 3166, e.g. KE)");

        // The reference must be unique per attempt. Auto-generating a fresh GUID when
        // the caller doesn't pass one keeps the sample idempotency-safe even when
        // a previous run failed and you immediately re-submit.
        var merchantRef = args.Get("--merchant-ref")
            ?? $"sample-{Guid.NewGuid():N}";

        var reconRef = args.Get("--reconciliation-ref");

        var resultUrl = args.Get("--result-url")
            ?? (string.IsNullOrWhiteSpace(defaultResultUrl)
                ? Prompt.AskString("Result URL (webhook callback)")
                : defaultResultUrl);

        return new CommonValues(
            Method: method,
            Amount: new MoneyDto { Value = amountValue, Currency = currency },
            Country: country,
            MerchantReference: merchantRef,
            ReconciliationReference: reconRef,
            ResultUrl: resultUrl);
    }

    public static MsisdnPartyDto GatherParty(ParsedArgs args, string label)
    {
        Output.Info($"-- {label} details --");
        return new MsisdnPartyDto
        {
            Id        = args.Get("--party-id")        ?? Prompt.AskString("  Party id"),
            Msisdn    = args.Get("--party-msisdn")    ?? Prompt.AskString("  Phone (E.164, e.g. +254712345678)"),
            FirstName = args.Get("--party-first-name") ?? AskOptional("  First name"),
            LastName  = args.Get("--party-last-name")  ?? AskOptional("  Last name"),
            Email     = args.Get("--party-email")      ?? AskOptional("  Email"),
        };
    }

    private static string? AskOptional(string label)
    {
        var input = Prompt.AskString(label, defaultValue: "", required: false);
        return string.IsNullOrWhiteSpace(input) ? null : input;
    }
}
