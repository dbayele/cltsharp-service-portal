using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CltSharp.ServiceRequests.Api.Models;

public sealed record CreateServiceRequestRequest(
    [property: Required] string ServiceCode,
    [property: Required] Dictionary<string, JsonElement> Data);

public sealed record ServiceRequestRecord(
    Guid Id,
    string SubmissionNumber,
    string ServiceCode,
    Dictionary<string, JsonElement> Data,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string? RequesterSubject,
    string? AssignedToSubject,
    string? AssignedToName,
    string? SubmissionIpAddress,
    string? SubmissionCountry,
    IReadOnlyList<InternalNote> InternalNotes);

public sealed record InternalNote(DateTimeOffset CreatedAtUtc, string AuthorSubject, string AuthorName, string Text);
public sealed record ServiceRequestResponse(string SubmissionNumber, DateTimeOffset ReceivedAtUtc, string Status);
public sealed record MyServiceRequestResponse(string SubmissionNumber, string ServiceCode, DateTimeOffset ReceivedAtUtc, DateTimeOffset UpdatedAtUtc, string Status);

public sealed record WaterBillLookupRequest([property: Required] string AccountNumber);
public sealed record WaterBillAccountResponse(string AccountNumberMasked, string ServiceAddress, long BalanceCents, DateOnly DueDate);
public sealed record CreateWaterPaymentIntentRequest([property: Required] string AccountNumber, long AmountCents, bool SavePaymentMethod, string? SavedPaymentMethodId);
public sealed record WaterPaymentIntentResponse(Guid PaymentId, string ClientSecret, string PaymentIntentId, long AmountCents, string Currency);
public sealed record SavedCardResponse(string Id, string Brand, string Last4, long? ExpMonth, long? ExpYear);
public sealed record WaterPaymentHistoryResponse(Guid Id, string AccountNumberMasked, long AmountCents, string Currency, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record LoginAuditRequest(string? Subject, string? Email, string? IpAddress, string? CountryCode, string? UserAgent, string? Auth0ClientId, string? SessionId, bool Allowed, string? DenyReason);
