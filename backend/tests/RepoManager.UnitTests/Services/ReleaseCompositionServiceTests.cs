using FluentAssertions;
using RepoManager.Application.Releases;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Releases;

namespace RepoManager.UnitTests.Services;

public class ReleaseCompositionServiceTests
{
    private static Project BuildProject(
        Guid? primaryRepoId,
        params (Guid Id, string Name)[] repos)
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Test Project" };

        foreach (var (id, name) in repos)
        {
            project.ProjectRepositories.Add(new ProjectRepository
            {
                ProjectId = project.Id,
                RepositoryId = id,
                IsPrimary = primaryRepoId.HasValue && id == primaryRepoId.Value,
                Repository = new Repository
                {
                    Id = id,
                    Name = name,
                    DefaultBranch = "main",
                    ExternalId = id.ToString(),
                    WebUrl = "https://example.com",
                    AzureProjectName = "proj"
                }
            });
        }

        return project;
    }

    [Fact]
    public void DeriveReleaseVersion_PrimaryRepoIncluded_UsesPrimaryNextVersion()
    {
        var repoA = Guid.NewGuid();
        var repoB = Guid.NewGuid();
        var project = BuildProject(primaryRepoId: repoA, (repoA, "alpha"), (repoB, "beta"));

        var selections = new[]
        {
            new ReleaseRepositorySelectionDto(repoA, "2.0.0", "major"),
            new ReleaseRepositorySelectionDto(repoB, "1.5.0", "minor")
        };

        var (version, repoId) = ReleaseCompositionService.DeriveReleaseVersion(selections, project);

        version.Should().Be("2.0.0");
        repoId.Should().Be(repoA);
    }

    [Fact]
    public void DeriveReleaseVersion_PrimaryRepoExcluded_FallsBackToAlphabeticallyFirst()
    {
        var primaryRepo = Guid.NewGuid();
        var repoA = Guid.NewGuid();
        var repoB = Guid.NewGuid();
        var project = BuildProject(primaryRepoId: primaryRepo,
            (primaryRepo, "zzz-primary"),
            (repoA, "gamma"),
            (repoB, "alpha"));

        // primary repo NOT included in selections
        var selections = new[]
        {
            new ReleaseRepositorySelectionDto(repoA, "3.0.0", "major"),
            new ReleaseRepositorySelectionDto(repoB, "1.0.0", "patch")
        };

        var (version, repoId) = ReleaseCompositionService.DeriveReleaseVersion(selections, project);

        // "alpha" < "gamma" alphabetically → repoB wins
        version.Should().Be("1.0.0");
        repoId.Should().Be(repoB);
    }

    [Fact]
    public void DeriveReleaseVersion_SingleRepoIncluded_UsesThatReposNextVersion()
    {
        var repoA = Guid.NewGuid();
        var repoB = Guid.NewGuid();
        var project = BuildProject(primaryRepoId: repoA, (repoA, "alpha"), (repoB, "beta"));

        // Only repoB selected (primary excluded)
        var selections = new[]
        {
            new ReleaseRepositorySelectionDto(repoB, "4.1.0", "minor")
        };

        var (version, repoId) = ReleaseCompositionService.DeriveReleaseVersion(selections, project);

        version.Should().Be("4.1.0");
        repoId.Should().Be(repoB);
    }
}
