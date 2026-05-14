using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Jira;
using RepoManager.Application.Reconciliation;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Reconciliation;

public class ReleaseReconciliationService : IReleaseReconciliationService
{
    private readonly AppDbContext _db;
    private readonly IJiraService _jira;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ReleaseReconciliationService(AppDbContext db, IJiraService jira)
    {
        _db = db;
        _jira = jira;
    }

    public async Task<ReconciliationResultDto> ReconcileAsync(Guid releaseId, CancellationToken ct = default)
    {
        var release = await _db.Releases
            .Include(r => r.RepositoryTags)
            .FirstOrDefaultAsync(r => r.Id == releaseId, ct)
            ?? throw new NotFoundException("Release", releaseId);

        var project = await _db.Projects.FindAsync([release.ProjectId], ct)
            ?? throw new NotFoundException("Project", release.ProjectId);

        if (project.JiraConnectionId is null)
            throw new ConflictException("Jira is not configured for this project");

        var connectionId = project.JiraConnectionId.Value;
        var projectKeys = JsonSerializer.Deserialize<List<string>>(project.JiraProjectKeys) ?? [];
        var versionName = BuildVersionName(project.FixVersionPattern, release.Version);

        // Sync fix versions for all project keys and collect Jira tickets
        var allJiraTicketKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var jiraTicketDetails = new Dictionary<string, JiraTicketDto>(StringComparer.OrdinalIgnoreCase);
        JiraRelease? primaryJiraReleaseEntity = null;

        foreach (var projectKey in projectKeys)
        {
            var jiraRelease = await _jira.SyncFixVersionAsync(
                connectionId, projectKey, versionName, project.AutoCreateFixVersion, ct);

            var jiraReleaseEntity = await UpsertJiraReleaseAsync(connectionId, project.Id, projectKey, jiraRelease, ct);
            primaryJiraReleaseEntity ??= jiraReleaseEntity;

            foreach (var ticket in jiraRelease.Tickets)
            {
                allJiraTicketKeys.Add(ticket.Key);
                jiraTicketDetails[ticket.Key] = ticket;
            }
        }

        if (primaryJiraReleaseEntity is null)
            throw new ConflictException("No Jira project keys are configured for this project");

        // Load Git ticket IDs from commits in the release range
        var repoIds = release.RepositoryTags.Select(rt => rt.RepositoryId).ToHashSet();
        var gitRawIds = await _db.Commits
            .Where(c => repoIds.Contains(c.RepositoryId) && c.JiraTicketId != null)
            .Select(c => c.JiraTicketId!)
            .Distinct()
            .ToListAsync(ct);

        var gitTicketIds = new HashSet<string>(gitRawIds, StringComparer.OrdinalIgnoreCase);

        // Resolve subtask parents when configured
        if (project.MatchSubtasksToParents)
            gitTicketIds = await ResolveSubtaskParentsAsync(gitTicketIds, ct);

        // Commit counts per ticket for GitOnly buckets
        var commitCountsByTicket = await _db.Commits
            .Where(c => repoIds.Contains(c.RepositoryId) && c.JiraTicketId != null)
            .GroupBy(c => c.JiraTicketId!)
            .Select(g => new { TicketId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TicketId, x => x.Count, ct);

        // Compute set diff
        var matched = gitTicketIds.Intersect(allJiraTicketKeys, StringComparer.OrdinalIgnoreCase).ToList();
        var jiraOnly = allJiraTicketKeys.Except(gitTicketIds, StringComparer.OrdinalIgnoreCase).ToList();
        var gitOnly = gitTicketIds.Except(allJiraTicketKeys, StringComparer.OrdinalIgnoreCase).ToList();

        var total = matched.Count + jiraOnly.Count + gitOnly.Count;
        var matchRate = total == 0 ? 100m : Math.Round((decimal)matched.Count / total * 100, 2);

        // Build ticket lookups for Tickets table (Git side)
        var gitTicketTitles = await _db.Tickets
            .Where(t => repoIds.Contains(t.RepositoryId) && gitTicketIds.Contains(t.TicketId))
            .GroupBy(t => t.TicketId)
            .Select(g => new { TicketId = g.Key, Title = g.First().Title })
            .ToDictionaryAsync(x => x.TicketId, x => x.Title, ct);

        var result = new ReconciliationResultDto(
            releaseId,
            DateTimeOffset.UtcNow,
            matched.Count,
            jiraOnly.Count,
            gitOnly.Count,
            matchRate,
            matched.Select(k => new MatchedTicketDto(k,
                jiraTicketDetails.TryGetValue(k, out var jt) ? jt.Summary : string.Empty,
                jiraTicketDetails.TryGetValue(k, out var jt2) ? jt2.Status : string.Empty)).ToList(),
            jiraOnly.Select(k => jiraTicketDetails.TryGetValue(k, out var jt) ? jt : new JiraTicketDto(k, string.Empty, string.Empty, default, string.Empty, null, null, null, null)).ToList(),
            gitOnly.Select(k => new GitTicketDto(k, gitTicketTitles.TryGetValue(k, out var t) ? t : null, commitCountsByTicket.GetValueOrDefault(k, 0))).ToList());

        await PersistAsync(release.Id, primaryJiraReleaseEntity.Id, result, ct);

        return result;
    }

    public async Task<ReconciliationResultDto?> GetLatestAsync(Guid releaseId, CancellationToken ct = default)
    {
        var row = await _db.ReleaseReconciliations
            .FirstOrDefaultAsync(r => r.ReleaseId == releaseId, ct);

        if (row is null) return null;

        return JsonSerializer.Deserialize<ReconciliationResultDto>(row.Snapshot, JsonOptions);
    }

    public async Task AddGitTicketsToJiraAsync(Guid releaseId, IReadOnlyList<string> ticketKeys, CancellationToken ct = default)
    {
        var row = await _db.ReleaseReconciliations
            .FirstOrDefaultAsync(r => r.ReleaseId == releaseId, ct)
            ?? throw new NotFoundException("ReleaseReconciliation", releaseId);

        var jiraRelease = await _db.JiraReleases.FindAsync([row.JiraReleaseId], ct)
            ?? throw new NotFoundException("JiraRelease", row.JiraReleaseId);

        foreach (var key in ticketKeys)
            await _jira.AddTicketToFixVersionAsync(jiraRelease.JiraConnectionId, key, jiraRelease.JiraVersionId, ct);
    }

    private async Task<JiraRelease> UpsertJiraReleaseAsync(
        Guid connectionId, Guid projectId, string projectKey, JiraReleaseDto dto, CancellationToken ct)
    {
        var existing = await _db.JiraReleases
            .Include(r => r.JiraTickets)
            .FirstOrDefaultAsync(r => r.JiraConnectionId == connectionId && r.JiraVersionId == dto.JiraVersionId, ct);

        if (existing is null)
        {
            existing = new JiraRelease
            {
                Id = Guid.NewGuid(),
                JiraConnectionId = connectionId,
                ProjectId = projectId,
                JiraProjectKey = projectKey,
                JiraVersionId = dto.JiraVersionId,
                Name = dto.Name,
                IsReleased = dto.IsReleased,
                ReleaseDate = dto.ReleaseDate,
                LastSyncedAt = DateTimeOffset.UtcNow
            };
            _db.JiraReleases.Add(existing);
        }
        else
        {
            existing.Name = dto.Name;
            existing.IsReleased = dto.IsReleased;
            existing.ReleaseDate = dto.ReleaseDate;
            existing.LastSyncedAt = DateTimeOffset.UtcNow;
            _db.JiraTickets.RemoveRange(existing.JiraTickets);
        }

        foreach (var ticket in dto.Tickets)
        {
            _db.JiraTickets.Add(new JiraTicket
            {
                Id = Guid.NewGuid(),
                JiraReleaseId = existing.Id,
                Key = ticket.Key,
                Summary = ticket.Summary,
                Status = ticket.Status,
                StatusCategory = ticket.StatusCategory,
                IssueType = ticket.IssueType,
                AssigneeName = ticket.AssigneeName,
                AssigneeEmail = ticket.AssigneeEmail,
                Priority = ticket.Priority,
                ParentKey = ticket.ParentKey,
                LastSyncedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    private async Task<HashSet<string>> ResolveSubtaskParentsAsync(HashSet<string> gitTicketIds, CancellationToken ct)
    {
        var resolved = new HashSet<string>(gitTicketIds, StringComparer.OrdinalIgnoreCase);

        var parents = await _db.JiraTickets
            .Where(t => gitTicketIds.Contains(t.Key) && t.ParentKey != null)
            .Select(t => t.ParentKey!)
            .Distinct()
            .ToListAsync(ct);

        foreach (var parent in parents)
            resolved.Add(parent);

        return resolved;
    }

    private async Task PersistAsync(Guid releaseId, Guid jiraReleaseId, ReconciliationResultDto result, CancellationToken ct)
    {
        var snapshot = JsonSerializer.Serialize(result, JsonOptions);

        var existing = await _db.ReleaseReconciliations
            .FirstOrDefaultAsync(r => r.ReleaseId == releaseId, ct);

        if (existing is null)
        {
            _db.ReleaseReconciliations.Add(new ReleaseReconciliation
            {
                Id = Guid.NewGuid(),
                ReleaseId = releaseId,
                JiraReleaseId = jiraReleaseId,
                RunAt = result.RunAt,
                MatchedCount = result.MatchedCount,
                JiraOnlyCount = result.JiraOnlyCount,
                GitOnlyCount = result.GitOnlyCount,
                MatchRatePercent = result.MatchRatePercent,
                Snapshot = snapshot
            });
        }
        else
        {
            existing.JiraReleaseId = jiraReleaseId;
            existing.RunAt = result.RunAt;
            existing.MatchedCount = result.MatchedCount;
            existing.JiraOnlyCount = result.JiraOnlyCount;
            existing.GitOnlyCount = result.GitOnlyCount;
            existing.MatchRatePercent = result.MatchRatePercent;
            existing.Snapshot = snapshot;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string BuildVersionName(string? pattern, string version) =>
        string.IsNullOrEmpty(pattern) ? version : pattern.Replace("{version}", version);
}
