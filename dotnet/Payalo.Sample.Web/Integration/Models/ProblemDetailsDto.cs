namespace Payalo.Sample.Web.Integration.Models;

// RFC 7807 Problem Details body. The gateway returns this as `application/problem+json`
// for every error response (validation, auth, business-logic, server errors).
//
// `ErrorCode` is the machine-readable identifier you should branch on in code; `Detail`
// is the human-readable message you can show to the user (or log).
public sealed class ProblemDetailsDto
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? ErrorCode { get; set; }
}
