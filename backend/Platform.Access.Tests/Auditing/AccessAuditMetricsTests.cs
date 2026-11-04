using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Platform.Access.Infrastructure.Auditing;
using Platform.Access.Tests.Support;

namespace Platform.Access.Tests.Auditing;

[TestFixture]
public sealed class AccessAuditMetricsTests
{
    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_Counter_HasAgreedNameAndMeter()
    {
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        _ = new AccessAuditMetrics(factory);
        using var listener = new MeterListener();
        var seen = new List<(string Meter, string Instrument)>();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, factory))
            {
                seen.Add((instrument.Meter.Name, instrument.Name));
            }
        };
        listener.Start();

        Assert.That(seen, Is.EqualTo(new[] { ("Platform.Access", "access.audit.failures") }));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_BrokenMetricsListener_DoesNotThrow()
    {
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        var metrics = new AccessAuditMetrics(factory);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, factory))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("listener broke"));
        listener.Start();

        Assert.DoesNotThrow(() => metrics.RecordFailure("failed"), "a broken exporter must not change a decision");
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NullMeterFactory_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new AccessAuditMetrics(null!));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_EveryFailure_AddsOne()
    {
        using var test = new TestMetrics();

        test.Metrics.RecordFailure("failed");
        test.Metrics.RecordFailure("failed");
        test.Metrics.RecordFailure("timed_out");

        Assert.That(test.Total, Is.EqualTo(3));
        Assert.That(test.Reasons, Is.EqualTo(new[] { "failed", "failed", "timed_out" }));
    }
}
