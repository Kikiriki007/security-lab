using Platform.Identity.Domain;
using Platform.Identity.Services;

namespace Platform.Tests.Identity;

[TestFixture]
[Category("US-04")]
public class PasswordAndCredentialTests
{
    private const string Password = "correct horse battery staple";

    // Passwords the hasher and verifier must handle the same way: ASCII, Cyrillic, mixed scripts, special characters.
    private static readonly string[] Passwords =
    [
        Password,
        "бурек је најјачи искрено",
        "thisIsAnotherPasswordTestWohooo",
        "Pisi kako читаш citaj kako пишеш",
        "p@$$w0rd!\"'<>&%\\"
    ];

    private readonly BCryptPasswordHasher _hasher = new();

    [TestCaseSource(nameof(Passwords))]
    public void Hash_IsBCrypt_NotPlaintext_AndSalted(string password)
    {
        var hash = _hasher.Hash(password);

        Assert.That(hash, Does.StartWith("$2"));
        Assert.That(hash, Does.Not.Contain(password));
        Assert.That(_hasher.Hash(password), Is.Not.EqualTo(hash));
    }

    [TestCaseSource(nameof(Passwords))]
    public void Verify_AcceptsCorrectPassword_AndRejectsWrongOne(string password)
    {
        var hash = _hasher.Hash(password);

        Assert.That(_hasher.Verify(password, hash), Is.True);
        Assert.That(_hasher.Verify("wrong password", hash), Is.False);
    }

    [Test]
    public void Verify_NeverThrows_ForMissingOrMalformedHash()
    {
        Assert.That(_hasher.Verify(Password, null), Is.False);
        Assert.That(_hasher.Verify(Password, "not-a-bcrypt-hash"), Is.False);
    }

    [Test]
    public void Verify_DoesNotTruncateLongPasswords()
    {
        var longPassword = new string('a', 100);
        var hash = _hasher.Hash(longPassword);

        Assert.That(_hasher.Verify(longPassword, hash), Is.True);
        Assert.That(_hasher.Verify(new string('a', 99) + "b", hash), Is.False);
    }

    [TestCaseSource(nameof(Passwords))]
    public async Task Verifier_AcceptsCorrectCredentials_WithEmailNormalization(string password)
    {
        var id = Guid.NewGuid();
        var verifier = VerifierWith(new CredentialRecord(id, AccountStatus.Active, _hasher.Hash(password)));

        var result = await verifier.VerifyAsync("  Student@Example.COM ", password);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.AccountId, Is.EqualTo(id));
        Assert.That(result.Status, Is.EqualTo(AccountStatus.Active));
    }

    [TestCaseSource(nameof(Passwords))]
    public async Task Verifier_ReturnsStatus_ForBlockedAccount_SoTheCallerCanRefuse(string password)
    {
        var verifier = VerifierWith(new CredentialRecord(Guid.NewGuid(), AccountStatus.Blocked, _hasher.Hash(password)));

        var result = await verifier.VerifyAsync("student@example.com", password);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Status, Is.EqualTo(AccountStatus.Blocked));
    }

    [Test]
    public async Task Verifier_RejectsWrongPassword_UnknownEmail_AndAccountWithoutHash()
    {
        var withHash = VerifierWith(new CredentialRecord(Guid.NewGuid(), AccountStatus.Active, _hasher.Hash(Password)));
        var withoutHash = VerifierWith(new CredentialRecord(Guid.NewGuid(), AccountStatus.Active, null));
        var empty = VerifierWith(null);

        Assert.That((await withHash.VerifyAsync("student@example.com", "wrong")).IsValid, Is.False);
        Assert.That((await withoutHash.VerifyAsync("student@example.com", Password)).IsValid, Is.False);
        Assert.That((await empty.VerifyAsync("nobody@example.com", Password)).IsValid, Is.False);
    }

    private CredentialVerifier VerifierWith(CredentialRecord? record) => new(new FakeStore(record), _hasher);

    private sealed class FakeStore(CredentialRecord? record) : ICredentialStore
    {
        public Task<CredentialRecord?> FindByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
            Task.FromResult(normalizedEmail == "student@example.com" ? record : null);
    }
}
