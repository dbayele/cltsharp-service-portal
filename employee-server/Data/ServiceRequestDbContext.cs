using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CltSharp.EmployeeRequests.Api.Data;

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
        entity.Property(x => x.AssignedToSubject).HasColumnName("assigned_to_subject").HasMaxLength(255);
        entity.Property(x => x.AssignedToName).HasColumnName("assigned_to_name").HasMaxLength(255);
        entity.Property(x => x.SubmissionIpAddress).HasColumnName("submission_ip_address").HasMaxLength(64);
        entity.Property(x => x.SubmissionCountry).HasColumnName("submission_country").HasMaxLength(2);
        entity.Property(x => x.InternalNotes).HasColumnName("internal_notes").HasColumnType("jsonb").IsRequired();
    }
}

public sealed class GeneralServiceRequestDbContext(DbContextOptions<GeneralServiceRequestDbContext> options) : ServiceRequestDbContext(options);
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
