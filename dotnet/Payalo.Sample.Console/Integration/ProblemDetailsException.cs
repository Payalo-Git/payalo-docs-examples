using System.Net;
using Payalo.Sample.Console.Integration.Models;

namespace Payalo.Sample.Console.Integration;

// Thrown by PayaloClient when the gateway returns a non-2xx response.
//
// The gateway uses RFC 7807 Problem Details (application/problem+json) for all errors,
// so we surface the structured payload to callers instead of a bare HttpRequestException.
// We also capture the W3C `traceparent` header so support tickets can quote a single
// correlation token that the gateway team can grep for.
public sealed class PayaloApiException : Exception
{
    public PayaloApiException(
        HttpStatusCode statusCode,
        ProblemDetailsDto? problem,
        string? rawBody,
        string? traceId = null)
        : base(BuildMessage(statusCode, problem, rawBody))
    {
        StatusCode = statusCode;
        Problem = problem;
        RawBody = rawBody;
        TraceId = traceId;
    }

    public HttpStatusCode StatusCode { get; }
    public ProblemDetailsDto? Problem { get; }
    public string? RawBody { get; }
    public string? TraceId { get; }

    public string? ErrorCode => Problem?.ErrorCode;

    // On a 400 the errorCode is always `validation_failed`; the specific code is the last segment of `type`.
    public string? SpecificCode => Problem?.Type?.Split('/').LastOrDefault();

    public string? Detail => Problem?.Detail;
    public string? Title => Problem?.Title;

    // 4xx (except 408/429): the request itself is bad and nothing was created. Fix it and send it again;
    // the same merchantReference can be used. After a 5xx or a timeout the payment may be registered:
    // read Payment Status by merchantReference before you send again.
    public bool IsRetryable =>
        (int)StatusCode >= 500 ||
        StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;

    private static string BuildMessage(HttpStatusCode statusCode, ProblemDetailsDto? problem, string? rawBody)
    {
        if (problem is not null)
            return $"Payalo gateway returned {(int)statusCode}: [{problem.ErrorCode}] {problem.Detail}";

        var snippet = rawBody is { Length: > 0 } ? $" Body: {rawBody[..Math.Min(rawBody.Length, 500)]}" : "";
        return $"Payalo gateway returned {(int)statusCode} {statusCode}.{snippet}";
    }
}
