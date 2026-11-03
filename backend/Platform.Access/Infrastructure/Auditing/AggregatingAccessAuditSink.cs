using System.Runtime.ExceptionServices;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Folds repeated identical denials into one summary record so a flood of denials cannot flood the log
/// (T11). The key is actor + action + denial reason.
/// <list type="bullet">
/// <item>The first denial of a window is written at once with count 1.</item>
/// <item>Repeats inside the window are only counted.</item>
/// <item>A later write (of any key) that finds the window over writes one summary record whose count is
/// the number of repeats that were held back, with the time of the last one.</item>
/// </list>
/// Privileged (allowed) records are never aggregated. At most <see cref="AccessAuditOptions.MaxAggregationKeys"/>
/// keys are tracked; a denial for a key over that limit is written as it is, so no record is ever dropped.
/// Expired windows are closed on the next write, not by a timer, so the summary of the very last window
/// appears only when another denial arrives.
/// </summary>
internal sealed class AggregatingAccessAuditSink(
    IAccessAuditSink inner,
    AccessAuditOptions options,
    TimeProvider clock) : IAccessAuditSink
{
    private readonly object _gate = new();
    private readonly Dictionary<Key, Window> _windows = [];

    public async ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.EventType != AccessAuditRecord.DeniedEventType)
        {
            await inner.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            return;
        }

        var key = new Key(record.Actor, record.Action, record.DenialReason);
        var now = clock.GetUtcNow();
        List<AccessAuditRecord> summaries;
        Window? opened = null;
        var writeNow = false;

        lock (_gate)
        {
            summaries = CloseExpiredWindows(now);

            if (_windows.TryGetValue(key, out var window))
            {
                window.Held += Math.Max(1, record.Count);
                window.Last = record;
            }
            else
            {
                writeNow = true;
                if (_windows.Count < options.MaxAggregationKeys)
                {
                    opened = new Window(now);
                    _windows[key] = opened;
                }
            }
        }

        // A failed summary must not stop the current record from being written.
        ExceptionDispatchInfo? summaryFailure = null;
        foreach (var summary in summaries)
        {
            try
            {
                await inner.WriteAsync(summary, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                summaryFailure ??= ExceptionDispatchInfo.Capture(ex);
            }
        }

        if (writeNow)
        {
            try
            {
                await inner.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The first record was not written, so later repeats must not be held back as if it was.
                Forget(key, opened);
                throw;
            }
        }

        summaryFailure?.Throw();
    }

    private List<AccessAuditRecord> CloseExpiredWindows(DateTimeOffset now)
    {
        List<AccessAuditRecord> summaries = [];
        List<Key>? expired = null;

        foreach (var (key, window) in _windows)
        {
            if (now - window.Started < options.DenyAggregationWindow)
            {
                continue;
            }

            (expired ??= []).Add(key);
            if (window.Held > 0 && window.Last is not null)
            {
                summaries.Add(window.Last with { Count = window.Held });
            }
        }

        if (expired is not null)
        {
            foreach (var key in expired)
            {
                _windows.Remove(key);
            }
        }

        return summaries;
    }

    private void Forget(Key key, Window? opened)
    {
        if (opened is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_windows.TryGetValue(key, out var current) && ReferenceEquals(current, opened))
            {
                _windows.Remove(key);
            }
        }
    }

    private readonly record struct Key(string Actor, string Action, string? DenialReason);

    private sealed class Window(DateTimeOffset started)
    {
        public DateTimeOffset Started { get; } = started;

        public int Held { get; set; }

        public AccessAuditRecord? Last { get; set; }
    }
}
