using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using RepoManager.Application.Auth;
using RepoManager.Application.Commits;
using RepoManager.Application.Confluence;
using RepoManager.Application.Events;
using RepoManager.Application.GitProviders;
using RepoManager.Application.Jira;
using RepoManager.Application.Projects;
using RepoManager.Application.Queues;
using RepoManager.Application.Reconciliation;
using RepoManager.Application.Releases;
using RepoManager.Application.Repositories;
using RepoManager.Application.Services;
using RepoManager.Application.Maintenance;
using RepoManager.Application.Templates;
using RepoManager.Application.Validators;
using RepoManager.Infrastructure.Auth;
using RepoManager.Infrastructure.Maintenance;
using RepoManager.Infrastructure.Commits;
using RepoManager.Infrastructure.Releases;
using RepoManager.Infrastructure.Confluence;
using RepoManager.Infrastructure.GitProviders;
using RepoManager.Infrastructure.Jira;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Projects;
using RepoManager.Infrastructure.Reconciliation;
using RepoManager.Infrastructure.Repositories;
using RepoManager.Infrastructure.Sync;
using RepoManager.Infrastructure.Templates;
using RepoManager.Infrastructure.Services.Handlebars;

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
        services.AddScoped<IGitProviderService, GitProviderService>();
        services.AddScoped<IRepositoryService, RepositoryService>();
        services.AddScoped<IValidator<SetLatestTagDto>, SetLatestTagDtoValidator>();
        services.AddScoped<IValidator<CreateReleaseRequest>, CreateReleaseRequestValidator>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddSingleton<IConventionalCommitParser, ConventionalCommitParser>();
        services.AddScoped<CommitSyncService>();

        services.AddTransient<JiraResilienceHandler>();
        services.AddHttpClient<IJiraService, JiraService>()
            .AddHttpMessageHandler<JiraResilienceHandler>();
        services.AddScoped<IJiraConnectionService, JiraConnectionService>();
        services.AddScoped<IRepoJiraComparisonService, RepoJiraComparisonService>();

        services.AddHttpClient<IConfluencePublisher, ConfluencePublisher>();
        services.AddScoped<IConfluenceConnectionService, ConfluenceConnectionService>();

        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IVersionBumpService, VersionBumpService>();
        services.AddScoped<IReleaseCompositionService, ReleaseCompositionService>();
        services.AddScoped<IReleaseNoteTemplateService, ReleaseNoteTemplateService>();
        services.AddScoped<IReleaseReconciliationService, ReleaseReconciliationService>();

        services.AddMemoryCache();
        services.AddSingleton<ISyncJobQueue, InMemorySyncJobQueue>();
        services.AddSingleton<ISyncEventPublisher, InMemorySyncEventPublisher>();
        services.AddSingleton<IProjectSyncEventPublisher, InMemoryProjectSyncEventPublisher>();
        services.AddSingleton<ProjectSyncCancellationRegistry>();
        services.AddScoped<IRepositorySyncService, RepositorySyncService>();
        services.AddScoped<IProjectSyncService, ProjectSyncService>();
        services.AddScoped<IProjectSyncSnapshotService, ProjectSyncSnapshotService>();

        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();

        services.AddSingleton<MissingTokenRecorder>();
        services.AddSingleton<HandlebarsDotNet.IHandlebars>(sp =>
            HandlebarsFactory.Create(sp.GetRequiredService<MissingTokenRecorder>()));

        services.AddScoped<IValidator<Application.DTOs.Bindings.CreateBindingRequest>, CreateBindingRequestValidator>();
        services.AddScoped<IValidator<Application.DTOs.Bindings.UpdateBindingRequest>, UpdateBindingRequestValidator>();
        services.AddScoped<ProjectCustomVariableUpsertValidator>();
        services.AddScoped<IProjectTemplateBindingService, ProjectTemplateBindingService>();
        services.AddScoped<IProjectCustomVariableService, ProjectCustomVariableService>();
        services.AddScoped<IReleaseRenderService, ReleaseRenderService>();

        return services;
    }
}
