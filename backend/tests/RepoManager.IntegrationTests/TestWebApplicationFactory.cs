using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TestWebApplicationFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "integration-test-secret-key-that-is-at-least-32-characters",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["DataProtection:Key"] = "test-data-protection-key-32chars!",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the registered AppDbContext
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            // Also remove any DbContext-related registrations
            var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
            if (contextDescriptor is not null)
                services.Remove(contextDescriptor);

            // Register with the shared in-memory connection so all test calls share the same DB
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            // Create the schema now (ConfigureServices runs before background services start).
            // Avoid BuildServiceProvider() here — it triggers Serilog's ReloadableLogger.Freeze()
            // prematurely, causing a double-freeze error when the host later resolves ILoggerFactory.
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            using var db = new AppDbContext(dbOptions);
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
