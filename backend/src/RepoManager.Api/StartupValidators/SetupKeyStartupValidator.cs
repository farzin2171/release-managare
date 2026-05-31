using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Api.StartupValidators;

public sealed class SetupKeyStartupValidator : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SetupKeyStartupValidator> _logger;

    public SetupKeyStartupValidator(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<SetupKeyStartupValidator> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var key = _configuration["RELEASE_MANAGER_SETUP_KEY"];
        if (!string.IsNullOrEmpty(key))
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
        if (db is null)
            return;

        var hasUsers = await db.Users.AnyAsync(ct);
        if (!hasUsers)
        {
            _logger.LogCritical("RELEASE_MANAGER_SETUP_KEY must be set before first run.");
            _lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
