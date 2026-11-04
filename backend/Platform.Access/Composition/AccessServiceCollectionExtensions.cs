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
        services.AddMetrics();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AccessAuditMetrics>();
        services.TryAddSingleton<AccessAuditOptions>();

        services.AddSingleton<InMemoryActionCatalog>();
        services.AddSingleton<IActionCatalog>(sp => sp.GetRequiredService<InMemoryActionCatalog>());
        services.AddSingleton<InMemoryAccessPolicyRegistry>();
        services.AddSingleton<IAccessPolicyRegistry>(sp => sp.GetRequiredService<InMemoryAccessPolicyRegistry>());
        services.AddSingleton(CreateAggregatingSink);
        services.AddSingleton<IAccessAuditWriter>(CreateAuditWriter);
        services.AddHostedService<AccessAuditFlushService>();
        services.AddSingleton<IAccessDecision, DefaultDenyAccessDecision>();

        return services;
    }

    // Resilient(Filtering(Aggregating(Logger))): the outer layer guarantees a failing or slow audit
    // write never changes a decision; the filter keeps only denials and privileged allows; the
    // aggregator folds repeated denials; the logger is the destination until the Audit module adapter exists.
    // Every part is a singleton and depends only on singletons (no captive dependency). The aggregator is
    // registered on its own so AccessAuditFlushService flushes the very instance the writer chain uses.
    private static AggregatingAccessAuditSink CreateAggregatingSink(IServiceProvider services)
    {
        var logger = new LoggerAccessAuditSink(services.GetRequiredService<ILogger<LoggerAccessAuditSink>>());
        return new AggregatingAccessAuditSink(
            logger, services.GetRequiredService<AccessAuditOptions>(), services.GetRequiredService<TimeProvider>());
    }

    private static ResilientAccessAuditWriter CreateAuditWriter(IServiceProvider services)
    {
        var options = services.GetRequiredService<AccessAuditOptions>();
        var clock = services.GetRequiredService<TimeProvider>();

        var filtering = new FilteringAccessAuditWriter(
            new AccessAuditRecordFactory(clock, options),
            services.GetRequiredService<AggregatingAccessAuditSink>(),
            options);

        return new ResilientAccessAuditWriter(
            filtering,
            options,
            clock,
            services.GetRequiredService<ILogger<ResilientAccessAuditWriter>>(),
            services.GetRequiredService<AccessAuditMetrics>());
    }
}
