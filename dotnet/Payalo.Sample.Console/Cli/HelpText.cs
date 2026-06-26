using SystemConsole = System.Console;

namespace Payalo.Sample.Console.Cli;

// Static help text. Kept as plain strings (rather than auto-generated from a command
// metadata graph) so a reader can see in one place exactly what each subcommand accepts.
public static class HelpText
{
    public static void PrintRoot()
    {
        SystemConsole.WriteLine("""
            payalo — Payalo Merchant Gateway v2 console sample

            Usage:
              payalo <command> [options]

            Commands:
              payin-direct      Submit a Direct Pay-In (server-to-server deposit)
              payin-web         Submit a Web Pay-In (Hosted Payment Page)
              payin-qr          Submit a QR Pay-In
              payout-direct     Submit a Direct Pay-Out (disbursement to an end user)
              payout-tax        Submit a Direct Tax Payout (no payee, recipient implicit)
              status            Look up a transaction by gateway or merchant reference
              records           List transactions in a date range (paginated)
              help              Show this help

            Each command supports `--help` for command-specific options. Required values
            not supplied via flags will be prompted interactively (when stdin is a TTY).

            Configuration (Payalo:* keys):
              BaseUrl           Gateway base URL (defaults to dev environment)
              ApiKey            Brand API key (required)
              DefaultResultUrl  resultUrl populated on submit when --result-url is omitted

            Set via environment variables (Payalo__ApiKey=...), `dotnet user-secrets`,
            or a gitignored `appsettings.Local.json` next to `appsettings.json`.

            Full V2 API reference: https://docs.payalo.com
            """);
    }

    public static void PrintForCommand(string command)
    {
        var text = command switch
        {
            "payin-direct"   => DirectPayIn,
            "payin-web"      => WebPayIn,
            "payin-qr"       => QrPayIn,
            "payout-direct"  => DirectPayOut,
            "payout-tax"     => TaxPayout,
            "status"         => Status,
            "records"        => Records,
            _                => null,
        };

        if (text is null)
        {
            SystemConsole.WriteLine($"Unknown command: {command}");
            PrintRoot();
            return;
        }

        SystemConsole.WriteLine(text);
    }

    private const string PartyOptions =
        """
            --party-id              Internal customer id (required)
            --party-msisdn          Phone in E.164, e.g. +254712345678 (required)
            --party-first-name      Optional
            --party-last-name       Optional
            --party-email           Optional
        """;

    private const string SubmitCommonOptions =
        """
            --method                Payment method code, e.g. mpesa-ke (required)
            --amount                Decimal amount, e.g. 100.00 (required)
            --currency              ISO 4217 code, e.g. KES (required)
            --country               ISO 3166 code, e.g. KE (required)
            --merchant-ref          Idempotency key. Auto-generated (UUID) if omitted.
            --reconciliation-ref    Optional bookkeeping label
            --result-url            Webhook callback URL. Defaults to Payalo:DefaultResultUrl.
        """;

    private const string DirectPayIn =
        $$"""
        payalo payin-direct — POST /direct/payin/{method}

        Server-to-server deposit (e.g. M-Pesa STK push).

        Options:
        {{SubmitCommonOptions}}
        {{PartyOptions}}
        """;

    private const string WebPayIn =
        $$"""
        payalo payin-web — POST /web/payin/{method}

        Hosted Payment Page. The response includes `pageUrl` which you redirect the end
        user to; the gateway will redirect them back to --success-url / --error-url and
        send the authoritative status to --result-url.

        Options:
        {{SubmitCommonOptions}}
        {{PartyOptions}}
            --success-url           Browser redirect on success (optional)
            --error-url             Browser redirect on error (optional)
            --preferred-language    Hosted-page language, BCP-47 tag e.g. fr-FR (optional)
        """;

    private const string QrPayIn =
        $$"""
        payalo payin-qr — POST /qr/payin/{method}

        QR-code deposit. The gateway POSTs the raw QR data to --display-qr-url; your
        application is responsible for fetching and rendering it.

        Options:
        {{SubmitCommonOptions}}
        {{PartyOptions}}
            --display-qr-url        Where the gateway delivers QR data (required)
        """;

    private const string DirectPayOut =
        $$"""
        payalo payout-direct — POST /direct/payout/{method}

        Server-to-server disbursement to an end user.

        Options:
        {{SubmitCommonOptions}}
        {{PartyOptions}}
        """;

    private const string TaxPayout =
        $$"""
        payalo payout-tax — POST /direct/taxpayout/{method}

        Payout to a tax authority. No payer/payee — the recipient is implicit in the
        payment method. `--reconciliation-ref` is REQUIRED for this flow.

        Options:
        {{SubmitCommonOptions}}
        """;

    private const string Status =
        """
        payalo status — GET /status/{ref}

        Look up a transaction. Provide exactly one reference:

        Options:
            --gref                  Gateway reference returned on submit
            --mref                  Merchant reference you supplied on submit
        """;

    private const string Records =
        """
        payalo records — GET /records

        Paginated bulk reconciliation. The cursor encodes the original filter set, so
        on follow-up calls you should pass *only* --page-cursor.

        Options:
            --from                  ISO 8601 start (required for first page)
            --to                    ISO 8601 end (required for first page)
            --type                  payin | payout | tax (optional filter)
            --method                Method code (optional filter)
            --status                pending | success | failed (optional filter)
            --page-size             Page size (optional, gateway-defined default)
            --page-cursor           Cursor from a previous Pages.Next/Previous
        """;
}
