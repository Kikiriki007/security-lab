using Microsoft.EntityFrameworkCore;
using Platform.Api.Services.Identity;
using Platform.Identity.Enums;
using Platform.Identity.Persistence;
using Platform.Identity.Services;

namespace Platform.Identity.Tests;

[TestFixture]
[Category("OPS-01")]
public class TestUsersTests
{
    private InMemoryAccountRepository _accounts = null!;

    [SetUp]
    public void SetUp()
    {
        _accounts = new InMemoryAccountRepository();
    }

    [Test]
    public void Create_Defaults_ToActiveStudent()
    {
        var user = TestUsers.Create(_accounts);

        Assert.That(user.Role, Is.EqualTo(AccountRole.Student));
        Assert.That(user.Status, Is.EqualTo(AccountStatus.Active));
    }

    [TestCase(AccountRole.Student, AccountStatus.Active)]
    [TestCase(AccountRole.Teacher, AccountStatus.Active)]
    [TestCase(AccountRole.Administrator, AccountStatus.Active)]
    [TestCase(AccountRole.Student, AccountStatus.Blocked)]
    [TestCase(AccountRole.Teacher, AccountStatus.Archived)]
    public void Create_UsesRequestedRoleAndStatus(AccountRole role, AccountStatus status)
    {
        var user = TestUsers.Create(_accounts, role, status);

        Assert.That(user.Role, Is.EqualTo(role));
        Assert.That(user.Status, Is.EqualTo(status));
    }

    [Test]
    public void Create_SavesTheAccountToTheRepository()
    {
        var user = TestUsers.Create(_accounts, status: AccountStatus.Blocked);

        var saved = _accounts.Find(user.Id);

        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Email, Is.EqualTo(user.Email));
        Assert.That(saved.Status, Is.EqualTo(user.Status));
    }

    [Test]
    public void Build_DoesNotSaveTheAccount()
    {
        var user = TestUsers.Build();

        Assert.That(_accounts.Find(user.Id), Is.Null);
    }

    [Test]
    public void Create_ReturnsDifferentUsersEachTime()
    {
        var first = TestUsers.Create(_accounts);
        var second = TestUsers.Create(_accounts);

        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        Assert.That(second.Email, Is.Not.EqualTo(first.Email));
    }

    [Test]
    public void Create_UsesTheReservedExampleDomain()
    {
        var user = TestUsers.Create(_accounts);

        Assert.That(user.Email, Does.EndWith("@example.com"));
    }

    [Test]
    public void Create_WithPassword_StoresAHashThatVerifies()
    {
        const string password = "pass-for-tests-123";
        var user = TestUsers.Create(_accounts, password: password);

        Assert.That(user.Password, Is.EqualTo(password));
        Assert.That(user.Account.PasswordHash, Is.Not.Null.And.Not.EqualTo(password));
        Assert.That(new BCryptPasswordHasher().Verify(password, user.Account.PasswordHash!), Is.True);
    }

    [Test]
    public void Create_WithoutPassword_HasNoPasswordHash()
    {
        var user = TestUsers.Create(_accounts);

        Assert.That(user.Password, Is.Null);
        Assert.That(user.Account.PasswordHash, Is.Null);
    }

    [Test]
    public async Task CreateAsync_SavesAccountWithRoleStatusAndHash_ToTheDatabase()
    {
        await using var context = InMemoryContext();
        const string password = "pass-for-database-tests";

        var user = await TestUsers.CreateAsync(
            context, AccountRole.Teacher, AccountStatus.Blocked, password);
        var saved = await context.Accounts.AsNoTracking().SingleAsync(account => account.Id == user.Id);

        Assert.That(saved.Role, Is.EqualTo(AccountRole.Teacher));
        Assert.That(saved.Status, Is.EqualTo(AccountStatus.Blocked));
        Assert.That(saved.PasswordHash, Is.Not.Null.And.Not.EqualTo(password));
        Assert.That(new BCryptPasswordHasher().Verify(password, saved.PasswordHash!), Is.True);
    }

    [TestCase(AccountStatus.Active)]
    [TestCase(AccountStatus.Blocked)]
    [TestCase(AccountStatus.Archived)]
    public async Task CreateAsync_UserWithPassword_CanBeVerifiedByCredentialVerifier(AccountStatus status)
    {
        await using var context = InMemoryContext();
        const string password = "pass-for-login-tests";
        var user = await TestUsers.CreateAsync(context, status: status, password: password);
        var verifier = new CredentialVerifier(new EfCredentialStore(context), new BCryptPasswordHasher());

        var result = await verifier.VerifyAsync(user.Email, password);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.AccountId, Is.EqualTo(user.Id));
        Assert.That(result.Status, Is.EqualTo(status));
    }

    [Test]
    public async Task CreateAsync_WithoutPassword_CannotLogIn()
    {
        await using var context = InMemoryContext();
        var user = await TestUsers.CreateAsync(context);
        var verifier = new CredentialVerifier(new EfCredentialStore(context), new BCryptPasswordHasher());

        var result = await verifier.VerifyAsync(user.Email, "any-password");

        Assert.That(user.Account.PasswordHash, Is.Null);
        Assert.That(result.IsValid, Is.False);
    }

    private static PlatformIdentityDbContext InMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PlatformIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlatformIdentityDbContext(options);
    }
}
