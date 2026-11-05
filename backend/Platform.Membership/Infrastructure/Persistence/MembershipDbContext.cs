using Microsoft.EntityFrameworkCore;

namespace Platform.Membership.Infrastructure.Persistence;

/// <summary>
/// Baza modula za članstvo (ACC-02, ACC-07). Ista PostgreSQL baza kao ostali moduli,
/// ali sopstvena šema i sopstvene migracije.
/// </summary>
public sealed class MembershipDbContext(DbContextOptions<MembershipDbContext> options) : DbContext(options)
{
    public const string Schema = "membership";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MembershipDbContext).Assembly);
    }
}