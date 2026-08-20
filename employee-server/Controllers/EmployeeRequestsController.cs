using CltSharp.EmployeeRequests.Api.Models;
using CltSharp.EmployeeRequests.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CltSharp.EmployeeRequests.Api.Controllers;

[ApiController,Authorize(Policy="Employee"),Route("api/requests")]
public sealed class EmployeeRequestsController(IEmployeeRequestRepository repository):ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeQueueItem>>> List([FromQuery]string scope="all",[FromQuery]string? status=null,[FromQuery]string? serviceCode=null,[FromQuery]string? search=null,CancellationToken ct=default)
    {
        var allowedScopes=AllowedScopes();
        if(scope.Equals("all",StringComparison.OrdinalIgnoreCase))
        {
            var rows=new List<ServiceRequestRecord>();
            foreach(var s in allowedScopes) rows.AddRange(await repository.ListAsync(s,status,serviceCode,search,ct));
            return Ok(rows.GroupBy(x=>x.SubmissionNumber).Select(g=>g.First()).OrderByDescending(x=>x.ReceivedAtUtc).Select(ToQueue));
        }
        if(!allowedScopes.Contains(scope,StringComparer.OrdinalIgnoreCase))return Forbid();
        var scoped=await repository.ListAsync(scope,status,serviceCode,search,ct);return Ok(scoped.Select(ToQueue));
    }

    [HttpGet("{number}")]
    public async Task<ActionResult<EmployeeRequestDetail>> Get(string number,CancellationToken ct)
    {
        var domain=RequestRouting.ForSubmission(number);if(!CanRead(domain))return Forbid();
        var row=await repository.GetAsync(number,ct);return row is null?NotFound():Ok(ToDetail(row));
    }

    [HttpPatch("{number}")]
    public async Task<ActionResult<EmployeeRequestDetail>> Update(string number,[FromBody]UpdateRequestRequest request,CancellationToken ct)
    {
        var domain=RequestRouting.ForSubmission(number);if(!CanWrite(domain))return Forbid();
        var assignedSubject=request.AssignedToSubject;var assignedName=request.AssignedToName;
        if(string.Equals(assignedSubject,"self",StringComparison.OrdinalIgnoreCase)){assignedSubject=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");assignedName=User.FindFirstValue("name")??User.Identity?.Name??User.FindFirstValue("email");}
        var row=await repository.UpdateAsync(number,request.Status,assignedSubject,assignedName,null,ct);return row is null?NotFound():Ok(ToDetail(row));
    }

    [HttpPost("{number}/notes")]
    public async Task<ActionResult<EmployeeRequestDetail>> AddNote(string number,[FromBody]AddInternalNoteRequest request,CancellationToken ct)
    {
        var domain=RequestRouting.ForSubmission(number);if(!CanWrite(domain))return Forbid();
        var sub=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub")??"unknown";var name=User.FindFirstValue("name")??User.Identity?.Name??sub;
        var row=await repository.UpdateAsync(number,null,null,null,new InternalNote(DateTimeOffset.UtcNow,sub,name,request.Text),ct);return row is null?NotFound():Ok(ToDetail(row));
    }

    private string[] AllowedScopes()=>new[]{RequestDomain.General,RequestDomain.Police,RequestDomain.Fire,RequestDomain.Airport}.Where(CanRead).Select(d=>d.ToString().ToLowerInvariant()).ToArray();
    private bool CanRead(RequestDomain d)=>d switch{RequestDomain.Police=>User.HasAnyPermission("read:police","read:public-safety"),RequestDomain.Fire=>User.HasAnyPermission("read:fire","read:public-safety"),RequestDomain.Airport=>User.HasPermission("read:airport"),_=>User.HasPermission("read:requests")};
    private bool CanWrite(RequestDomain d)=>d switch{RequestDomain.Police=>User.HasAnyPermission("write:police","write:public-safety"),RequestDomain.Fire=>User.HasAnyPermission("write:fire","write:public-safety"),RequestDomain.Airport=>User.HasPermission("write:airport"),_=>User.HasPermission("write:requests")};
    private static EmployeeQueueItem ToQueue(ServiceRequestRecord x){var d=RequestRouting.ForService(x.ServiceCode);return new(x.SubmissionNumber,x.ServiceCode,RequestRouting.DepartmentName(d),x.ReceivedAtUtc,x.UpdatedAtUtc,x.Status,x.AssignedToName);}
    private static EmployeeRequestDetail ToDetail(ServiceRequestRecord x){var d=RequestRouting.ForService(x.ServiceCode);return new(x.SubmissionNumber,x.ServiceCode,RequestRouting.DepartmentName(d),x.Data,x.ReceivedAtUtc,x.UpdatedAtUtc,x.Status,x.AssignedToSubject,x.AssignedToName,x.SubmissionIpAddress,x.SubmissionCountry,x.InternalNotes);}
}

public static class ClaimsPrincipalPermissions
{
    public static bool HasPermission(this ClaimsPrincipal user,string permission)=>user.Claims.Where(c=>c.Type=="permissions"||c.Type=="scope").SelectMany(c=>c.Value.Split(' ',StringSplitOptions.RemoveEmptyEntries)).Contains(permission,StringComparer.OrdinalIgnoreCase);
    public static bool HasAnyPermission(this ClaimsPrincipal user,params string[] permissions)=>permissions.Any(user.HasPermission);
}
