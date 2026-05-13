using RepoManager.Domain.Enums;

namespace RepoManager.Application.GitProviders;

public interface IGitProviderFactory
{
    IGitProvider GetProvider(ProviderType providerType);
}
