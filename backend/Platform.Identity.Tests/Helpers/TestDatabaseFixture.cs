using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Identity.Persistence;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Platform.Identity.Tests.Helpers;

[SetUpFixture]
public class TestDatabaseFixture
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("test_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public static string ConnectionString { get; private set; } = string.Empty;
    private static Respawner _respawner = null!;
    private static NpgsqlConnection _connection = null!;
    
    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        // 1. Pokreće Docker kontejner pre bilo kog testa
        await _dbContainer.StartAsync();
        ConnectionString = _dbContainer.GetConnectionString();

        // 2. Pozivanje migracija za kreiranje tabela u bazi
        await ApplyMigrationsAsync();
        
        // 3. Inicijalizacija Respawn-a
        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions()
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        // 4. Gasi kontejner kad se svi testovi završe
        await _connection.DisposeAsync();
        await _dbContainer.StopAsync();
        await _dbContainer.DisposeAsync();
    }

    public static async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_connection);
    }

    private static async Task ApplyMigrationsAsync()
    {
        using var factory = new CustomWebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformIdentityDbContext>();

        // Primenjuje sve migracije iz API projekta na testnu bazu
        await db.Database.MigrateAsync();
    }
}