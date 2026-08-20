using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CltSharp.ServiceRequests.Api.Data;

public abstract class ServiceRequestDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ServiceRequestEntity> ServiceRequests => Set<ServiceRequestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ServiceRequestEntity>();
        entity.ToTable("service_requests"); entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.SubmissionNumber).HasColumnName("submission_number").HasMaxLength(64).IsRequired();
        entity.HasIndex(x => x.SubmissionNumber).IsUnique().HasDatabaseName("ux_service_requests_submission_number");
        entity.Property(x => x.ServiceCode).HasColumnName("service_code").HasMaxLength(96).IsRequired();
        entity.HasIndex(x => new { x.ServiceCode, x.ReceivedAtUtc }).HasDatabaseName("ix_service_requests_service_code_received_at");
        entity.Property(x => x.Data).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        entity.HasIndex(x => new { x.Status, x.ReceivedAtUtc }).HasDatabaseName("ix_service_requests_status_received_at");
        entity.Property(x => x.RequesterSubject).HasColumnName("requester_subject").HasMaxLength(255);
        entity.HasIndex(x => new { x.RequesterSubject, x.ReceivedAtUtc }).HasDatabaseName("ix_service_requests_requester_received_at");
        entity.Property(x => x.AssignedToSubject).HasColumnName("assigned_to_subject").HasMaxLength(255);
        entity.Property(x => x.AssignedToName).HasColumnName("assigned_to_name").HasMaxLength(255);
        entity.Property(x => x.SubmissionIpAddress).HasColumnName("submission_ip_address").HasMaxLength(64);
        entity.Property(x => x.SubmissionCountry).HasColumnName("submission_country").HasMaxLength(2);
        entity.Property(x => x.InternalNotes).HasColumnName("internal_notes").HasColumnType("jsonb").IsRequired();
    }
}

public sealed class GeneralServiceRequestDbContext(DbContextOptions<GeneralServiceRequestDbContext> options) : ServiceRequestDbContext(options)
{
    public DbSet<ResidentProfileEntity> ResidentProfiles => Set<ResidentProfileEntity>();
    public DbSet<WaterPaymentEntity> WaterPayments => Set<WaterPaymentEntity>();
    public DbSet<LoginAuditEntity> LoginAudits => Set<LoginAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var profile=modelBuilder.Entity<ResidentProfileEntity>();
        profile.ToTable("resident_profiles"); profile.HasKey(x=>x.Id);
        profile.Property(x=>x.Id).HasColumnName("id");
        profile.Property(x=>x.Auth0Subject).HasColumnName("auth0_subject").HasMaxLength(255).IsRequired();
        profile.HasIndex(x=>x.Auth0Subject).IsUnique();
        profile.Property(x=>x.StripeCustomerId).HasColumnName("stripe_customer_id").HasMaxLength(128);
        profile.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        profile.Property(x=>x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        var payment=modelBuilder.Entity<WaterPaymentEntity>();
        payment.ToTable("water_payments"); payment.HasKey(x=>x.Id);
        payment.Property(x=>x.Id).HasColumnName("id");
        payment.Property(x=>x.Auth0Subject).HasColumnName("auth0_subject").HasMaxLength(255).IsRequired();
        payment.HasIndex(x=>new{x.Auth0Subject,x.CreatedAtUtc});
        payment.Property(x=>x.WaterAccountNumber).HasColumnName("water_account_number").HasMaxLength(64).IsRequired();
        payment.Property(x=>x.AmountCents).HasColumnName("amount_cents").IsRequired();
        payment.Property(x=>x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        payment.Property(x=>x.StripePaymentIntentId).HasColumnName("stripe_payment_intent_id").HasMaxLength(128).IsRequired();
        payment.HasIndex(x=>x.StripePaymentIntentId).IsUnique();
        payment.Property(x=>x.Status).HasColumnName("status").HasMaxLength(64).IsRequired();
        payment.Property(x=>x.RequestIpAddress).HasColumnName("request_ip_address").HasMaxLength(64);
        payment.Property(x=>x.RequestCountry).HasColumnName("request_country").HasMaxLength(2);
        payment.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        payment.Property(x=>x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        var audit=modelBuilder.Entity<LoginAuditEntity>();
        audit.ToTable("login_audits"); audit.HasKey(x=>x.Id);
        audit.Property(x=>x.Id).HasColumnName("id");
        audit.Property(x=>x.Subject).HasColumnName("subject").HasMaxLength(255);
        audit.Property(x=>x.Email).HasColumnName("email").HasMaxLength(320);
        audit.Property(x=>x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        audit.Property(x=>x.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        audit.Property(x=>x.UserAgent).HasColumnName("user_agent").HasMaxLength(1024);
        audit.Property(x=>x.Auth0ClientId).HasColumnName("auth0_client_id").HasMaxLength(255);
        audit.Property(x=>x.SessionId).HasColumnName("session_id").HasMaxLength(255);
        audit.Property(x=>x.Allowed).HasColumnName("allowed");
        audit.Property(x=>x.DenyReason).HasColumnName("deny_reason").HasMaxLength(512);
        audit.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        audit.HasIndex(x=>new{x.Subject,x.CreatedAtUtc});
        audit.HasIndex(x=>new{x.IpAddress,x.CreatedAtUtc});
    }
}

public sealed class PoliceDbContext(DbContextOptions<PoliceDbContext> options) : ServiceRequestDbContext(options);
public sealed class FireDbContext(DbContextOptions<FireDbContext> options) : ServiceRequestDbContext(options);
public sealed class AirportDbContext(DbContextOptions<AirportDbContext> options) : ServiceRequestDbContext(options);

public sealed class ServiceRequestEntity
{
    public Guid Id { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public JsonDocument Data { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RequesterSubject { get; set; }
    public string? AssignedToSubject { get; set; }
    public string? AssignedToName { get; set; }
    public string? SubmissionIpAddress { get; set; }
    public string? SubmissionCountry { get; set; }
    public JsonDocument InternalNotes { get; set; } = JsonDocument.Parse("[]");
}

public sealed class ResidentProfileEntity
{
    public Guid Id { get; set; }
    public string Auth0Subject { get; set; } = string.Empty;
    public string? StripeCustomerId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class WaterPaymentEntity
{
    public Guid Id { get; set; }
    public string Auth0Subject { get; set; } = string.Empty;
    public string WaterAccountNumber { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string Status { get; set; } = "created";
    public string? RequestIpAddress { get; set; }
    public string? RequestCountry { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class LoginAuditEntity
{
    public Guid Id { get; set; }
    public string? Subject { get; set; }
    public string? Email { get; set; }
    public string? IpAddress { get; set; }
    public string? CountryCode { get; set; }
    public string? UserAgent { get; set; }
    public string? Auth0ClientId { get; set; }
    public string? SessionId { get; set; }
    public bool Allowed { get; set; }
    public string? DenyReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
