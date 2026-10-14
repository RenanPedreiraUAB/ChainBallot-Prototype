using Microsoft.EntityFrameworkCore;

namespace CynetAuth.Api.Data;

/// <summary>
/// Entity Framework database context for CynetAuth local identity data.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets stored local user credential records.
    /// </summary>
    public DbSet<UserCredential> Users => Set<UserCredential>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).IsRequired().HasMaxLength(128);
            entity.Property(x => x.NormalizedUsername).IsRequired().HasMaxLength(128);
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.PasswordSalt).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.NormalizedUsername).IsUnique();
        });
    }
}
