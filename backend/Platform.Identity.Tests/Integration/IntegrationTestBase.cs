using Microsoft.Extensions.DependencyInjection;
using Platform.Identity.Persistence;
using Platform.Identity.Tests.Helpers;

namespace Platform.identity.Tests.Integration;

[TestFixture]
public class IntegrationTestBase
{
    protected static CustomWebApplicationFactory<Program> _factory = null!;
    protected static HttpClient _client = null!;
    
    [OneTimeSetUp]
    public void BaseClassSetup()
    {
        _factory = new CustomWebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void BaseClassTeardown()
    {
        _factory.Dispose();
        _client.Dispose();
    }

    [SetUp]
    public async Task BaseSetup()
    {
        await TestDatabaseFixture.ResetDatabaseAsync();
    }

    // Helper metoda za kraće ubacivanje u bazu iz testova
    protected async Task InsertAsync<T>(T entity) where T : class
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
    }
}