using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Console.Integration.Models;

// Represents an amount + currency, e.g. { "value": 500.00, "currency": "KES" }.
//
// Decimal precision must match the currency: 2 dp for EUR/KES/USD, 0 for JPY, etc.
// Validation is authoritative server-side.
public sealed class MoneyDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Value { get; set; }

    [Required, StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO 4217 code.")]
    public string Currency { get; set; } = "";
}
