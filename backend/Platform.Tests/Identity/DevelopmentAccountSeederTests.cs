using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Api.Services.Identity;
using Platform.Identity;
using Platform.Identity.Data;
using Platform.Identity.Domain;
using Platform.Identity.Services;

namespace Platform.Tests.Identity;

[TestFixture]
[Category("OPS-01")]
public class DevelopmentAccountSeederTests
{
    [Test]
    public void All_ContainsStudentTeacherAdministratorAndBlockedAccount()
    {
        Assert.That(DevelopmentAccountSeeder.All, Has.Count.EqualTo(4));
        Assert.That(DevelopmentAccountSeeder.All.Count(account => account.Status == AccountStatus.Blocked), Is.EqualTo(1));
        Assert.That(
            DevelopmentAccountSeeder.All
                .Where(account => account.Status == AccountStatus.Active)
                .Select(account => account.Role),
            Is.EquivalentTo(new[] { DevelopmentRole.Student, DevelopmentRole.Teacher, DevelopmentRole.Administrator }));
    }

    [Test]
    public void All_HaveUniqueIdsAndEmails()
    {
        Assert.That(
            DevelopmentAccountSeeder.All.Select(account => account.Id).Distinct().Count(),
            Is.EqualTo(DevelopmentAccountSeeder.All.Count));
        Assert.That(
            DevelopmentAccountSeeder.All.Select(account => account.Email).Distinct(StringComparer.Ordinal).Count(),
            Is.EqualTo(DevelopmentAccountSeeder.All.Count));
    }

    [Test]
    public void All_UseTheReservedExampleDomain()
    {
        Assert.That(
            DevelopmentAccountSeeder.All.Select(account => account.Email),
            Has.All.Matches<string>(email => email.EndsWith("@example.com")));
    }

    [Test]
    public void All_EmailsAreInTheNormalizedFormUsedByLogin()
    {
        Assert.That(
            DevelopmentAccountSeeder.All.Select(account => account.Email),
            Has.All.Matches<string>(email => email == CredentialVerifier.NormalizeEmail(email)));
    }

    [Test]
    public async Task UpsertAccountsAsync_AddsAllFourAccountsAndSetsTheirPasswords()
    {
        using var context = NewContext();
        const string password = "seed-test-password";

        var result = await DevelopmentAccountSeeder.UpsertAccountsAsync(
            context, new BCryptPasswordHasher(), password);
        var accounts = await context.Accounts.AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Added, Is.EqualTo(4));
            Assert.That(result.PasswordsSet, Is.EqualTo(4));
            Assert.That(accounts.Select(account => account.Email),
                Is.EquivalentTo(DevelopmentAccountSeeder.All.Select(account => account.Email)));
            Assert.That(accounts.Count(account => account.Status == AccountStatus.Blocked), Is.EqualTo(1));
            Assert.That(accounts, Has.All.Matches<UserAccount>(account =>
                account.PasswordHash is not null &&
                account.PasswordHash != password &&
                new BCryptPasswordHasher().Verify(password, account.PasswordHash)));
        });
    }

    [Test]
    public async Task UpsertAccountsAsync_IsIdempotentAndDoesNotChangeHashes()
    {
        using var context = NewContext();
        var hasher = new BCryptPasswordHasher();
        await DevelopmentAccountSeeder.UpsertAccountsAsync(context, hasher, "seed-test-password");
        var hashes = await context.Accounts.AsNoTracking()
            .ToDictionaryAsync(account => account.Email, account => account.PasswordHash);

        var result = await DevelopmentAccountSeeder.UpsertAccountsAsync(context, hasher, "another-password");
        var accountCount = await context.Accounts.CountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Added, Is.Zero);
            Assert.That(result.PasswordsSet, Is.Zero);
            Assert.That(accountCount, Is.EqualTo(4));
            Assert.That(
                context.Accounts.AsNoTracking().ToDictionary(account => account.Email, account => account.PasswordHash),
                Is.EquivalentTo(hashes));
        });
    }

    [Test]
    public async Task UpsertAccountsAsync_DoesNotOverwriteAnExistingHash()
    {
        using var context = NewContext();
        var definition = DevelopmentAccountSeeder.All[0];
        var existingHash = new BCryptPasswordHasher().Hash("different-known-password");
        var existing = new UserAccount(definition.Id, definition.Email, definition.Status);
        existing.SetPasswordHash(existingHash);
        context.Accounts.Add(existing);
        await context.SaveChangesAsync();

        var result = await DevelopmentAccountSeeder.UpsertAccountsAsync(
            context, new BCryptPasswordHasher(), "seed-test-password");

        Assert.Multiple(() =>
        {
            Assert.That(result.PasswordsSet, Is.EqualTo(3));
            Assert.That(context.Accounts.Single(account => account.Id == definition.Id).PasswordHash,
                Is.EqualTo(existingHash));
        });
    }

    [Test]
    public async Task UpsertAccountsAsync_FillsAnExistingMissingHash()
    {
        using var context = NewContext();
        var definition = DevelopmentAccountSeeder.All[0];
        context.Accounts.Add(new UserAccount(definition.Id, definition.Email, definition.Status));
        await context.SaveChangesAsync();

        var result = await DevelopmentAccountSeeder.UpsertAccountsAsync(
            context, new BCryptPasswordHasher(), "seed-test-password");

        Assert.Multiple(() =>
        {
            Assert.That(result.Added, Is.EqualTo(3));
            Assert.That(result.PasswordsSet, Is.EqualTo(4));
            Assert.That(context.Accounts.Single(account => account.Id == definition.Id).PasswordHash, Is.Not.Null);
        });
    }

    [Test]
    public void Administrator_UsesTheIdFromStubAdminIds()
    {
        var administrator = DevelopmentAccountSeeder.All.Single(account => account.Role == DevelopmentRole.Administrator);
        Assert.That(administrator.Id, Is.EqualTo(DevelopmentAccountSeeder.AdminId));

        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");
        if (!File.Exists(settingsPath))
            Assert.Ignore("appsettings.Development.json is not available in the test output directory.");

        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var adminIds = settings.RootElement.GetProperty("Stub").GetProperty("AdminIds").GetString();
        Assert.That(adminIds, Does.Contain(DevelopmentAccountSeeder.AdminId.ToString()));
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    public void RequireDevelopment_Throws_OutsideDevelopment(string environmentName)
    {
        Assert.Throws<InvalidOperationException>(
            () => DevelopmentAccountSeeder.RequireDevelopment(EnvironmentNamed(environmentName)));
    }

    [Test]
    public void RequireDevelopment_Allows_Development()
    {
        Assert.DoesNotThrow(
            () => DevelopmentAccountSeeder.RequireDevelopment(EnvironmentNamed(Environments.Development)));
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    public void SeedAsync_Throws_OutsideDevelopment_BeforeTouchingTheDatabase(string environmentName)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();

        Assert.ThrowsAsync<InvalidOperationException>(
            () => DevelopmentAccountSeeder.SeedAsync(
                context, new ThrowingHasher(), "any-dev-password", EnvironmentNamed(environmentName)));
    }

    [Test]
    public void SeedAsync_RejectsBlankPassword_BeforeTouchingTheDatabase()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();

        Assert.ThrowsAsync<ArgumentException>(
            () => DevelopmentAccountSeeder.SeedAsync(
                context, new ThrowingHasher(), "   ", EnvironmentNamed(Environments.Development)));
    }

    [TestCase(DevelopmentAccountSeeder.PlaceholderPassword)]
    [TestCase("change_me_anything")]
    public void SeedAsync_RejectsPlaceholderPassword_BeforeTouchingTheDatabase(string password)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();

        var exception = Assert.ThrowsAsync<ArgumentException>(
            () => DevelopmentAccountSeeder.SeedAsync(
                context, new ThrowingHasher(), password, EnvironmentNamed(Environments.Development)));

        Assert.That(exception!.ParamName, Is.EqualTo("password"));
    }

    [Test]
    public void SeedAsync_RejectsPasswordThatLoginWouldRefuse()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();
        var password = new string('x', CredentialVerifier.MaxPasswordLength + 1);

        Assert.ThrowsAsync<ArgumentException>(
            () => DevelopmentAccountSeeder.SeedAsync(
                context, new ThrowingHasher(), password, EnvironmentNamed(Environments.Development)));
    }

    private static IHostEnvironment EnvironmentNamed(string name) =>
        WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = name }).Environment;

    private static PlatformIdentityDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlatformIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlatformIdentityDbContext(options);
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{IdentityModule.ConnectionStringName}"] = "Host=localhost;Database=model_only"
            })
            .Build();

        return new ServiceCollection().AddIdentityModule(configuration).BuildServiceProvider();
    }

    private sealed class ThrowingHasher : IPasswordHasher
    {
        public string Hash(string password) => throw new InvalidOperationException("Hasher must not be called.");

        public bool Verify(string password, string? passwordHash) =>
            throw new InvalidOperationException("Hasher must not be called.");
    }
}
