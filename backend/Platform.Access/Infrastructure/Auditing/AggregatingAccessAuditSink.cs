using System.Runtime.ExceptionServices;

namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Folds repeated identical denials into one summary record so a flood of denials cannot flood the log
/// (T11). The key is actor + action + target + denial reason, so probing many resources leaves one record per
/// resource instead of hiding all but the first and last one behind a single summary.
/// <list type="bullet">
/// <item>The first denial of a window is written at once with count 1.</item>
/// <item>Repeats inside the window are only counted.</item>
/// <item>A later write (of any key) that finds the window over writes one summary record whose count is
/// the number of repeats that were held back, with the time of the last one.</item>
/// </list>
/// Privileged (allowed) records are never aggregated. At most <see cref="AccessAuditOptions.MaxAggregationKeys"/>
/// keys are tracked. When that table is full, a denial for a new key is folded into one overflow window per
/// denial reason: the first one is written as it is and the rest are only counted, then reported as one summary
/// that names no actor, action or target (those are the many keys that did not fit). So a flood of distinct
/// actors cannot flood the log either, and no denial goes uncounted. If even the overflow windows would exceed
/// twice the key limit (only possible with many distinct denial reasons), the record is written as it is: memory
/// stays bounded and nothing is dropped.
/// Expired windows are closed on the next write or by <see cref="FlushExpiredAsync"/>; <see cref="FlushAllAsync"/>
/// also closes open windows (shutdown). A summary whose write fails is kept (bounded by
/// <see cref="AccessAuditOptions.MaxAggregationKeys"/>, oldest first) and retried by the next flush, so a failing
/// sink does not lose held-back counts. The caller is responsible for calling the flush methods periodically.
/// </summary>
internal sealed class AggregatingAccessAuditSink(
    IAccessAuditSink inner,
    AccessAuditOptions options,
    TimeProvider clock) : IAccessAuditSink
{
    private readonly object _gate = new();
    private readonly Dictionary<Key, Window> _windows = [];

    // End of the oldest open window. Writes skip the scan for expired windows until this time is reached,
    // so the common path under the lock is O(1) instead of a walk over every tracked key.
    private DateTimeOffset _nextExpiry = DateTimeOffset.MaxValue;

    // Summaries whose write failed. They are retried by the next flush, so a failing sink does not lose counts.
    private readonly List<AccessAuditRecord> _pending = [];

    public async ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.EventType != AccessAuditRecord.DeniedEventType)
        {
            await inner.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            return;
        }

        var key = new Key(record.Actor, record.Action, record.Target, record.DenialReason);
        var now = clock.GetUtcNow();
        List<AccessAuditRecord> summaries;
        Window? opened = null;
        var writeNow = false;

        lock (_gate)
        {
            summaries = CloseWindows(now, includeOpen: false);

            if (_windows.TryGetValue(key, out var window))
            {
                window.Held += Math.Max(1, record.Count);
                window.Last = record;
            }
            else if (_windows.Count < options.MaxAggregationKeys)
            {
                writeNow = true;
                opened = Open(key, now);
            }
            else
            {
                // The key table is full: count the denial in the overflow window of its reason.
                key = Key.OverflowOf(record.DenialReason);
                if (_windows.TryGetValue(key, out var overflow))
                {
                    overflow.Held += Math.Max(1, record.Count);
                    overflow.Last = record;
                }
                else
                {
                    writeNow = true;
                    if (_windows.Count < 2L * options.MaxAggregationKeys)
                    {
                        opened = Open(key, now);
                    }
                }
            }
        }

        // A failed summary must not stop the current record from being written.
        var summaryFailure = await WriteSummariesAsync(summaries, cancellationToken).ConfigureAwait(false);

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

    /// <summary>Writes the summaries of expired windows and retries failed ones, without waiting for another denial.</summary>
    public ValueTask FlushExpiredAsync(CancellationToken cancellationToken = default) =>
        FlushAsync(includeOpen: false, cancellationToken);

    /// <summary>Writes every held-back count, also of windows that are still open (used at shutdown).</summary>
    public ValueTask FlushAllAsync(CancellationToken cancellationToken = default) =>
        FlushAsync(includeOpen: true, cancellationToken);

    private async ValueTask FlushAsync(bool includeOpen, CancellationToken cancellationToken)
    {
        List<AccessAuditRecord> summaries;
        lock (_gate)
        {
            // Older failed summaries go first, so the log keeps its order and the oldest survive the bound.
            var closed = CloseWindows(clock.GetUtcNow(), includeOpen);
            summaries = [.. _pending, .. closed];
            _pending.Clear();
        }

        (await WriteSummariesAsync(summaries, cancellationToken).ConfigureAwait(false))?.Throw();
    }

    // Tries every summary; a failed one goes back to the retry list instead of being lost.
    private async ValueTask<ExceptionDispatchInfo?> WriteSummariesAsync(
        List<AccessAuditRecord> summaries,
        CancellationToken cancellationToken)
    {
        ExceptionDispatchInfo? failure = null;
        foreach (var summary in summaries)
        {
            try
            {
                await inner.WriteAsync(summary, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure ??= ExceptionDispatchInfo.Capture(ex);
                Requeue(summary);
            }
        }

        return failure;
    }

    // The retry list is bounded like the window table; beyond that the newest summary is dropped.
    private void Requeue(AccessAuditRecord summary)
    {
        lock (_gate)
        {
            if (_pending.Count < options.MaxAggregationKeys)
            {
                _pending.Add(summary);
            }
        }
    }

    private Window Open(Key key, DateTimeOffset now)
    {
        var window = new Window(now);
        _windows[key] = window;

        var end = now + options.DenyAggregationWindow;
        if (end < _nextExpiry)
        {
            _nextExpiry = end;
        }

        return window;
    }

    private List<AccessAuditRecord> CloseWindows(DateTimeOffset now, bool includeOpen)
    {
        List<AccessAuditRecord> summaries = [];

        if (!includeOpen && now < _nextExpiry)
        {
            return summaries;
        }

        List<Key>? expired = null;
        var nextExpiry = DateTimeOffset.MaxValue;

        foreach (var (key, window) in _windows)
        {
            var end = window.Started + options.DenyAggregationWindow;
            if (!includeOpen && now < end)
            {
                if (end < nextExpiry)
                {
                    nextExpiry = end;
                }

                continue;
            }

            (expired ??= []).Add(key);
            if (window.Held > 0 && window.Last is not null)
            {
                summaries.Add(SummaryOf(key, window));
            }
        }

        if (expired is not null)
        {
            foreach (var key in expired)
            {
                _windows.Remove(key);
            }
        }

        _nextExpiry = nextExpiry;
        return summaries;
    }

    // The summary of an overflow window covers many keys, so it names none of them.
    private static AccessAuditRecord SummaryOf(Key key, Window window)
    {
        var summary = window.Last! with { Count = window.Held };

        return key.IsOverflow
            ? summary with
            {
                Actor = AccessAuditRecord.NoValue,
                Action = AccessAuditRecord.NoValue,
                Target = AccessAuditRecord.NoValue,
                CorrelationId = AccessAuditRecord.NoValue,
            }
            : summary;
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

                // Repeats counted while the first write was in flight must still be reported.
                if (opened.Held > 0 && opened.Last is not null && _pending.Count < options.MaxAggregationKeys)
                {
                    _pending.Add(SummaryOf(key, opened));
                }
            }
        }
    }

    private readonly record struct Key(
        string Actor, string Action, string Target, string? DenialReason, bool IsOverflow = false)
    {
        public static Key OverflowOf(string? denialReason) =>
            new(AccessAuditRecord.NoValue, AccessAuditRecord.NoValue, AccessAuditRecord.NoValue, denialReason, true);
    }

    private sealed class Window(DateTimeOffset started)
    {
        public DateTimeOffset Started { get; } = started;

        public int Held { get; set; }

        public AccessAuditRecord? Last { get; set; }
    }
}
