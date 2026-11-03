namespace Platform.Access.Infrastructure.Auditing;

/// <summary>
/// Final or intermediate destination of a prepared <see cref="AccessAuditRecord"/>.
/// Internal on purpose: the public contract stays <c>IAccessAuditWriter</c>.
/// </summary>
internal interface IAccessAuditSink
{
    ValueTask WriteAsync(AccessAuditRecord record, CancellationToken cancellationToken = default);
}
