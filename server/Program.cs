using CltSharp.ServiceRequests.Api.Data;
using CltSharp.ServiceRequests.Api.Security;
using CltSharp.ServiceRequests.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder=WebApplication.CreateBuilder(args);
var generalConnectionString=builder.Configuration.GetConnectionString("ServiceRequests")??throw new InvalidOperationException("Connection string 'ServiceRequests' is required.");
var policeConnectionString=builder.Configuration.GetConnectionString("Police")??throw new InvalidOperationException("Connection string 'Police' is required.");
var fireConnectionString=builder.Configuration.GetConnectionString("Fire")??throw new InvalidOperationException("Connection string 'Fire' is required.");
var airportConnectionString=builder.Configuration.GetConnectionString("Airport")??throw new InvalidOperationException("Connection string 'Airport' is required.");
var authDomain=builder.Configuration["Auth0:Domain"]??throw new InvalidOperationException("Auth0:Domain is required.");
var authAudience=builder.Configuration["Auth0:Audience"]??throw new InvalidOperationException("Auth0:Audience is required.");
builder.Services.AddControllers(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<GeneralServiceRequestDbContext>(o=>o.UseNpgsql(generalConnectionString));
builder.Services.AddDbContext<PoliceDbContext>(o=>o.UseNpgsql(policeConnectionString));
builder.Services.AddDbContext<FireDbContext>(o=>o.UseNpgsql(fireConnectionString));
builder.Services.AddDbContext<AirportDbContext>(o=>o.UseNpgsql(airportConnectionString));
builder.Services.AddScoped<IServiceRequestRepository,RoutedPostgreSqlServiceRequestRepository>();
builder.Services.AddScoped<IClientNetworkContext,ClientNetworkContext>();
builder.Services.AddScoped<IWaterBillingGateway,DevelopmentWaterBillingGateway>();
builder.Services.AddScoped<IWaterPaymentService,StripeWaterPaymentService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{o.Authority=$"https://{authDomain.TrimEnd('/')}/";o.Audience=authAudience;});
builder.Services.AddAuthorization();
builder.Services.AddCors(o=>o.AddPolicy("resident-ui",policy=>policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()??["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod()));
var app=builder.Build();
if(builder.Configuration.GetValue<bool>("Database:AutoCreate"))
{
    await using var scope=app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<GeneralServiceRequestDbContext>().Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<PoliceDbContext>().Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<FireDbContext>().Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<AirportDbContext>().Database.EnsureCreatedAsync();
}
app.UseHttpsRedirection(); app.UseCors("resident-ui"); app.UseMiddleware<UnitedStatesOnlyMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
app.MapGet("/health",async(GeneralServiceRequestDbContext g,PoliceDbContext p,FireDbContext f,AirportDbContext a,CancellationToken ct)=>
{
    var ga=await g.Database.CanConnectAsync(ct);var pa=await p.Database.CanConnectAsync(ct);var fa=await f.Database.CanConnectAsync(ct);var aa=await a.Database.CanConnectAsync(ct);
    return ga&&pa&&fa&&aa?Results.Ok(new{status="ok",databases=new{serviceRequests="ok",police="ok",fire="ok",airport="ok"}}):Results.Json(new{status="degraded",databases=new{serviceRequests=ga?"ok":"unavailable",police=pa?"ok":"unavailable",fire=fa?"ok":"unavailable",airport=aa?"ok":"unavailable"}},statusCode:503);
});
app.Run();
