using RepoManager.Application.GitProviders;
using RepoManager.Domain.Enums;

namespace RepoManager.Infrastructure.GitProviders;

public class GitProviderFactory : IGitProviderFactory
{
    private readonly AzureDevOpsGitProvider _azureDevOps;

    public GitProviderFactory(AzureDevOpsGitProvider azureDevOps) => _azureDevOps = azureDevOps;

    public IGitProvider GetProvider(ProviderType providerType) => providerType switch
    {
        ProviderType.AzureDevOps => _azureDevOps,
        _ => throw new NotSupportedException($"Git provider '{providerType}' is not supported.")
    };
}
