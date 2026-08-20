using CltSharp.EmployeeRequests.Api.Data;
using CltSharp.EmployeeRequests.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CltSharp.EmployeeRequests.Api.Services;

public enum RequestDomain { General, Police, Fire, Airport }

public static class RequestRouting
{
    private static readonly HashSet<string> PoliceServices=new(StringComparer.OrdinalIgnoreCase)
    {
        "crime-report","crime-tip","officer-commendation","officer-misconduct","police-report-supplement",
        "police-incident-report-copy","police-crash-report-copy","police-public-records","police-alarm-registration",
        "police-false-alarm-appeal","police-picketing-notification","police-vacation-watch","police-traffic-enforcement",
        "police-neighborhood-concern","police-off-duty-event","police-community-event","police-fingerprinting"
    };

    public static RequestDomain ForService(string code)=>PoliceServices.Contains(code)||code.StartsWith("police-",StringComparison.OrdinalIgnoreCase)
        ?RequestDomain.Police:code.StartsWith("fire-",StringComparison.OrdinalIgnoreCase)?RequestDomain.Fire:code.StartsWith("airport-",StringComparison.OrdinalIgnoreCase)?RequestDomain.Airport:RequestDomain.General;
    public static RequestDomain ForSubmission(string number)=>number.StartsWith("CMPD-",StringComparison.OrdinalIgnoreCase)?RequestDomain.Police:number.StartsWith("CFD-",StringComparison.OrdinalIgnoreCase)?RequestDomain.Fire:number.StartsWith("CLT-AV-",StringComparison.OrdinalIgnoreCase)?RequestDomain.Airport:RequestDomain.General;
    public static string DepartmentName(RequestDomain domain)=>domain switch{RequestDomain.Police=>"Police",RequestDomain.Fire=>"Fire",RequestDomain.Airport=>"Airport",_=>"General"};
}

public interface IEmployeeRequestRepository
{
    Task<ServiceRequestRecord?> GetAsync(string submissionNumber,CancellationToken ct);
    Task<IReadOnlyList<ServiceRequestRecord>> ListAsync(string scope,string? status,string? serviceCode,string? search,CancellationToken ct);
    Task<ServiceRequestRecord?> UpdateAsync(string submissionNumber,string? status,string? assignedSubject,string? assignedName,InternalNote? note,CancellationToken ct);
}

public sealed class EmployeeRequestRepository(GeneralServiceRequestDbContext generalDb,PoliceDbContext policeDb,FireDbContext fireDb,AirportDbContext airportDb):IEmployeeRequestRepository
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    public async Task<ServiceRequestRecord?> GetAsync(string number,CancellationToken ct)
    {
        var e=await DbForSubmission(number).ServiceRequests.AsNoTracking().SingleOrDefaultAsync(x=>x.SubmissionNumber==number,ct);
        return e is null?null:ToRecord(e);
    }

    public async Task<IReadOnlyList<ServiceRequestRecord>> ListAsync(string scope,string? status,string? serviceCode,string? search,CancellationToken ct)
    {
        var domains=ParseScope(scope);
        var tasks=domains.Select(d=>QueryAsync(DbForDomain(d),status,serviceCode,search,ct));
        var rows=(await Task.WhenAll(tasks)).SelectMany(x=>x).OrderByDescending(x=>x.ReceivedAtUtc).Take(1000).Select(ToRecord).ToList();
        return rows;
    }

    private static async Task<List<ServiceRequestEntity>> QueryAsync(ServiceRequestDbContext db,string? status,string? serviceCode,string? search,CancellationToken ct)
    {
        var q=db.ServiceRequests.AsNoTracking().AsQueryable();
        if(!string.IsNullOrWhiteSpace(status))q=q.Where(x=>x.Status==status);
        if(!string.IsNullOrWhiteSpace(serviceCode))q=q.Where(x=>x.ServiceCode==serviceCode);
        if(!string.IsNullOrWhiteSpace(search))
        {
            var term=search.Trim();
            q=q.Where(x=>EF.Functions.ILike(x.SubmissionNumber,$"%{term}%")||EF.Functions.ILike(x.ServiceCode,$"%{term}%")||(x.AssignedToName!=null&&EF.Functions.ILike(x.AssignedToName,$"%{term}%")));
        }
        return await q.OrderByDescending(x=>x.ReceivedAtUtc).Take(500).ToListAsync(ct);
    }

    public async Task<ServiceRequestRecord?> UpdateAsync(string number,string? status,string? assignedSubject,string? assignedName,InternalNote? note,CancellationToken ct)
    {
        var db=DbForSubmission(number);var e=await db.ServiceRequests.SingleOrDefaultAsync(x=>x.SubmissionNumber==number,ct);if(e is null)return null;
        if(!string.IsNullOrWhiteSpace(status))e.Status=status;
        if(assignedSubject is not null)e.AssignedToSubject=assignedSubject;
        if(assignedName is not null)e.AssignedToName=assignedName;
        if(note is not null){var notes=DeserializeNotes(e.InternalNotes);notes.Add(note);e.InternalNotes=JsonDocument.Parse(JsonSerializer.Serialize(notes,JsonOptions));}
        e.UpdatedAtUtc=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);return ToRecord(e);
    }

    private ServiceRequestDbContext DbForSubmission(string number)=>DbForDomain(RequestRouting.ForSubmission(number));
    private ServiceRequestDbContext DbForDomain(RequestDomain d)=>d switch{RequestDomain.Police=>policeDb,RequestDomain.Fire=>fireDb,RequestDomain.Airport=>airportDb,_=>generalDb};
    private static RequestDomain[] ParseScope(string? scope)=>scope?.ToLowerInvariant() switch{"police"=>[RequestDomain.Police],"fire"=>[RequestDomain.Fire],"general"=>[RequestDomain.General],_=>[RequestDomain.General,RequestDomain.Police,RequestDomain.Fire,RequestDomain.Airport]};
    private static ServiceRequestRecord ToRecord(ServiceRequestEntity e)=>new(e.Id,e.SubmissionNumber,e.ServiceCode,JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(e.Data.RootElement.GetRawText(),JsonOptions)??new(),e.ReceivedAtUtc,e.UpdatedAtUtc,e.Status,e.RequesterSubject,e.AssignedToSubject,e.AssignedToName,e.SubmissionIpAddress,e.SubmissionCountry,DeserializeNotes(e.InternalNotes));
    private static List<InternalNote> DeserializeNotes(JsonDocument d)=>JsonSerializer.Deserialize<List<InternalNote>>(d.RootElement.GetRawText(),JsonOptions)??new();
}
