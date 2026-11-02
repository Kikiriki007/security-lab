using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Controllers.Identity;
using Platform.Api.Domain.Authentication;
using Platform.Api.Services.Identity;
using Platform.Identity.Domain;
using Platform.Identity.Services;

namespace Platform.Tests.Identity;

[TestFixture]
[Category("US-04")]
public class LoginTests
{
    private const string Email = "student@example.com";
    private const string Password = "S3cret-pass-for-tests";
    private const string Token = "opaque-session-token-value";

    // Passwords the login flow must handle the same way: ASCII, Cyrillic, mixed scripts, special characters.
    private static readonly string[] Passwords =
    [
        Password,
        "бурек је најјачи искрено",
        "thisIsAnotherPasswordTestWohooo",
        "Pisi kako читаш citaj kako пишеш",
        "p@$$w0rd!\"'<>&%\\"
    ];

    private FakeVerifier _verifier = null!;
    private FakeSessions _sessions = null!;
    private RecordingAudit _audit = null!;
    private LoginAccount _login = null!;

    [SetUp]
    public void SetUp()
    {
        _verifier = new FakeVerifier();
        _sessions = new FakeSessions();
        _audit = new RecordingAudit();
        _login = new LoginAccount(_verifier, _sessions, _audit, TimeProvider.System);
    }

    [TestCaseSource(nameof(Passwords))]
    public async Task ValidCredentials_ActiveAccount_CreatesActiveSession_AndAuditsSuccess(string password)
    {
        var id = Guid.NewGuid();
        _verifier.Result = new CredentialResult(true, id, AccountStatus.Active);

        var result = await _login.ExecuteAsync(Email, password, "corr-1");

        Assert.That(result.Outcome, Is.EqualTo(LoginOutcome.Success));
        Assert.That(result.UserId, Is.EqualTo(id));
        Assert.That(result.Session!.Token, Is.EqualTo(Token));
        Assert.That(_verifier.Calls, Is.EqualTo(1));
        Assert.That(_verifier.LastPassword, Is.EqualTo(password));
        Assert.That(_sessions.Calls, Is.EqualTo(1));
        Assert.That(_sessions.LastUser, Is.EqualTo(id));
        Assert.That(_sessions.LastLevel, Is.EqualTo(AuthenticationLevel.Active));
        var e = _audit.Events.Single();
        Assert.That(e.Outcome, Is.EqualTo("success"));
        Assert.That(e.ActorId, Is.EqualTo(id));
    }

    [Test]
    public async Task InvalidCredentials_NoSession_GenericOutcome_AuditedAsFailure()
    {
        var result = await _login.ExecuteAsync(Email, Password, "corr-2");

        Assert.That(result.Outcome, Is.EqualTo(LoginOutcome.InvalidCredentials));
        Assert.That(result.Session, Is.Null);
        Assert.That(_sessions.Calls, Is.EqualTo(0));
        Assert.That(_audit.Events.Single().Outcome, Is.EqualTo("failure"));
    }

    [TestCase(AccountStatus.Blocked)]
    [TestCase(AccountStatus.Archived)]
    public async Task BlockedOrArchived_WithCorrectPassword_IsRefused_LikeAWrongPassword(AccountStatus status)
    {
        var id = Guid.NewGuid();
        _verifier.Result = new CredentialResult(true, id, status);

        var result = await _login.ExecuteAsync(Email, Password, "corr-3");

        Assert.That(result.Outcome, Is.EqualTo(LoginOutcome.InvalidCredentials));
        Assert.That(result.Session, Is.Null);
        Assert.That(_sessions.Calls, Is.EqualTo(0));
        var e = _audit.Events.Single();
        Assert.That(e.Outcome, Is.EqualTo("denied"));
        Assert.That(e.ActorId, Is.EqualTo(id));
    }

    [TestCase(null, Password)]
    [TestCase("", Password)]
    [TestCase("   ", Password)]
    [TestCase(Email, null)]
    [TestCase(Email, "")]
    public async Task MissingInput_IsInvalidRequest_AndNothingIsChecked(string? email, string? password)
    {
        var result = await _login.ExecuteAsync(email, password, "corr-4");

        Assert.That(result.Outcome, Is.EqualTo(LoginOutcome.InvalidRequest));
        Assert.That(_verifier.Calls, Is.EqualTo(0));
        Assert.That(_sessions.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task OversizedEmail_IsInvalidRequest_AndNothingIsChecked()
    {
        var longEmail = new string('a', CredentialVerifier.MaxEmailLength) + "@example.com";

        var result = await _login.ExecuteAsync(longEmail, Password, "corr-5");

        Assert.That(result.Outcome, Is.EqualTo(LoginOutcome.InvalidRequest));
        Assert.That(_verifier.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task OversizedPassword_IsInvalidRequest_AndNothingIsChecked()
    {
        var longPassword = new string('p', CredentialVerifier.MaxPasswordLength + 1);

        var result = await _login.ExecuteAsync(Email, longPassword, "corr-6");

        Assert.That(result.Outcome, Is.EqualTo(LoginOutcome.InvalidRequest));
        Assert.That(_verifier.Calls, Is.EqualTo(0));
    }

    [Test]
    public async Task SessionStoreUnavailable_IsReported_AndNoSuccessIsAudited()
    {
        _verifier.Result = new CredentialResult(true, Guid.NewGuid(), AccountStatus.Active);
        _sessions.Next = null;

        var result = await _login.ExecuteAsync(Email, Password, "corr-7");

        Assert.That(result.Outcome, Is.EqualTo(LoginOutcome.Unavailable));
        Assert.That(_audit.Events.Single().Outcome, Is.EqualTo("failure"));
    }

    [TestCaseSource(nameof(Passwords))]
    public async Task AuditEvents_NeverContainEmailPasswordOrToken(string password)
    {
        _verifier.Result = new CredentialResult(true, Guid.NewGuid(), AccountStatus.Active);
        await _login.ExecuteAsync(Email, password, "corr-8");
        _verifier.Result = CredentialResult.Invalid;
        await _login.ExecuteAsync(Email, password, "corr-9");

        Assert.That(_audit.Events, Has.Count.EqualTo(2));
        foreach (var e in _audit.Events)
        {
            var text = string.Join('|', e.Action, e.Target, e.Outcome, e.CorrelationId, e.Detail);
            Assert.That(text, Does.Not.Contain(password).And.Not.Contain(Email).And.Not.Contain(Token));
        }
    }

    [TestCaseSource(nameof(Passwords))]
    public async Task Controller_Success_SetsHttpOnlySecureStrictCookie_AndNeverPutsTokenInBody(string password)
    {
        _verifier.Result = new CredentialResult(true, Guid.NewGuid(), AccountStatus.Active);
        var controller = ControllerWithHttpContext(out var http);

        var response = await controller.Login(new LoginRequest(Email, password), CancellationToken.None);

        Assert.That(response, Is.InstanceOf<OkObjectResult>());
        var ok = response as OkObjectResult;
        Assert.That(System.Text.Json.JsonSerializer.Serialize(ok!.Value), Does.Not.Contain(Token));
        Assert.That(http.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        AssertSessionCookieSet(http);
    }

    [Test]
    public async Task Controller_WrongCredentials_Returns401_AndNoCookie()
    {
        var controller = ControllerWithHttpContext(out var http);

        var response = await controller.Login(new LoginRequest(Email, Password), CancellationToken.None);

        Assert.That(response, Is.InstanceOf<UnauthorizedObjectResult>());
        AssertNoCookieSet(http);
    }

    [Test]
    public async Task Controller_MalformedRequest_Returns400_AndNoCookie()
    {
        var controller = ControllerWithHttpContext(out var http);

        var response = await controller.Login(new LoginRequest(null, null), CancellationToken.None);

        Assert.That(response, Is.InstanceOf<BadRequestObjectResult>());
        AssertNoCookieSet(http);
    }

    [Test]
    public async Task Controller_SessionStoreUnavailable_Returns503_AndNoCookie()
    {
        _verifier.Result = new CredentialResult(true, Guid.NewGuid(), AccountStatus.Active);
        _sessions.Next = null;
        var controller = ControllerWithHttpContext(out var http);

        var response = await controller.Login(new LoginRequest(Email, Password), CancellationToken.None);

        Assert.That(response, Is.InstanceOf<ObjectResult>());
        var unavailable = response as ObjectResult;
        Assert.That(unavailable!.StatusCode, Is.EqualTo(503));
        AssertNoCookieSet(http);
    }

    private static void AssertSessionCookieSet(DefaultHttpContext http)
    {
        var setCookie = http.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        Assert.That(setCookie, Does.Contain(SessionCookie.Name.ToLowerInvariant() + "=" + Token));
        Assert.That(setCookie, Does.Contain("httponly").And.Contain("secure").And.Contain("samesite=strict"));
    }

    private static void AssertNoCookieSet(DefaultHttpContext http) =>
        Assert.That(http.Response.Headers.ContainsKey("Set-Cookie"), Is.False);

    private LoginController ControllerWithHttpContext(out DefaultHttpContext http)
    {
        http = new DefaultHttpContext();
        return new LoginController(_login) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private sealed class FakeVerifier : ICredentialVerifier
    {
        public CredentialResult Result { get; set; } = CredentialResult.Invalid;
        public int Calls { get; private set; }
        public string? LastPassword { get; private set; }

        public Task<CredentialResult> VerifyAsync(string email, string password, CancellationToken ct = default)
        {
            Calls++;
            LastPassword = password;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeSessions : ISessionCreator
    {
        public IssuedSession? Next { get; set; } = new(Token, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(8));
        public int Calls { get; private set; }
        public Guid? LastUser { get; private set; }
        public AuthenticationLevel? LastLevel { get; private set; }

        public Task<IssuedSession?> CreateAsync(Guid userId, AuthenticationLevel level, CancellationToken ct = default)
        {
            Calls++;
            LastUser = userId;
            LastLevel = level;
            return Task.FromResult(Next);
        }
    }

    private sealed class RecordingAudit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];
        public void Write(AuditEvent e) => Events.Add(e);
    }
}
