namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Settings for auditing access decisions (ACC-05).
/// </summary>
/// <remarks>
/// <see cref="PrivilegedActions"/> is a temporary source of truth for privileged actions
/// (Analiza_zadatka 9.1, actions marked with a star). Once the action catalog exposes
/// a privileged flag per action, the catalog becomes the source and this list is removed.
/// </remarks>
internal sealed class AccessAuditOptions
{
    public static readonly IReadOnlySet<string> DefaultPrivilegedActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "lab.revision.publish",
        "attempt.review",
        "account.status.change",
        "system.config.manage",
        "results.sensitive.read",
    };

    /// <summary>Actions whose Allow decisions are audited. Compared ordinally, never normalized.</summary>
    public IReadOnlySet<string> PrivilegedActions { get; init; } = DefaultPrivilegedActions;

    /// <summary>Upper bound for one audit write; the decision never waits longer.</summary>
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Window in which repeated identical denials are folded into one record.</summary>
    public TimeSpan DenyAggregationWindow { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum length of one audited value (same limit as the Audit module detail values).</summary>
    public int MaxValueLength { get; init; } = 64;

    /// <summary>Maximum number of distinct denial keys tracked at once.</summary>
    public int MaxAggregationKeys { get; init; } = 1024;
}
