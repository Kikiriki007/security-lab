using Platform.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Identity.Domain;
using Platform.Identity;
using Platform.Identity.Data;
using Platform.Identity.Services;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Platform.Tests.Identity;

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

        Assert.That(user.Role, Is.EqualTo(DevelopmentRole.Student));
        Assert.That(user.Status, Is.EqualTo(AccountStatus.Active));
    }

    [TestCase(DevelopmentRole.Student, AccountStatus.Active)]
    [TestCase(DevelopmentRole.Teacher, AccountStatus.Active)]
    [TestCase(DevelopmentRole.Administrator, AccountStatus.Active)]
    [TestCase(DevelopmentRole.Student, AccountStatus.Blocked)]
    [TestCase(DevelopmentRole.Teacher, AccountStatus.Archived)]
    public void Create_UsesRequestedRoleAndStatus(DevelopmentRole role, AccountStatus status)
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
    public async Task CreateAsync_PersistsRequestedStatusEmailAndVerifiablePasswordHash()
    {
        using var context = NewContext();
        const string password = "database-test-password";

        var user = await TestUsers.CreateAsync(
            context, DevelopmentRole.Teacher, AccountStatus.Blocked, password);
        var saved = await context.Accounts.AsNoTracking().SingleAsync(account => account.Id == user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(saved.Email, Is.EqualTo(user.Email));
            Assert.That(saved.Status, Is.EqualTo(AccountStatus.Blocked));
            Assert.That(saved.PasswordHash, Is.Not.Null.And.Not.EqualTo(password));
            Assert.That(new BCryptPasswordHasher().Verify(password, saved.PasswordHash), Is.True);
        });
    }

    [Test]
    public async Task CreateAsync_WithoutPasswordLeavesPasswordHashNull()
    {
        using var context = NewContext();

        var user = await TestUsers.CreateAsync(context);
        var saved = await context.Accounts.AsNoTracking().SingleAsync(account => account.Id == user.Id);

        Assert.That(saved.PasswordHash, Is.Null);
    }

    [Test]
    public void AdminConfiguration_ContainsOnlyAdministratorIds_AndEmptyWhenThereAreNone()
    {
        var student = TestUsers.Build(DevelopmentRole.Student);
        var teacher = TestUsers.Build(DevelopmentRole.Teacher);
        var administrator = TestUsers.Build(DevelopmentRole.Administrator);

        var configuration = TestUsers.AdminConfiguration(student, administrator, teacher);
        var noAdministrators = TestUsers.AdminConfiguration(student, teacher);

        Assert.Multiple(() =>
        {
            Assert.That(configuration["Stub:AdminIds"], Is.EqualTo(administrator.Id.ToString()));
            Assert.That(noAdministrators["Stub:AdminIds"], Is.Empty);
        });
    }

    [Test]
    public async Task CreateAsync_AccountsAreVerifiedThroughTheIdentityModuleCredentialVerifier()
    {
        var services = new ServiceCollection()
            .AddIdentityModule(new ConfigurationBuilder().Build());
        services.RemoveAll<IDbContextOptionsConfiguration<PlatformIdentityDbContext>>();
        services.RemoveAll<DbContextOptions<PlatformIdentityDbContext>>();
        services.RemoveAll<PlatformIdentityDbContext>();
        services.AddDbContext<PlatformIdentityDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();
        var verifier = scope.ServiceProvider.GetRequiredService<ICredentialVerifier>();
        const string password = "login-test-password";

        var active = await TestUsers.CreateAsync(
            context, status: AccountStatus.Active, password: password);
        var blocked = await TestUsers.CreateAsync(
            context, status: AccountStatus.Blocked, password: password);

        var activeResult = await verifier.VerifyAsync(active.Email, password);
        var blockedResult = await verifier.VerifyAsync(blocked.Email, password);

        Assert.Multiple(() =>
        {
            Assert.That(activeResult.IsValid, Is.True);
            Assert.That(activeResult.Status, Is.EqualTo(AccountStatus.Active));
            Assert.That(blockedResult.IsValid, Is.True);
            Assert.That(blockedResult.Status, Is.EqualTo(AccountStatus.Blocked));
        });
    }

    private static PlatformIdentityDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlatformIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlatformIdentityDbContext(options);
    }
}
