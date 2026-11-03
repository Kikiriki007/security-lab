namespace Platform.Access.Tests.Support;

/// <summary>Test clock: time moves only when the test calls <see cref="Advance"/>.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
