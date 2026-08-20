namespace CltSharp.EmployeeRequests.Api.Security;
public sealed class UnitedStatesOnlyMiddleware(RequestDelegate next,IConfiguration configuration,IWebHostEnvironment environment)
{
 public async Task InvokeAsync(HttpContext context){if(context.Request.Path.StartsWithSegments("/health")){await next(context);return;}var h=configuration["NetworkSecurity:CountryHeader"]??"CF-IPCountry";var c=context.Request.Headers[h].FirstOrDefault()?.Trim().ToUpperInvariant();var fail=configuration.GetValue("NetworkSecurity:FailClosedWhenCountryMissing",!environment.IsDevelopment());if((string.IsNullOrWhiteSpace(c)&&fail)||(!string.IsNullOrWhiteSpace(c)&&c!="US")){context.Response.StatusCode=403;await context.Response.WriteAsJsonAsync(new{title="Access unavailable",detail="This employee service is available only from the United States."});return;}await next(context);}
}
