using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Platform.Access.Infrastructure.Auditing;

namespace Platform.Access.Tests.Support;

/// <summary>Keeps every measurement of the audit failure counter made through one meter factory.</summary>
internal sealed class FailureCounterListener : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<(long Value, string? Reason, IReadOnlyList<string> TagKeys)> _measurements = [];

    public FailureCounterListener(IMeterFactory factory)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, factory) && instrument.Name == AccessAuditMetrics.FailuresName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var keys = new List<string>();
            string? reason = null;
            foreach (var tag in tags)
            {
                keys.Add(tag.Key);
                if (tag.Key == AccessAuditMetrics.ReasonTag)
                {
                    reason = tag.Value?.ToString();
                }
            }

            lock (_measurements)
            {
                _measurements.Add((value, reason, keys));
            }
        });
        _listener.Start();
    }

    public long Total
    {
        get
        {
            lock (_measurements)
            {
                return _measurements.Sum(m => m.Value);
            }
        }
    }

    public IReadOnlyList<string?> Reasons
    {
        get
        {
            lock (_measurements)
            {
                return [.. _measurements.Select(m => m.Reason)];
            }
        }
    }

    public IReadOnlyList<string> TagKeys
    {
        get
        {
            lock (_measurements)
            {
                return [.. _measurements.SelectMany(m => m.TagKeys).Distinct()];
            }
        }
    }

    public void Dispose() => _listener.Dispose();
}

/// <summary>A real <see cref="AccessAuditMetrics"/> on a real meter factory, with a listener on its counter.</summary>
internal sealed class TestMetrics : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly FailureCounterListener _listener;

    public TestMetrics()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = _provider.GetRequiredService<IMeterFactory>();
        Metrics = new AccessAuditMetrics(factory);
        _listener = new FailureCounterListener(factory);
    }

    public AccessAuditMetrics Metrics { get; }

    public long Total => _listener.Total;

    public IReadOnlyList<string?> Reasons => _listener.Reasons;

    public IReadOnlyList<string> TagKeys => _listener.TagKeys;

    public void Dispose()
    {
        _listener.Dispose();
        _provider.Dispose();
    }
}
