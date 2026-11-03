namespace Platform.Audit.Tests;

// Deliberate misconfiguration used to prove that scope validation is really
// switched on: a singleton that captures a scoped service (a "captive dependency").
internal sealed class ScopedDependency
{
}

internal sealed class SingletonConsumer(ScopedDependency dependency)
{
    public ScopedDependency Dependency { get; } = dependency;
}
