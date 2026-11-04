using System.Diagnostics.Metrics;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Counter of audit failures: a second signal next to the <c>access.audit.failure</c> log alarm. The alarm
/// goes through the same logging pipeline as the audit record, so when that pipeline is what failed, both the
/// record and the alarm are lost and nothing is left to notice. A counter is read by a different path
/// (metrics exporter, health check, dashboard), so it survives that case.
/// </summary>
internal sealed class AccessAuditMetrics
{
    public const string MeterName = "Platform.Access";
    public const string FailuresName = "access.audit.failures";
    public const string ReasonTag = "reason";

    private readonly Counter<long> _failures;

    public AccessAuditMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _failures = meterFactory.Create(MeterName).CreateCounter<long>(
            FailuresName,
            unit: "{failure}",
            description: "Access audit writes or flushes that failed or timed out. Tag: reason.");
    }

    /// <summary>
    /// Counts one failure. The only tag is the fixed reason, never a message, actor or correlation id.
    /// Never throws: a broken metrics listener must not change an access decision.
    /// </summary>
    public void RecordFailure(string reason)
    {
        try
        {
            _failures.Add(1, new KeyValuePair<string, object?>(ReasonTag, reason));
        }
        catch (Exception)
        {
            // Metrics are a side channel; they must never stop the audit chain or a decision.
        }
    }
}
