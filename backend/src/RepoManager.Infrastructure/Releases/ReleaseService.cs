using HandlebarsDotNet;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Releases;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Releases;

public class ReleaseService : IReleaseService
{
    private readonly AppDbContext _db;

    public ReleaseService(AppDbContext db) => _db = db;

    public async Task<ReleaseDto> CreateAsync(Guid projectId, CreateReleaseDto dto, Guid createdByUserId, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("Project", projectId);

        var template = await _db.ReleaseNoteTemplates.FindAsync([dto.TemplateId], ct)
            ?? throw new NotFoundException("ReleaseNoteTemplate", dto.TemplateId);

        var allTickets = new List<Ticket>();
        foreach (var rt in dto.RepositoryTags)
        {
            var tickets = await _db.Tickets
                .Where(t => t.RepositoryId == rt.RepositoryId
                         && t.FromTag == rt.FromTag
                         && t.ToTag == rt.ToTag)
                .ToListAsync(ct);
            allTickets.AddRange(tickets);
        }

        var suggestedVersion = ComputeSuggestedVersion(allTickets, dto.RepositoryTags);

        var breaking = allTickets.Where(t => t.IsBreaking).ToList();
        var notBreaking = allTickets.Where(t => !t.IsBreaking).ToList();
        var features = notBreaking.Where(t => t.PrimaryType == "feat").ToList();
        var fixes = notBreaking.Where(t => t.PrimaryType == "fix").ToList();
        var other = notBreaking.Where(t => t.PrimaryType != "feat" && t.PrimaryType != "fix").ToList();

        var ticketIds = allTickets.Select(t => t.TicketId).Distinct().ToList();
        var repoIds = dto.RepositoryTags.Select(rt => rt.RepositoryId).ToList();

        var contributors = await _db.Commits
            .Where(c => repoIds.Contains(c.RepositoryId)
                     && c.JiraTicketId != null
                     && ticketIds.Contains(c.JiraTicketId))
            .Select(c => c.AuthorName)
            .Distinct()
            .ToListAsync(ct);

        var commitCounts = await _db.Commits
            .Where(c => repoIds.Contains(c.RepositoryId))
            .GroupBy(c => c.RepositoryId)
            .Select(g => new { RepositoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RepositoryId, x => x.Count, ct);

        var repoNames = await _db.Repositories
            .Where(r => repoIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var project = await _db.Projects.FindAsync([projectId], ct)!;
        var context = new
        {
            project = new { id = project!.Id, name = project.Name, description = project.Description },
            version = dto.Version,
            sections = new
            {
                breaking = breaking.Select(ToTicketContext).ToList(),
                features = features.Select(ToTicketContext).ToList(),
                fixes = fixes.Select(ToTicketContext).ToList(),
                other = other.Select(ToTicketContext).ToList()
            },
            contributors,
            repositories = dto.RepositoryTags.Select(rt => new
            {
                name = repoNames.GetValueOrDefault(rt.RepositoryId, rt.RepositoryId.ToString()),
                fromTag = rt.FromTag,
                toTag = rt.ToTag,
                commitCount = commitCounts.GetValueOrDefault(rt.RepositoryId, 0)
            }).ToList()
        };

        var compiled = Handlebars.Compile(template.ContentTemplate);
        var generatedMarkdown = compiled(context);

        var release = new Release
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Version = dto.Version,
            Status = ReleaseStatus.Draft,
            GeneratedNotesMarkdown = generatedMarkdown,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Releases.Add(release);

        foreach (var rt in dto.RepositoryTags)
        {
            _db.ReleaseRepositoryTags.Add(new ReleaseRepositoryTag
            {
                ReleaseId = release.Id,
                RepositoryId = rt.RepositoryId,
                FromTag = rt.FromTag,
                ToTag = rt.ToTag,
                CommitCount = commitCounts.GetValueOrDefault(rt.RepositoryId, 0)
            });
        }

        await _db.SaveChangesAsync(ct);

        release = await _db.Releases
            .Include(r => r.RepositoryTags).ThenInclude(rt => rt.Repository)
            .FirstAsync(r => r.Id == release.Id, ct);

        return ToDto(release, suggestedVersion);
    }

    public async Task<ReleaseDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var release = await _db.Releases
            .Include(r => r.RepositoryTags).ThenInclude(rt => rt.Repository)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Release", id);

        return ToDto(release, string.Empty);
    }

    public async Task<ReleaseDto> UpdateNotesAsync(Guid id, UpdateNotesDto dto, CancellationToken ct = default)
    {
        var release = await _db.Releases
            .Include(r => r.RepositoryTags).ThenInclude(rt => rt.Repository)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Release", id);

        if (release.Status == ReleaseStatus.Published)
            throw new ConflictException("Release is published and locked");

        release.EditedNotesMarkdown = dto.EditedNotesMarkdown;
        await _db.SaveChangesAsync(ct);
        return ToDto(release, string.Empty);
    }

    public Task<ReleaseDto> PublishAsync(Guid id, CancellationToken ct = default) =>
        throw new NotImplementedException("PublishAsync is implemented in T072.");

    public async Task<IReadOnlyList<ReleaseDto>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var releases = await _db.Releases
            .Where(r => r.ProjectId == projectId)
            .Include(r => r.RepositoryTags).ThenInclude(rt => rt.Repository)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return releases.Select(r => ToDto(r, string.Empty)).ToList();
    }

    private static string ComputeSuggestedVersion(List<Ticket> tickets, IReadOnlyList<RepositoryTagRangeDto> repositoryTags)
    {
        var baseTag = repositoryTags.FirstOrDefault()?.FromTag ?? string.Empty;
        var hasPrefix = baseTag.StartsWith('v');
        var versionStr = hasPrefix ? baseTag[1..] : baseTag;

        if (!System.Version.TryParse(versionStr, out var v))
            return baseTag;

        var prefix = hasPrefix ? "v" : string.Empty;
        var minor = v.Minor >= 0 ? v.Minor : 0;
        var build = v.Build >= 0 ? v.Build : 0;

        if (tickets.Any(t => t.IsBreaking))
            return $"{prefix}{v.Major + 1}.0.0";
        if (tickets.Any(t => t.PrimaryType == "feat"))
            return $"{prefix}{v.Major}.{minor + 1}.0";
        return $"{prefix}{v.Major}.{minor}.{build + 1}";
    }

    private static object ToTicketContext(Ticket t) => new
    {
        ticketId = t.TicketId,
        title = t.Title,
        primaryType = t.PrimaryType,
        isBreaking = t.IsBreaking,
        commitCount = t.CommitCount,
        contributorCount = t.ContributorCount
    };

    private static ReleaseDto ToDto(Release r, string suggestedVersion)
    {
        var tags = r.RepositoryTags.Select(rt => new ReleaseRepositoryTagDto(
            rt.RepositoryId,
            rt.Repository?.Name ?? string.Empty,
            rt.FromTag,
            rt.ToTag,
            rt.CommitCount)).ToList();

        return new ReleaseDto(
            r.Id, r.ProjectId, r.Version,
            r.Status.ToString(),
            r.GeneratedNotesMarkdown, r.EditedNotesMarkdown,
            r.ConfluencePageId, r.ConfluencePageUrl,
            r.CreatedAt, r.PublishedAt,
            suggestedVersion, tags);
    }
}
