using CltSharp.ServiceRequests.Api.Data;
using CltSharp.ServiceRequests.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CltSharp.ServiceRequests.Api.Services;

public interface IServiceRequestRepository
{
    Task AddAsync(ServiceRequestRecord request, CancellationToken cancellationToken);
    Task<ServiceRequestRecord?> GetAsync(string submissionNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<ServiceRequestRecord>> GetByRequesterAsync(string subject, CancellationToken cancellationToken);
}

public enum RequestDomain { General, Police, Fire, Airport }

public static class RequestRouting
{
    private static readonly HashSet<string> PoliceServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "crime-report","crime-tip","officer-commendation","officer-misconduct","police-report-supplement",
        "police-incident-report-copy","police-crash-report-copy","police-public-records","police-alarm-registration",
        "police-false-alarm-appeal","police-picketing-notification","police-vacation-watch","police-traffic-enforcement",
        "police-neighborhood-concern","police-off-duty-event","police-community-event","police-fingerprinting"
    };

    public static RequestDomain DomainForService(string serviceCode)
        => PoliceServices.Contains(serviceCode) || serviceCode.StartsWith("police-", StringComparison.OrdinalIgnoreCase)
            ? RequestDomain.Police
            : serviceCode.StartsWith("fire-", StringComparison.OrdinalIgnoreCase)
                ? RequestDomain.Fire
                : serviceCode.StartsWith("airport-", StringComparison.OrdinalIgnoreCase)
                    ? RequestDomain.Airport
                    : RequestDomain.General;

    public static RequestDomain DomainForSubmission(string submissionNumber)
        => submissionNumber.StartsWith("CMPD-", StringComparison.OrdinalIgnoreCase) ? RequestDomain.Police
            : submissionNumber.StartsWith("CFD-", StringComparison.OrdinalIgnoreCase) ? RequestDomain.Fire
            : submissionNumber.StartsWith("CLT-AV-", StringComparison.OrdinalIgnoreCase) ? RequestDomain.Airport
            : RequestDomain.General;
}

public sealed class RoutedPostgreSqlServiceRequestRepository(
    GeneralServiceRequestDbContext generalDb,
    PoliceDbContext policeDb,
    FireDbContext fireDb,
    AirportDbContext airportDb) : IServiceRequestRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AddAsync(ServiceRequestRecord request, CancellationToken ct)
    {
        var db = DbForService(request.ServiceCode);
        db.ServiceRequests.Add(ToEntity(request));
        await db.SaveChangesAsync(ct);
    }

    public async Task<ServiceRequestRecord?> GetAsync(string number, CancellationToken ct)
    {
        var e = await DbForSubmission(number).ServiceRequests.AsNoTracking().SingleOrDefaultAsync(x => x.SubmissionNumber == number, ct);
        return e is null ? null : ToRecord(e);
    }

    public async Task<IReadOnlyList<ServiceRequestRecord>> GetByRequesterAsync(string subject, CancellationToken ct)
    {
        var g = await QueryByRequester(generalDb, subject, ct);
        var p = await QueryByRequester(policeDb, subject, ct);
        var f = await QueryByRequester(fireDb, subject, ct);
        var a = await QueryByRequester(airportDb, subject, ct);
        return g.Concat(p).Concat(f).Concat(a).OrderByDescending(x => x.ReceivedAtUtc).Take(500).Select(ToRecord).ToList();
    }

    private static async Task<List<ServiceRequestEntity>> QueryByRequester(ServiceRequestDbContext db, string subject, CancellationToken ct)
        => await db.ServiceRequests.AsNoTracking().Where(x => x.RequesterSubject == subject).OrderByDescending(x => x.ReceivedAtUtc).Take(200).ToListAsync(ct);

    private ServiceRequestDbContext DbForService(string code) => RequestRouting.DomainForService(code) switch
    {
        RequestDomain.Police => policeDb,
        RequestDomain.Fire => fireDb,
        RequestDomain.Airport => airportDb,
        _ => generalDb
    };

    private ServiceRequestDbContext DbForSubmission(string number) => RequestRouting.DomainForSubmission(number) switch
    {
        RequestDomain.Police => policeDb,
        RequestDomain.Fire => fireDb,
        RequestDomain.Airport => airportDb,
        _ => generalDb
    };

    private static ServiceRequestEntity ToEntity(ServiceRequestRecord r) => new()
    {
        Id=r.Id,SubmissionNumber=r.SubmissionNumber,ServiceCode=r.ServiceCode,
        Data=JsonDocument.Parse(JsonSerializer.Serialize(r.Data,JsonOptions)),ReceivedAtUtc=r.ReceivedAtUtc,
        UpdatedAtUtc=r.UpdatedAtUtc,Status=r.Status,RequesterSubject=r.RequesterSubject,
        AssignedToSubject=r.AssignedToSubject,AssignedToName=r.AssignedToName,
        SubmissionIpAddress=r.SubmissionIpAddress,SubmissionCountry=r.SubmissionCountry,
        InternalNotes=JsonDocument.Parse(JsonSerializer.Serialize(r.InternalNotes,JsonOptions))
    };

    private static ServiceRequestRecord ToRecord(ServiceRequestEntity e) => new(
        e.Id,e.SubmissionNumber,e.ServiceCode,
        JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(e.Data.RootElement.GetRawText(),JsonOptions)??new(),
        e.ReceivedAtUtc,e.UpdatedAtUtc,e.Status,e.RequesterSubject,e.AssignedToSubject,e.AssignedToName,
        e.SubmissionIpAddress,e.SubmissionCountry,DeserializeNotes(e.InternalNotes));

    private static List<InternalNote> DeserializeNotes(JsonDocument d)
        => JsonSerializer.Deserialize<List<InternalNote>>(d.RootElement.GetRawText(),JsonOptions)??new();
}
