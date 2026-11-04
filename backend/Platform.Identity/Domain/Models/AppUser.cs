using Microsoft.AspNetCore.Identity;
using Platform.Identity.Domain.Enums;

namespace Platform.Identity.Domain.Models;

public sealed class AppUser : IdentityUser<Guid>
{
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}