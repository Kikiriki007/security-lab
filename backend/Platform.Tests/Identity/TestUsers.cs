using Platform.Api.Services.Identity;
using Platform.Identity.Data;
using Platform.Identity.Domain;
using Platform.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace Platform.Tests.Identity;

/// <summary>
/// Role is NOT persisted because the account model has no role; only Administrator has an effect,
/// and only through <see cref="TestUsers.AdminConfiguration"/>.
/// </summary>
/// <param name="Password">Plain text kept only so tests can log in; it is never persisted.</param>
public sealed record TestUser(UserAccount Account, DevelopmentRole Role, string? Password = null)
{
    public Guid Id => Account.Id;
    public string Email => Account.Email;
    public AccountStatus Status => Account.Status;
}

/// <summary>
/// Test user helpers. Role is NOT persisted because the account model has no role; only Administrator
/// has an effect, and only through the configuration returned by <see cref="AdminConfiguration"/>.
/// </summary>
public static class TestUsers
{
    public static TestUser Create(
        IAccountRepository accounts,
        DevelopmentRole role = DevelopmentRole.Student,
        AccountStatus status = AccountStatus.Active,
        string? password = null,
        IPasswordHasher? hasher = null)
    {
        var user = Build(role, status, password, hasher);
        accounts.Save(user.Account);
        return user;
    }

    public static async Task<TestUser> CreateAsync(
        PlatformIdentityDbContext context,
        DevelopmentRole role = DevelopmentRole.Student,
        AccountStatus status = AccountStatus.Active,
        string? password = null,
        IPasswordHasher? hasher = null,
        CancellationToken ct = default)
    {
        var user = Build(role, status, password, hasher);
        context.Accounts.Add(user.Account);
        await context.SaveChangesAsync(ct);
        return user;
    }

    public static IReadOnlyDictionary<string, string?> AdminConfiguration(params TestUser[] users) =>
        new Dictionary<string, string?>
        {
            ["Stub:AdminIds"] = string.Join(
                ',',
                users.Where(user => user.Role == DevelopmentRole.Administrator)
                    .Select(user => user.Id))
        };

    public static TestUser Build(
        DevelopmentRole role = DevelopmentRole.Student,
        AccountStatus status = AccountStatus.Active,
        string? password = null,
        IPasswordHasher? hasher = null)
    {
        var id = Guid.NewGuid();
        var email = $"{role.ToString().ToLowerInvariant()}-{id:N}@example.com";
        var account = new UserAccount(id, email, status);
        if (password is not null)
            account.SetPasswordHash((hasher ?? new BCryptPasswordHasher()).Hash(password));

        return new TestUser(account, role, password);
    }
}
