using Microsoft.AspNetCore.Identity;
using Platform.Identity.Contracts;
using Platform.Identity.Domain.Enums;
using Platform.Identity.Domain.Models;

namespace Platform.Identity.Application;

public sealed class AccountLockoutAdministrationService(UserManager<AppUser> userManager)
    : IAccountLockoutAdministration
{
    public async Task<UnlockOutcome> UnlockAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(accountId.ToString());
        if (user is null)
            return UnlockOutcome.NotFound;

        var wasLockedOut = await userManager.IsLockedOutAsync(user);
        if (!wasLockedOut && user.AccessFailedCount == 0)
            return UnlockOutcome.NotLocked;

        var cleared = await userManager.SetLockoutEndDateAsync(user, null);
        if (!cleared.Succeeded)
            throw new InvalidOperationException("Otključavanje naloga nije uspelo.");

        var reset = await userManager.ResetAccessFailedCountAsync(user);
        if (!reset.Succeeded)
            throw new InvalidOperationException("Resetovanje brojača neuspešnih prijava nije uspelo.");

        return wasLockedOut ? UnlockOutcome.Unlocked : UnlockOutcome.NotLocked;
    }
}
