
using Microsoft.EntityFrameworkCore;
using Platform.Authoring.Infrastructure.Persistence.Entities;

namespace Platform.Authoring.Infrastructure.Persistence;

public sealed class AuthoringDbContext(
    DbContextOptions<AuthoringDbContext> options)
    : DbContext(options)
{
    public DbSet<LaboratoryRecord> Laboratories =>
        Set<LaboratoryRecord>();

    public DbSet<LabPrerequisiteRecord> LabPrerequisites =>
        Set<LabPrerequisiteRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("authoring");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AuthoringDbContext).Assembly);
    }
}
