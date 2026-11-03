using Platform.Api.Interfaces.Identity;
using Platform.Identity.Entities;
using Platform.Identity.Enums;
using Platform.Identity.Interfaces;
using Platform.Identity.Persistence;
using Platform.Identity.Services;

namespace Platform.Identity.Tests;

/// <summary>A test account, with its plaintext password retained only for test login requests.</summary>
/// <param name="Password">Plain text kept only so tests can log in; it is never persisted.</param>
public sealed record TestUser(UserAccount Account, string? Password = null)
{
    public Guid Id => Account.Id;
    public string Email => Account.Email;
    public AccountStatus Status => Account.Status;
    public AccountRole Role => Account.Role;
}

public static class TestUsers
{
    /// <summary>Creates a user in the in-memory account repository used by repository-backed tests.</summary>
    public static TestUser Create(
        IAccountRepository accounts,
        AccountRole role = AccountRole.Student,
        AccountStatus status = AccountStatus.Active,
        string? password = null,
        IPasswordHasher? hasher = null)
    {
        var user = Build(role, status, password, hasher);
        accounts.Save(user.Account);
        return user;
    }

    /// <summary>Creates a user in the identity database used by credential verification and login tests.</summary>
    public static async Task<TestUser> CreateAsync(
        PlatformIdentityDbContext context,
        AccountRole role = AccountRole.Student,
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

    public static TestUser Build(
        AccountRole role = AccountRole.Student,
        AccountStatus status = AccountStatus.Active,
        string? password = null,
        IPasswordHasher? hasher = null)
    {
        var id = Guid.NewGuid();
        var email = $"{role.ToString().ToLowerInvariant()}-{id:N}@example.com";
        var account = new UserAccount(id, email, status, role);
        if (password is not null)
            account.SetPasswordHash((hasher ?? new BCryptPasswordHasher()).Hash(password));

        return new TestUser(account, password);
    }
}
