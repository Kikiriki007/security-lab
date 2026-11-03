using Platform.Identity.Domain.Models;

namespace Platform.Identity.Contracts;

public interface IAccountRegistration
{
    ValueTask<RegisterAccountResult> RegisterAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken = default);
}