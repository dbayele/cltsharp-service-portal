using CltSharp.ServiceRequests.Api.Models;
using CltSharp.ServiceRequests.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace CltSharp.ServiceRequests.Api.Controllers;
[ApiController, Authorize, Route("api/my/requests")]
public sealed class MyRequestsController(IServiceRequestRepository repository):ControllerBase{
 [HttpGet] public async Task<ActionResult<IReadOnlyList<MyServiceRequestResponse>>> Get(CancellationToken ct){var sub=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"); if(string.IsNullOrWhiteSpace(sub)) return Unauthorized(); var rows=await repository.GetByRequesterAsync(sub,ct); return Ok(rows.Select(x=>new MyServiceRequestResponse(x.SubmissionNumber,x.ServiceCode,x.ReceivedAtUtc,x.UpdatedAtUtc,x.Status)));}}
