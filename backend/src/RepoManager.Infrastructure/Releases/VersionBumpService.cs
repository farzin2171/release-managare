using System.Diagnostics;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Releases;
using RepoManager.Domain.ValueObjects;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Releases;

public class VersionBumpService : IVersionBumpService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VersionBumpService> _logger;

    public VersionBumpService(AppDbContext db, ILogger<VersionBumpService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Constructor used by unit tests (no logger required)
    internal VersionBumpService(AppDbContext db)
        : this(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<VersionBumpService>.Instance)
    {
    }

    public async Task<VersionBumpSuggestionDto> SuggestAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var repo = await _db.Repositories.FindAsync([repositoryId], ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        var hasTag = !string.IsNullOrEmpty(repo.LatestTag);
        SemVer? previousSemVer = null;

        if (hasTag)
        {
            if (!SemVer.TryParse(repo.LatestTag!, out previousSemVer))
                throw new ValidationException([
                    new ValidationFailure("LatestTag", $"Repository '{repo.Name}' has a non-semver tag '{repo.LatestTag}'.")
                    {
                        ErrorCode = "non_semver_tag"
                    }
                ]);
        }

        // Find commits since the latest tag (commits after the tag commit by timestamp)
        var commits = await GetCommitsSinceTagAsync(repositoryId, repo.LatestTagCommitSha, ct);

        var toCommitSha = await _db.Commits
            .Where(c => c.RepositoryId == repositoryId)
            .OrderByDescending(c => c.CommittedAt)
            .Select(c => c.Sha)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        string bumpType;
        string suggestedNextVersion;

        if (!hasTag || previousSemVer is null)
        {
            // No semver tag: suggest 0.1.0 regardless of commit content
            bumpType = "minor";
            suggestedNextVersion = "0.1.0";

            var ticketCount = commits
                .Where(c => c.JiraTicketId is not null)
                .Select(c => c.JiraTicketId!)
                .Distinct()
                .Count();

            sw.Stop();
            _logger.LogDebug(
                "VersionBump repoId={RepositoryId} bumpType={BumpType} elapsed={ElapsedMs}ms outcome=success",
                repositoryId, bumpType, sw.ElapsedMilliseconds);

            return new VersionBumpSuggestionDto(
                PreviousVersion: string.Empty,
                SuggestedNextVersion: suggestedNextVersion,
                BumpType: bumpType,
                FromCommitSha: string.Empty,
                ToCommitSha: toCommitSha,
                CommitCount: commits.Count,
                TicketCount: ticketCount);
        }

        // Apply bump-type rules in priority order: breaking > feat > fix
        if (commits.Any(c => c.IsBreaking))
            bumpType = "major";
        else if (commits.Any(c => c.Type == "feat"))
            bumpType = "minor";
        else
            bumpType = "patch";

        suggestedNextVersion = bumpType switch
        {
            "major" => previousSemVer.NextMajor().ToString(),
            "minor" => previousSemVer.NextMinor().ToString(),
            _ => previousSemVer.NextPatch().ToString()
        };

        var uniqueTickets = commits
            .Where(c => c.JiraTicketId is not null)
            .Select(c => c.JiraTicketId!)
            .Distinct()
            .Count();

        sw.Stop();
        _logger.LogDebug(
            "VersionBump repoId={RepositoryId} bumpType={BumpType} elapsed={ElapsedMs}ms outcome=success",
            repositoryId, bumpType, sw.ElapsedMilliseconds);

        return new VersionBumpSuggestionDto(
            PreviousVersion: repo.LatestTag!,
            SuggestedNextVersion: suggestedNextVersion,
            BumpType: bumpType,
            FromCommitSha: repo.LatestTagCommitSha ?? string.Empty,
            ToCommitSha: toCommitSha,
            CommitCount: commits.Count,
            TicketCount: uniqueTickets);
    }

    private async Task<List<Domain.Entities.Commit>> GetCommitsSinceTagAsync(
        Guid repositoryId, string? tagCommitSha, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tagCommitSha))
            return await _db.Commits
                .Where(c => c.RepositoryId == repositoryId)
                .ToListAsync(ct);

        // Find the tag commit to get its timestamp, then return all commits after it
        var tagCommit = await _db.Commits
            .Where(c => c.RepositoryId == repositoryId && c.Sha == tagCommitSha)
            .FirstOrDefaultAsync(ct);

        if (tagCommit is null)
            return await _db.Commits
                .Where(c => c.RepositoryId == repositoryId)
                .ToListAsync(ct);

        return await _db.Commits
            .Where(c => c.RepositoryId == repositoryId && c.CommittedAt > tagCommit.CommittedAt)
            .ToListAsync(ct);
    }
}
