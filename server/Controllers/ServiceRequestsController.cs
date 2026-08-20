using CltSharp.ServiceRequests.Api.Models;
using CltSharp.ServiceRequests.Api.Services;
using CltSharp.ServiceRequests.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace CltSharp.ServiceRequests.Api.Controllers;

[ApiController]
[Route("api/service-requests")]
public sealed class ServiceRequestsController(IServiceRequestRepository repository, IClientNetworkContext network) : ControllerBase
{
    private static readonly HashSet<string> SupportedServices = new(StringComparer.OrdinalIgnoreCase) { "crime-report", "crime-tip", "officer-commendation", "officer-misconduct", "police-report-supplement", "police-incident-report-copy", "police-crash-report-copy", "police-public-records", "police-alarm-registration", "police-false-alarm-appeal", "police-picketing-notification", "police-vacation-watch", "police-traffic-enforcement", "police-neighborhood-concern", "police-off-duty-event", "police-community-event", "police-fingerprinting", "fire-anonymous-hazard-report", "fire-hydrant-request", "fire-water-system-test-release", "fire-performance-test-fee", "fire-equipment-registration", "fire-system-impairment", "fire-shop-drawing-review", "fire-foster-home-inspection", "fire-report-copy", "fire-smoke-co-alarm", "fire-hazmat-property-records", "fire-off-duty-event-staff", "fire-truck-education", "fire-station-tour", "fire-inspection", "fire-reinspection", "fire-permit", "fire-plan-review", "fire-special-event", "fire-open-burning", "fire-key-box", "fire-code-question", "airport-lost-found", "airport-noise-complaint", "airport-feedback", "airport-accessibility-accommodation", "airport-ada-titlevi-complaint", "airport-parking-concern", "airport-ground-transportation", "airport-tour-request", "airport-community-engagement", "airport-app-account-support", "airport-public-records", "airport-badging-credentialing", "airport-tenant-vendor-access", "airport-construction-coordination", "airport-commercial-vehicle", "airport-concessions-vendor", "rental-registration", "right-of-way-sight-obstructions", "residential-traffic-calming-devices", "pothole-repair", "basketball-goal-in-street", "sidewalk-bike-lane-roadway-obstruction", "illegal-parking-tractor-trailers-bike-lanes", "streetlight-repair", "residential-street-light-request", "new-traffic-signal-request", "traffic-signal-malfunctioning", "traffic-signal-timing", "street-sign-repair", "new-street-sign-request", "graffiti", "signs-public-rights-of-way", "high-weeds-grass-junk", "parking-on-lawn-business-hours", "parking-on-lawn-after-hours", "boarded-up-residential-structure", "homeless-support-outreach", "trees", "plant-new-street-tree", "transit-concern", "flooding-drainage-structure", "erosion-creek-storm-drain", "blockage-city-drainage-system", "pollution-creeks-ponds-lakes", "zoning-enforcement-complaint", "zoning-holds-release", "zoning-property-question", "park-rec-service-request", "park-rec-refund-cancellation", "new-sidewalk", "sidewalk-repair", "container-obstruction", "crosswalk-request", "dead-animal-collection", "found-animal", "bulky-item-pickup", "curb-it-pickup-day" };
    [AllowAnonymous, HttpPost]
    public async Task<ActionResult<ServiceRequestResponse>> Create([FromBody] CreateServiceRequestRequest request, CancellationToken ct)
    {
        if(!SupportedServices.Contains(request.ServiceCode)) return ValidationProblem(new Dictionary<string,string[]>{{"serviceCode",["Unsupported service request type."]}});
        var errors=Validate(request); if(errors.Count>0) return ValidationProblem(errors);
        var now=DateTimeOffset.UtcNow; var prefix=request.ServiceCode switch { "crime-report"=>"CMPD-RPT","crime-tip"=>"CMPD-TIP","officer-commendation"=>"CMPD-COM","officer-misconduct"=>"CMPD-IAB", var code when code.StartsWith("police-",StringComparison.OrdinalIgnoreCase)=>"CMPD-SVC", var code when code.StartsWith("fire-",StringComparison.OrdinalIgnoreCase)=>"CFD-SVC", var code when code.StartsWith("airport-",StringComparison.OrdinalIgnoreCase)=>"CLT-AV", "rental-registration"=>"CLT-RNT",_=>"CLT-SR" };
        var submission=$"{prefix}-{now:yyyyMMdd}-{Random.Shared.Next(100000,999999)}";
        var subject=request.ServiceCode.Equals("crime-tip",StringComparison.OrdinalIgnoreCase)?null:(User.Identity?.IsAuthenticated==true?User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"):null);
        var record=new ServiceRequestRecord(Guid.NewGuid(),submission,request.ServiceCode,request.Data,now,now,"Received",subject,null,null,network.IpAddress,network.CountryCode,[]);
        await repository.AddAsync(record,ct); return CreatedAtAction(nameof(Get),new{submissionNumber=submission},new ServiceRequestResponse(record.SubmissionNumber,record.ReceivedAtUtc,record.Status));
    }
    [AllowAnonymous, HttpGet("{submissionNumber}")]
    public async Task<ActionResult<ServiceRequestResponse>> Get(string submissionNumber,CancellationToken ct){var i=await repository.GetAsync(submissionNumber,ct);return i is null?NotFound():Ok(new ServiceRequestResponse(i.SubmissionNumber,i.ReceivedAtUtc,i.Status));}
    private static Dictionary<string,string[]> Validate(CreateServiceRequestRequest request)
    {
        var errors=new Dictionary<string,string[]>();
        var required=request.ServiceCode switch{
            "crime-report"=>["emergency","jurisdiction","incidentAddress","offenseType","incidentDate","involvement","narrative","firstName","lastName","dateOfBirth","phone","email","contactAddress"],
            "crime-tip"=>["emergency","crimeType","tipNarrative"],"officer-commendation"=>["commendationNarrative","firstName","lastName","email"],
            "officer-misconduct"=>["incidentDate","incidentLocation","complaintType","complaintNarrative","firstName","lastName","phone","email"],
            "rental-registration"=>["propertyAddress","unitCount","propertyType","ownerType","ownerName","ownerAddress","ownerPhone","ownerEmail","responsiblePartyName","responsiblePartyPhone","responsiblePartyEmail","responsiblePartyAddress"],
            _=>["location","description"]};
        foreach(var field in required) if(!request.Data.TryGetValue(field,out var v)||IsEmpty(v)) errors[field]=["This field is required."];
        if(request.Data.TryGetValue("emergency",out var e)&&e.ValueKind==JsonValueKind.String&&string.Equals(e.GetString(),"Yes",StringComparison.OrdinalIgnoreCase)) errors["emergency"]=["Emergency incidents must be reported by calling 911."];
        return errors;
    }
    private static bool IsEmpty(JsonElement v)=>v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined||(v.ValueKind==JsonValueKind.String&&string.IsNullOrWhiteSpace(v.GetString()));
}
