using Microsoft.EntityFrameworkCore;

namespace ElectionAuthorization.Api.Data;

/// <summary>
/// Database context for Election Authorization identity and vote-consumption state.
/// </summary>
public sealed class AuthorizationDbContext(DbContextOptions<AuthorizationDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets subject to VID mapping records.
    /// </summary>
    public DbSet<VoterIdentityMapping> IdentityMappings => Set<VoterIdentityMapping>();

    /// <summary>
    /// Gets election-specific authorization state records.
    /// </summary>
    public DbSet<ElectionVoterAuthorization> ElectionAuthorizations => Set<ElectionVoterAuthorization>();

    /// <summary>
    /// Gets authorization grant records.
    /// </summary>
    public DbSet<AuthorizationGrantRecord> Grants => Set<AuthorizationGrantRecord>();

    /// <summary>
    /// Gets authorization audit event records.
    /// </summary>
    public DbSet<AuthorizationAuditEvent> AuditEvents => Set<AuthorizationAuditEvent>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VoterIdentityMapping>(entity =>
        {
            entity.ToTable("identity_mappings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Subject).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Vid).IsRequired().HasMaxLength(64);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Subject).IsUnique();
            entity.HasIndex(x => x.Vid).IsUnique();
        });

        modelBuilder.Entity<ElectionVoterAuthorization>(entity =>
        {
            entity.ToTable("election_authorizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ElectionId).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Vid).IsRequired().HasMaxLength(64);
            entity.Property(x => x.IsEligible).IsRequired();
            entity.Property(x => x.HasVoted).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.ElectionId, x.Vid }).IsUnique();
        });

        modelBuilder.Entity<AuthorizationGrantRecord>(entity =>
        {
            entity.ToTable("authorization_grants");
            entity.HasKey(x => x.GrantId);
            entity.Property(x => x.ElectionId).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Vid).IsRequired().HasMaxLength(64);
            entity.Property(x => x.IssuedAtUtc).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.IsConsumed).IsRequired();
            entity.Property(x => x.SubmissionNonce).HasMaxLength(128);
            entity.HasIndex(x => new { x.ElectionId, x.Vid, x.IsConsumed });
        });

        modelBuilder.Entity<AuthorizationAuditEvent>(entity =>
        {
            entity.ToTable("authorization_audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).IsRequired().HasMaxLength(64);
            entity.Property(x => x.SubjectHash).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Vid).HasMaxLength(64);
            entity.Property(x => x.ElectionId).HasMaxLength(128);
            entity.Property(x => x.Outcome).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Details).HasMaxLength(512);
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
        });
    }
}
