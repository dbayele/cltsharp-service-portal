using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CltSharp.EmployeeRequests.Api.Models;

public sealed record ServiceRequestRecord(Guid Id,string SubmissionNumber,string ServiceCode,Dictionary<string,JsonElement> Data,DateTimeOffset ReceivedAtUtc,DateTimeOffset UpdatedAtUtc,string Status,string? RequesterSubject,string? AssignedToSubject,string? AssignedToName,string? SubmissionIpAddress,string? SubmissionCountry,IReadOnlyList<InternalNote> InternalNotes);
public sealed record InternalNote(DateTimeOffset CreatedAtUtc,string AuthorSubject,string AuthorName,string Text);
public sealed record EmployeeQueueItem(string SubmissionNumber,string ServiceCode,string Department,DateTimeOffset ReceivedAtUtc,DateTimeOffset UpdatedAtUtc,string Status,string? AssignedToName);
public sealed record EmployeeRequestDetail(string SubmissionNumber,string ServiceCode,string Department,Dictionary<string,JsonElement> Data,DateTimeOffset ReceivedAtUtc,DateTimeOffset UpdatedAtUtc,string Status,string? AssignedToSubject,string? AssignedToName,string? SubmissionIpAddress,string? SubmissionCountry,IReadOnlyList<InternalNote> InternalNotes);
public sealed record UpdateRequestRequest(string? Status,string? AssignedToSubject,string? AssignedToName);
public sealed record AddInternalNoteRequest([property: Required,MinLength(1),MaxLength(4000)] string Text);
public sealed record QueueSummary(string Scope,int Total,int Received,int Assigned,int InProgress,int Resolved,int Closed);
