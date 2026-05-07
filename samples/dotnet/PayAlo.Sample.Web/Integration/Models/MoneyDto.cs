using System.ComponentModel.DataAnnotations;

namespace PayAlo.Sample.Web.Integration.Models;

// Represents an amount + currency, e.g. { "value": 500.00, "currency": "KES" }.
//
// Decimal precision must match the currency: 2 dp for EUR/KES/USD, 0 for JPY, etc.
// Validation happens server-side — we only enforce "value > 0" here so the form catches
// the obvious cases before round-tripping.
public sealed class MoneyDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Value { get; set; }

    [Required, StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO 4217 code.")]
    public string Currency { get; set; } = "";
}
