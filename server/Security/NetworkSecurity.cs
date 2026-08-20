using System.Net;

namespace CltSharp.ServiceRequests.Api.Security;

public interface IClientNetworkContext
{
    string? IpAddress { get; }
    string? CountryCode { get; }
}

public sealed class ClientNetworkContext(IHttpContextAccessor accessor, IConfiguration configuration) : IClientNetworkContext
{
    private HttpContext? Context => accessor.HttpContext;
    public string? IpAddress
    {
        get
        {
            var header=configuration["NetworkSecurity:ClientIpHeader"];
            if(configuration.GetValue<bool>("NetworkSecurity:TrustClientIpHeader") && !string.IsNullOrWhiteSpace(header))
            {
                var raw=Context?.Request.Headers[header].FirstOrDefault()?.Split(',')[0].Trim();
                if(IPAddress.TryParse(raw,out var parsed)) return parsed.ToString();
            }
            return Context?.Connection.RemoteIpAddress?.ToString();
        }
    }
    public string? CountryCode
    {
        get
        {
            var header=configuration["NetworkSecurity:CountryHeader"] ?? "CF-IPCountry";
            var value=Context?.Request.Headers[header].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value)?null:value.Trim().ToUpperInvariant();
        }
    }
}

public sealed class UnitedStatesOnlyMiddleware(RequestDelegate next, IConfiguration configuration, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if(IsExempt(context.Request.Path)) { await next(context); return; }
        var header=configuration["NetworkSecurity:CountryHeader"] ?? "CF-IPCountry";
        var country=context.Request.Headers[header].FirstOrDefault()?.Trim().ToUpperInvariant();
        var failClosed=configuration.GetValue("NetworkSecurity:FailClosedWhenCountryMissing", !environment.IsDevelopment());
        if(string.IsNullOrWhiteSpace(country))
        {
            if(failClosed){context.Response.StatusCode=403;await context.Response.WriteAsJsonAsync(new{title="Access unavailable",detail="This service is available only from the United States."});return;}
        }
        else if(country!="US")
        {
            context.Response.StatusCode=403; await context.Response.WriteAsJsonAsync(new{title="Access unavailable",detail="This service is available only from the United States."}); return;
        }
        await next(context);
    }
    private static bool IsExempt(PathString path) => path.StartsWithSegments("/api/auth-events/login") || path.StartsWithSegments("/api/payments/stripe-webhook") || path.StartsWithSegments("/health");
}
