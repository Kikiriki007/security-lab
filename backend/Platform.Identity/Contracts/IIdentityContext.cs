using Platform.Identity.Domain.Enums;

namespace Platform.Identity.Contracts;

public sealed record AccountSnapshot(Guid Id, AccountStatus Status);

public interface IIdentityContext
{
    ValueTask<AccountSnapshot?> FindByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}