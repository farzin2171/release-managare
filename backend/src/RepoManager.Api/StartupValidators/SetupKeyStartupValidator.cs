using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RepoManager.Api.StartupValidators;

public sealed class SetupKeyStartupValidator : IHostedService
{
    public SetupKeyStartupValidator(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<SetupKeyStartupValidator> logger)
    { }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
