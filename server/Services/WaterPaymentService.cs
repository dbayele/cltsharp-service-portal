using CltSharp.ServiceRequests.Api.Data;
using CltSharp.ServiceRequests.Api.Models;
using CltSharp.ServiceRequests.Api.Security;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace CltSharp.ServiceRequests.Api.Services;

public interface IWaterBillingGateway
{
    Task<WaterBillAccountResponse?> LookupAsync(string accountNumber, CancellationToken ct);
}

// Development adapter only. Replace with Charlotte Water's authoritative CIS/billing integration.
public sealed class DevelopmentWaterBillingGateway(IConfiguration configuration) : IWaterBillingGateway
{
    public Task<WaterBillAccountResponse?> LookupAsync(string accountNumber, CancellationToken ct)
    {
        var normalized=new string((accountNumber??string.Empty).Where(char.IsLetterOrDigit).ToArray());
        if(normalized.Length<6) return Task.FromResult<WaterBillAccountResponse?>(null);
        var balance=configuration.GetValue<long>("WaterBilling:DevelopmentBalanceCents",12345);
        var masked=normalized.Length<=4?normalized:$"****{normalized[^4..]}";
        return Task.FromResult<WaterBillAccountResponse?>(new(masked,"Charlotte, North Carolina service address",balance,DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14))));
    }
}

public interface IWaterPaymentService
{
    Task<WaterPaymentIntentResponse> CreateIntentAsync(string subject,string? email,CreateWaterPaymentIntentRequest request,CancellationToken ct);
    Task<IReadOnlyList<SavedCardResponse>> GetSavedCardsAsync(string subject,CancellationToken ct);
    Task<IReadOnlyList<WaterPaymentHistoryResponse>> GetHistoryAsync(string subject,CancellationToken ct);
    Task ApplyStripeEventAsync(Event stripeEvent,CancellationToken ct);
}

public sealed class StripeWaterPaymentService(GeneralServiceRequestDbContext db,IWaterBillingGateway billing,IClientNetworkContext network,IConfiguration configuration) : IWaterPaymentService
{
    private readonly StripeClient stripe = new(configuration["Stripe:SecretKey"] ?? throw new InvalidOperationException("Stripe:SecretKey is required."));

    public async Task<WaterPaymentIntentResponse> CreateIntentAsync(string subject,string? email,CreateWaterPaymentIntentRequest request,CancellationToken ct)
    {
        if(request.AmountCents<100) throw new InvalidOperationException("Payment amount must be at least $1.00.");
        var account=await billing.LookupAsync(request.AccountNumber,ct) ?? throw new KeyNotFoundException("Water account was not found.");
        if(request.AmountCents>account.BalanceCents) throw new InvalidOperationException("Payment amount cannot exceed the current balance.");
        var customerId=await GetOrCreateCustomerAsync(subject,email,ct);
        if(!string.IsNullOrWhiteSpace(request.SavedPaymentMethodId)) await EnsurePaymentMethodOwnedByCustomer(request.SavedPaymentMethodId,customerId,ct);
        var options=new PaymentIntentCreateOptions
        {
            Amount=request.AmountCents,
            Currency="usd",
            Customer=customerId,
            Description="City of Charlotte, North Carolina water bill payment",
            AutomaticPaymentMethods=new PaymentIntentAutomaticPaymentMethodsOptions{Enabled=true},
            Metadata=new Dictionary<string,string>{{"auth0_subject",subject},{"water_account",request.AccountNumber}}
        };
        if(request.SavePaymentMethod) options.SetupFutureUsage="off_session";
        if(!string.IsNullOrWhiteSpace(request.SavedPaymentMethodId)) options.PaymentMethod=request.SavedPaymentMethodId;
        var intent=await stripe.V1.PaymentIntents.CreateAsync(options,cancellationToken:ct);
        var now=DateTimeOffset.UtcNow;
        var entity=new WaterPaymentEntity{Id=Guid.NewGuid(),Auth0Subject=subject,WaterAccountNumber=request.AccountNumber,AmountCents=request.AmountCents,Currency="usd",StripePaymentIntentId=intent.Id,Status=intent.Status,RequestIpAddress=network.IpAddress,RequestCountry=network.CountryCode,CreatedAtUtc=now,UpdatedAtUtc=now};
        db.WaterPayments.Add(entity); await db.SaveChangesAsync(ct);
        return new(entity.Id,intent.ClientSecret,intent.Id,request.AmountCents,"usd");
    }

    public async Task<IReadOnlyList<SavedCardResponse>> GetSavedCardsAsync(string subject,CancellationToken ct)
    {
        var profile=await db.ResidentProfiles.AsNoTracking().SingleOrDefaultAsync(x=>x.Auth0Subject==subject,ct);
        if(profile?.StripeCustomerId is null) return [];
        var methods=await stripe.V1.PaymentMethods.ListAsync(new PaymentMethodListOptions{Customer=profile.StripeCustomerId,Type="card"},cancellationToken:ct);
        return methods.Data.Where(x=>x.Card is not null).Select(x=>new SavedCardResponse(x.Id,x.Card.Brand,x.Card.Last4,x.Card.ExpMonth,x.Card.ExpYear)).ToList();
    }

    public async Task<IReadOnlyList<WaterPaymentHistoryResponse>> GetHistoryAsync(string subject,CancellationToken ct) =>
        await db.WaterPayments.AsNoTracking().Where(x=>x.Auth0Subject==subject).OrderByDescending(x=>x.CreatedAtUtc).Take(100)
            .Select(x=>new WaterPaymentHistoryResponse(x.Id,MaskAccount(x.WaterAccountNumber),x.AmountCents,x.Currency,x.Status,x.CreatedAtUtc,x.UpdatedAtUtc)).ToListAsync(ct);

    public async Task ApplyStripeEventAsync(Event stripeEvent,CancellationToken ct)
    {
        if(stripeEvent.Data.Object is not PaymentIntent intent) return;
        var payment=await db.WaterPayments.SingleOrDefaultAsync(x=>x.StripePaymentIntentId==intent.Id,ct);
        if(payment is null) return;
        payment.Status=intent.Status; payment.UpdatedAtUtc=DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct);
    }

    private async Task<string> GetOrCreateCustomerAsync(string subject,string? email,CancellationToken ct)
    {
        var profile=await db.ResidentProfiles.SingleOrDefaultAsync(x=>x.Auth0Subject==subject,ct);
        if(profile?.StripeCustomerId is not null) return profile.StripeCustomerId;
        var customer=await stripe.V1.Customers.CreateAsync(new CustomerCreateOptions{Email=email,Metadata=new Dictionary<string,string>{{"auth0_subject",subject}}},cancellationToken:ct);
        var now=DateTimeOffset.UtcNow;
        if(profile is null){profile=new ResidentProfileEntity{Id=Guid.NewGuid(),Auth0Subject=subject,StripeCustomerId=customer.Id,CreatedAtUtc=now,UpdatedAtUtc=now};db.ResidentProfiles.Add(profile);}else{profile.StripeCustomerId=customer.Id;profile.UpdatedAtUtc=now;}
        await db.SaveChangesAsync(ct); return customer.Id;
    }
    private async Task EnsurePaymentMethodOwnedByCustomer(string paymentMethodId,string customerId,CancellationToken ct)
    {
        var pm=await stripe.V1.PaymentMethods.GetAsync(paymentMethodId,cancellationToken:ct);
        if(pm.CustomerId!=customerId) throw new UnauthorizedAccessException("Saved payment method does not belong to this resident.");
    }
    private static string MaskAccount(string value)=>string.IsNullOrEmpty(value)?string.Empty:(value.Length<=4?value:$"****{value[^4..]}");
}
