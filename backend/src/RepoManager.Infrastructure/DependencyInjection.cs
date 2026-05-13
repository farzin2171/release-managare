using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.Auth;
using RepoManager.Application.GitProviders;
using RepoManager.Application.Jira;
using RepoManager.Application.Projects;
using RepoManager.Application.Repositories;
using RepoManager.Infrastructure.Auth;
using RepoManager.Infrastructure.GitProviders;
using RepoManager.Infrastructure.Jira;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Projects;
using RepoManager.Infrastructure.Repositories;

namespace RepoManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=../../data/repomanager.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString)
                   .AddInterceptors(new SqlitePragmaInterceptor()));

        services.AddDataProtection();

        services.AddScoped<IAuthService, AuthService>();

        services.AddSingleton<AzureDevOpsGitProvider>();
        services.AddSingleton<IGitProviderFactory, GitProviderFactory>();
        services.AddScoped<IGitProviderConnectionService, GitProviderConnectionService>();
        services.AddScoped<IRepositoryService, RepositoryService>();
        services.AddScoped<IProjectService, ProjectService>();

        services.AddTransient<JiraResilienceHandler>();
        services.AddHttpClient<IJiraService, JiraService>()
            .AddHttpMessageHandler<JiraResilienceHandler>();
        services.AddScoped<IJiraConnectionService, JiraConnectionService>();

        return services;
    }
}
