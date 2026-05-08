using System.ComponentModel.DataAnnotations;

namespace Payalo.Sample.Console.Integration.Models;

// Represents an end user (payer for pay-ins, payee for pay-outs) identified by phone number.
public sealed class MsisdnPartyDto
{
    [Required, StringLength(255)]
    public string Id { get; set; } = "";

    [Required, StringLength(20)]
    [RegularExpression(@"^\+[1-9]\d{6,19}$",
        ErrorMessage = "Phone number must be in international format, e.g. +254712345678.")]
    public string Msisdn { get; set; } = "";

    [StringLength(255)]
    public string? FirstName { get; set; }

    [StringLength(255)]
    public string? LastName { get; set; }

    [StringLength(320), EmailAddress]
    public string? Email { get; set; }
}
