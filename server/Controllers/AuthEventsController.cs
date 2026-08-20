using CltSharp.ServiceRequests.Api.Data;
using CltSharp.ServiceRequests.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace CltSharp.ServiceRequests.Api.Controllers;

[ApiController,Route("api/auth-events")]
public sealed class AuthEventsController(GeneralServiceRequestDbContext db,IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous,HttpPost("login")]
    public async Task<IActionResult> Login(LoginAuditRequest request,CancellationToken ct)
    {
        var expected=configuration["Auth0:LoginAuditSecret"]??string.Empty;
        var supplied=Request.Headers["X-Auth-Event-Secret"].FirstOrDefault()??string.Empty;
        if(string.IsNullOrEmpty(expected)||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected),Encoding.UTF8.GetBytes(supplied))) return Unauthorized();
        db.LoginAudits.Add(new LoginAuditEntity{Id=Guid.NewGuid(),Subject=request.Subject,Email=request.Email,IpAddress=request.IpAddress,CountryCode=request.CountryCode?.ToUpperInvariant(),UserAgent=request.UserAgent,Auth0ClientId=request.Auth0ClientId,SessionId=request.SessionId,Allowed=request.Allowed,DenyReason=request.DenyReason,CreatedAtUtc=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync(ct); return Accepted();
    }
}
