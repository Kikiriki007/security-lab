using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Platform.Access.Application.Services;
using Platform.Access.Contracts;
using Platform.Access.Infrastructure.Auditing;
using Platform.Access.Infrastructure.Catalog;
using Platform.Access.Infrastructure.Policies;

namespace Platform.Access.Composition;

public static class AccessServiceCollectionExtensions
{
    public static IServiceCollection AddAccessControl(this IServiceCollection services)
    {
        // Both are no-ops when the host already registered them (TryAdd), so the host's choice wins
        // without relying on registration order.
        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AccessAuditOptions>();

        services.AddSingleton<InMemoryActionCatalog>();
        services.AddSingleton<IActionCatalog>(sp => sp.GetRequiredService<InMemoryActionCatalog>());
        services.AddSingleton<InMemoryAccessPolicyRegistry>();
        services.AddSingleton<IAccessPolicyRegistry>(sp => sp.GetRequiredService<InMemoryAccessPolicyRegistry>());
        services.AddSingleton<IAccessAuditWriter>(CreateAuditWriter);
        services.AddSingleton<IAccessDecision, DefaultDenyAccessDecision>();

        return services;
    }

    // Resilient(Filtering(Aggregating(Logger))): the outer layer guarantees a failing or slow audit
    // write never changes a decision; the filter keeps only denials and privileged allows; the
    // aggregator folds repeated denials; the logger is the destination until the Audit module adapter exists.
    // Every part is a singleton and depends only on singletons (no captive dependency).
    private static ResilientAccessAuditWriter CreateAuditWriter(IServiceProvider services)
    {
        var options = services.GetRequiredService<AccessAuditOptions>();
        var clock = services.GetRequiredService<TimeProvider>();

        var logger = new LoggerAccessAuditSink(services.GetRequiredService<ILogger<LoggerAccessAuditSink>>());
        var aggregating = new AggregatingAccessAuditSink(logger, options, clock);
        var filtering = new FilteringAccessAuditWriter(new AccessAuditRecordFactory(clock, options), aggregating, options);

        return new ResilientAccessAuditWriter(
            filtering, options, clock, services.GetRequiredService<ILogger<ResilientAccessAuditWriter>>());
    }
}
