using Platform.Identity.Domain.Enums;

namespace Platform.Identity.Contracts;

/// <summary>
/// IDN-05: administratorsko otključavanje. Pravo pristupa proverava pozivalac
/// (IAccessDecision, akcija identity.account.unlock) pre poziva.
/// </summary>
public interface IAccountLockoutAdministration
{
    Task<UnlockOutcome> UnlockAsync(Guid accountId, CancellationToken cancellationToken = default);
}
