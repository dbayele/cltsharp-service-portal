using CltSharp.ServiceRequests.Api.Models;
using CltSharp.ServiceRequests.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Security.Claims;

namespace CltSharp.ServiceRequests.Api.Controllers;

[ApiController]
public sealed class WaterPaymentsController(IWaterPaymentService payments,IWaterBillingGateway billing,IConfiguration configuration) : ControllerBase
{
    [Authorize, HttpPost("api/water-bills/lookup")]
    public async Task<ActionResult<WaterBillAccountResponse>> Lookup(WaterBillLookupRequest request,CancellationToken ct)
    {
        var result=await billing.LookupAsync(request.AccountNumber,ct); return result is null?NotFound(new{detail="Water account was not found."}):Ok(result);
    }

    [Authorize, HttpPost("api/water-bills/payment-intents")]
    public async Task<ActionResult<WaterPaymentIntentResponse>> CreateIntent(CreateWaterPaymentIntentRequest request,CancellationToken ct)
    {
        try{return Ok(await payments.CreateIntentAsync(Subject(),User.FindFirstValue(ClaimTypes.Email)??User.FindFirstValue("email"),request,ct));}
        catch(KeyNotFoundException ex){return NotFound(new{detail=ex.Message});}
        catch(UnauthorizedAccessException ex){return Forbid(ex.Message);}
        catch(InvalidOperationException ex){return BadRequest(new{detail=ex.Message});}
    }

    [Authorize, HttpGet("api/water-bills/saved-cards")]
    public async Task<ActionResult<IReadOnlyList<SavedCardResponse>>> SavedCards(CancellationToken ct)=>Ok(await payments.GetSavedCardsAsync(Subject(),ct));

    [Authorize, HttpGet("api/water-bills/payments")]
    public async Task<ActionResult<IReadOnlyList<WaterPaymentHistoryResponse>>> History(CancellationToken ct)=>Ok(await payments.GetHistoryAsync(Subject(),ct));

    [AllowAnonymous, HttpPost("api/payments/stripe-webhook")]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        var secret=configuration["Stripe:WebhookSecret"]??throw new InvalidOperationException("Stripe:WebhookSecret is required.");
        var json=await new StreamReader(Request.Body).ReadToEndAsync(ct);
        Event evt;
        try{evt=EventUtility.ConstructEvent(json,Request.Headers["Stripe-Signature"],secret);}catch(StripeException){return BadRequest();}
        if(evt.Type is "payment_intent.succeeded" or "payment_intent.payment_failed" or "payment_intent.canceled" or "payment_intent.processing") await payments.ApplyStripeEventAsync(evt,ct);
        return Ok();
    }
    private string Subject()=>User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub")??throw new UnauthorizedAccessException();
}
