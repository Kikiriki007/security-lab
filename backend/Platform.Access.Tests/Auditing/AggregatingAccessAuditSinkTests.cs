using Platform.Access.Infrastructure.Auditing;
using Platform.Access.Tests.Support;

namespace Platform.Access.Tests.Auditing;

[TestFixture]
public sealed class AggregatingAccessAuditSinkTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private ManualTimeProvider _clock = null!;
    private RecordingAuditSink _inner = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new ManualTimeProvider(Start);
        _inner = new RecordingAuditSink();
    }

    private AggregatingAccessAuditSink Sink(int maxKeys = 1024, IAccessAuditSink? inner = null) =>
        new(inner ?? _inner, new AccessAuditOptions { DenyAggregationWindow = Window, MaxAggregationKeys = maxKeys }, _clock);

    private AccessAuditRecord Denied(
        string actor = "user-1",
        string action = "lab.read",
        string reason = "no_policy",
        string correlationId = "corr-1") =>
        new(_clock.GetUtcNow(), AccessAuditRecord.DeniedEventType, actor, action, "-", "deny",
            correlationId, reason, null, "v0");

    private AccessAuditRecord PrivilegedAllowed() =>
        new(_clock.GetUtcNow(), AccessAuditRecord.PrivilegedAllowedEventType, "admin-1", "account.status.change", "-",
            "allow", "corr-2", null, "identity.account.status.change.admin-only", "v1");

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_FirstDeny_IsWrittenImmediately()
    {
        await Sink().WriteAsync(Denied());

        Assert.That(_inner.Records, Has.Count.EqualTo(1));
        Assert.That(_inner.Records[0].Count, Is.EqualTo(1));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_RepeatedDenies_AggregatedWithCount()
    {
        var sink = Sink();
        for (var i = 0; i < 5; i++)
        {
            await sink.WriteAsync(Denied());
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.That(_inner.Records, Has.Count.EqualTo(1), "repeats inside the window are held back");

        _clock.Advance(Window);
        await sink.WriteAsync(Denied(actor: "someone-else"));

        // First record (count 1) + summary of the 4 held-back repeats + the new, unrelated denial.
        Assert.That(_inner.Records, Has.Count.EqualTo(3));
        var summary = _inner.Records.Single(r => r.Actor == "user-1" && r.Count > 1);
        Assert.That(summary.Count, Is.EqualTo(4));
        Assert.That(summary.EventType, Is.EqualTo(AccessAuditRecord.DeniedEventType));
        Assert.That(summary.Action, Is.EqualTo("lab.read"));
        Assert.That(summary.DenialReason, Is.EqualTo("no_policy"));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_SummaryTime_IsTimeOfLastHeldBackDeny()
    {
        var sink = Sink();
        await sink.WriteAsync(Denied());
        _clock.Advance(TimeSpan.FromSeconds(3));
        await sink.WriteAsync(Denied());
        var lastRepeat = _clock.GetUtcNow();
        _clock.Advance(Window);
        await sink.WriteAsync(Denied(actor: "x"));

        // One repeat was held back; its summary carries the time of that repeat, not of the flush.
        var summary = _inner.Records.Single(r => r.Actor == "user-1" && r.Time == lastRepeat);
        Assert.That(summary.Count, Is.EqualTo(1));
    }

    [TestCase("actor")]
    [TestCase("action")]
    [TestCase("reason")]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_DifferentActorActionOrReason_NotMerged(string differs)
    {
        var sink = Sink();
        await sink.WriteAsync(Denied());

        await sink.WriteAsync(differs switch
        {
            "actor" => Denied(actor: "user-2"),
            "action" => Denied(action: "lab.write"),
            _ => Denied(reason: "unknown_action"),
        });

        Assert.That(_inner.Records, Has.Count.EqualTo(2));
        Assert.That(_inner.Records.Select(r => r.Count), Has.All.EqualTo(1));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_DifferentCorrelationId_StillAggregated()
    {
        var sink = Sink();
        await sink.WriteAsync(Denied(correlationId: "corr-1"));
        await sink.WriteAsync(Denied(correlationId: "corr-2"));

        Assert.That(_inner.Records, Has.Count.EqualTo(1));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_WindowExpired_NewRecord()
    {
        var sink = Sink();
        await sink.WriteAsync(Denied());
        _clock.Advance(Window + TimeSpan.FromSeconds(1));

        await sink.WriteAsync(Denied());

        Assert.That(_inner.Records, Has.Count.EqualTo(2));
        Assert.That(_inner.Records.Select(r => r.Count), Has.All.EqualTo(1), "nothing was held back, so no summary");
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_WindowEndsExactlyAtLimit()
    {
        var sink = Sink();
        await sink.WriteAsync(Denied());

        _clock.Advance(Window - TimeSpan.FromMilliseconds(1));
        await sink.WriteAsync(Denied());
        Assert.That(_inner.Records, Has.Count.EqualTo(1), "just inside the window");

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await sink.WriteAsync(Denied());
        Assert.That(_inner.Records, Has.Count.EqualTo(3), "summary of the held-back repeat plus a new first record");
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_NewWindowForSameKey_ReportsOldSummaryAndStartsAgain()
    {
        var sink = Sink();
        await sink.WriteAsync(Denied());
        await sink.WriteAsync(Denied());
        await sink.WriteAsync(Denied());
        _clock.Advance(Window);

        await sink.WriteAsync(Denied());

        var records = _inner.Records;
        Assert.That(records.Select(r => r.Count), Is.EqualTo(new[] { 1, 2, 1 }));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_PrivilegedRecords_NeverAggregated()
    {
        var sink = Sink();

        for (var i = 0; i < 3; i++)
        {
            await sink.WriteAsync(PrivilegedAllowed());
        }

        Assert.That(_inner.Records, Has.Count.EqualTo(3));
        Assert.That(_inner.Records.Select(r => r.EventType), Has.All.EqualTo(AccessAuditRecord.PrivilegedAllowedEventType));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_PrivilegedRecord_DoesNotFlushOrDisturbDenyWindows()
    {
        var sink = Sink();
        await sink.WriteAsync(Denied());
        await sink.WriteAsync(Denied());
        await sink.WriteAsync(PrivilegedAllowed());

        Assert.That(_inner.Records, Has.Count.EqualTo(2));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_KeyLimit_NoRecordsLost()
    {
        var sink = Sink(maxKeys: 2);

        for (var i = 0; i < 5; i++)
        {
            await sink.WriteAsync(Denied(actor: $"user-{i}"));
        }

        Assert.That(_inner.Records, Has.Count.EqualTo(5), "keys over the limit are written, not tracked and not dropped");

        // Repeats of an untracked key are written too instead of being silently dropped.
        await sink.WriteAsync(Denied(actor: "user-4"));
        await sink.WriteAsync(Denied(actor: "user-4"));
        Assert.That(_inner.Records, Has.Count.EqualTo(7));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_KeyLimit_ExpiredKeysAreReleased()
    {
        var sink = Sink(maxKeys: 2);
        await sink.WriteAsync(Denied(actor: "a"));
        await sink.WriteAsync(Denied(actor: "b"));
        _clock.Advance(Window);

        await sink.WriteAsync(Denied(actor: "c"));
        await sink.WriteAsync(Denied(actor: "c"));

        Assert.That(_inner.Records.Count(r => r.Actor == "c"), Is.EqualTo(1), "key c is tracked again after old keys expired");
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_InnerFails_FirstDenyIsNotSwallowedAsRepeat()
    {
        var failing = new FailingThenRecordingSink(failures: 1);
        var sink = Sink(inner: failing);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await sink.WriteAsync(Denied()));
        await sink.WriteAsync(Denied());

        Assert.That(failing.Inner.Records, Has.Count.EqualTo(1), "a failed first write must not turn later denials into silent repeats");
    }

    private sealed class FailOnCallSink(int failingCall) : IAccessAuditSink
    {
        private int _calls;

        public RecordingAuditSink Inner { get; } = new();

        public ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _calls) == failingCall
                ? throw new InvalidOperationException("sink failed")
                : Inner.WriteAsync(record, cancellationToken);
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_SummaryWriteFails_CurrentDenyIsStillWritten()
    {
        // Call 1 = first deny, call 2 = summary (fails), call 3 = first deny of the new window.
        var failing = new FailOnCallSink(failingCall: 2);
        var sink = Sink(inner: failing);
        await sink.WriteAsync(Denied());
        await sink.WriteAsync(Denied());
        _clock.Advance(Window);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await sink.WriteAsync(Denied()));

        Assert.That(failing.Inner.Records, Has.Count.EqualTo(2), "the new first record was written despite the failed summary");
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_Token_IsPassedToInnerSink()
    {
        using var cts = new CancellationTokenSource();

        await Sink().WriteAsync(Denied(), cts.Token);

        Assert.That(_inner.LastToken, Is.EqualTo(cts.Token));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public void ACC05_NullRecord_IsRejected()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () => await Sink().WriteAsync(null!));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_ParallelDenies_NoCounterLost()
    {
        var sink = Sink();

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(async () => await sink.WriteAsync(Denied()))));

        Assert.That(_inner.Records, Has.Count.EqualTo(1), "exactly one first record");

        _clock.Advance(Window);
        await sink.WriteAsync(Denied(actor: "flush"));

        var total = _inner.Records.Where(r => r.Actor == "user-1").Sum(r => r.Count);
        Assert.That(total, Is.EqualTo(100));
    }

    [Test]
    [Property("RequirementId", "ACC-05")]
    public async Task ACC05_ParallelMixedKeys_EveryDenyAccountedFor()
    {
        var sink = Sink();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i =>
            Task.Run(async () => await sink.WriteAsync(Denied(actor: $"user-{i % 10}")))));

        _clock.Advance(Window);
        await sink.WriteAsync(Denied(actor: "flush"));

        var total = _inner.Records.Where(r => r.Actor != "flush").Sum(r => r.Count);
        Assert.That(total, Is.EqualTo(200));
    }
}
