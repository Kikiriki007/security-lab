using Platform.Access.Contracts;
using Platform.Access.Domain.Models;

namespace Platform.Api.Services.Identity;

/// <summary>
/// IDN-03 rule "only an administrator changes an account status", expressed as an access policy so the
/// central engine (default deny, audit) decides it. Same meaning as <see cref="AdminOnlyAccessDecision"/>.
/// The engine has already rejected a missing or non-active subject before this policy runs;
/// the checks here are still strict so the policy is safe on its own.
/// </summary>
public sealed class AccountStatusChangePolicy(IAdminDirectory admins) : IAccessPolicy
{
    // One of the privileged actions whose Allow decisions are audited (Analiza_zadatka 9.1).
    public const string ActionId = "account.status.change";

    public string Id => "identity.account.status.change.admin-only";

    public string Version => "v1";

    public ValueTask<bool> EvaluateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Only the canonical "D" form (36 characters) is accepted. Guid parsing alone also tolerates
        // surrounding white space and a trailing newline; the engine gets the id from the server-side
        // identity, so any other spelling is a sign of a wrong caller.
        var allowed = request.Subject is { Id.Length: 36 }
            && Guid.TryParseExact(request.Subject.Id, "D", out var userId)
            && userId != Guid.Empty
            && admins.IsAdministrator(userId);

        return ValueTask.FromResult(allowed);
    }
}
