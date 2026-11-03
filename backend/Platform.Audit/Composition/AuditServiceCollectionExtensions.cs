using Microsoft.Extensions.DependencyInjection;

namespace Platform.Audit.Composition;

public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Audit module with the host. This is the single entry point
    /// that Platform.Api calls; the host never registers Audit internals directly.
    /// </summary>
    /// <remarks>
    /// Skeleton only: nothing is registered yet. The audit contract, the writer and
    /// the storage add their registrations here. Lifetimes follow
    /// docs/components/audit/DI-GUIDELINES.md and are verified by the
    /// scope-validation tests in Platform.Audit.Tests.
    /// </remarks>
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
