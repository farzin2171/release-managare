using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.Auth;
using RepoManager.Infrastructure.Auth;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=./backend/data/repomanager.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString)
                   .AddInterceptors(new SqlitePragmaInterceptor()));

        services.AddDataProtection();

        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
