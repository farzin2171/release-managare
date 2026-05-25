using FluentValidation.Results;
using HandlebarsDotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs.Releases;
using RepoManager.Application.Services;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Services.Handlebars;
using ValidationException = RepoManager.Application.Common.Exceptions.ValidationException;

namespace RepoManager.Infrastructure.Releases;

public class ReleaseRenderService : IReleaseRenderService
{
    private readonly AppDbContext _db;
    private readonly IHandlebars _hbs;
    private readonly MissingTokenRecorder _recorder;
    private readonly ILogger<ReleaseRenderService> _logger;

    public ReleaseRenderService(
        AppDbContext db,
        IHandlebars hbs,
        MissingTokenRecorder recorder,
        ILogger<ReleaseRenderService> logger)
    {
        _db = db;
        _hbs = hbs;
        _recorder = recorder;
        _logger = logger;
    }

    // Test-friendly constructor (no logger required)
    internal ReleaseRenderService(AppDbContext db, IHandlebars hbs, MissingTokenRecorder recorder)
        : this(db, hbs, recorder,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ReleaseRenderService>.Instance)
    {
    }

    public async Task<PreparedReleaseDto> PrepareAsync(
        Guid releaseId,
        PreparePageRequest request,
        CancellationToken ct = default)
    {
        var release = await _db.Releases
            .Include(r => r.Project)
                .ThenInclude(p => p.ProjectRepositories)
                    .ThenInclude(pr => pr.Repository)
            .Include(r => r.ReleaseRepositories)
                .ThenInclude(rr => rr.Repository)
            .Include(r => r.RepositoryTags)
            .FirstOrDefaultAsync(r => r.Id == releaseId, ct)
            ?? throw new NotFoundException("Release", releaseId);

        var project = release.Project;

        var bindings = await _db.TemplateBindings
            .Where(b => b.ProjectId == project.Id)
            .Include(b => b.Template)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

        var customVars = await _db.CustomVariables
            .Where(v => v.ProjectId == project.Id)
            .ToListAsync(ct);

        // Validate: must have at least one ReleaseNotes binding
        if (!bindings.Any(b => b.Kind == TemplateBindingKind.ReleaseNotes))
        {
            throw new ValidationException([
                new ValidationFailure("Bindings", "Project has no ReleaseNotes binding.")
                {
                    ErrorCode = "no_release_notes_binding"
                }
            ]);
        }

        // Resolve context version
        var primaryRepo = project.ProjectRepositories.FirstOrDefault(pr => pr.IsPrimary);
        string version;

        if (request.AdminOverrideVersion is not null)
        {
            version = request.AdminOverrideVersion;
        }
        else
        {
            var snapshot = primaryRepo is not null
                ? release.ReleaseRepositories.FirstOrDefault(rr => rr.RepositoryId == primaryRepo.RepositoryId)
                : null;

            if (!string.IsNullOrEmpty(snapshot?.NextVersion))
            {
                version = snapshot.NextVersion;
            }
            else if (!string.IsNullOrEmpty(release.Version))
            {
                version = release.Version;
            }
            else
            {
                throw new ValidationException([
                    new ValidationFailure("Version", "Could not determine version: primary repo has no semver tag.")
                    {
                        ErrorCode = "no_semver_tag"
                    }
                ]);
            }
        }

        var ctx = BuildContext(release, project, version, customVars, request.ReconciliationData);

        // Build Handlebars-friendly context object
        var customDict = new Dictionary<string, string>(
            customVars.ToDictionary(v => v.Key, v => v.Value),
            StringComparer.Ordinal);

        var hbsContext = BuildHandlebarsContext(ctx, customDict);

        var pages = new List<PreparedPageDto>();
        var warnings = new List<string>();
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in bindings)
        {
            _recorder.BeginCapture();

            var titleTemplate = _hbs.Compile(binding.PageTitleTemplate);
            var bodyTemplate = _hbs.Compile(binding.Template.ContentTemplate);

            var renderedTitle = titleTemplate(hbsContext);
            var renderedBody = bodyTemplate(hbsContext);

            var unknownTokens = _recorder.EndCapture();

            if (renderedTitle.Length > 255)
            {
                renderedTitle = renderedTitle[..255];
                warnings.Add($"Binding {binding.Id}: title was truncated to 255 characters.");
            }

            if (!seenTitles.Add(renderedTitle))
                warnings.Add($"Duplicate page title detected: '{renderedTitle}'.");

            var resolvedParentPageId = binding.ParentPageId ?? project.ConfluenceParentPageId ?? string.Empty;

            pages.Add(new PreparedPageDto(
                binding.Id,
                binding.Kind.ToString(),
                renderedTitle,
                renderedBody,
                resolvedParentPageId,
                binding.LinkFromReleaseNotes,
                binding.SortOrder,
                unknownTokens.ToList()));
        }

        _logger.LogInformation(
            "PrepareAsync releaseId={ReleaseId} pageCount={Count} warnCount={Warnings}",
            releaseId, pages.Count, warnings.Count);

        return new PreparedReleaseDto(ctx, pages, warnings);
    }

    public Task<PublishResultDto> PublishAsync(
        Guid releaseId,
        PublishPagesRequest request,
        CancellationToken ct = default)
        => throw new NotImplementedException("PublishAsync is implemented in Phase 4 (T043).");

    public Task<TemplatePreviewDto> PreviewTemplateAsync(
        Guid templateId,
        TemplatePreviewRequest request,
        CancellationToken ct = default)
        => throw new NotImplementedException("PreviewTemplateAsync is implemented in Phase 7 (T057).");

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ReleaseRenderContextDto BuildContext(
        Domain.Entities.Release release,
        Domain.Entities.Project project,
        string version,
        List<Domain.Entities.ProjectCustomVariable> customVars,
        ReconciliationSummaryDto? reconciliationData)
    {
        // PreviousVersion: from primary repo snapshot or empty
        var primaryRepoId = project.ProjectRepositories.FirstOrDefault(pr => pr.IsPrimary)?.RepositoryId;
        var primarySnapshot = primaryRepoId.HasValue
            ? release.ReleaseRepositories.FirstOrDefault(rr => rr.RepositoryId == primaryRepoId.Value)
            : null;

        var previousVersion = primarySnapshot?.PreviousVersion ?? string.Empty;

        // Repos context
        var repos = release.ReleaseRepositories.Select(rr =>
        {
            var tag = release.RepositoryTags.FirstOrDefault(t => t.RepositoryId == rr.RepositoryId);
            return new RepoContextDto(
                rr.Repository?.Name ?? string.Empty,
                tag?.FromTag ?? rr.PreviousVersion,
                tag?.ToTag ?? rr.NextVersion,
                rr.CommitCount,
                rr.TicketCount,
                $"{rr.Repository?.Name ?? "repo"}_{version}");
        }).ToList();

        // Contributors and tickets are empty unless loaded separately (Phase 3 scope: templates + bindings)
        var tickets = new TicketBucketsDto([], [], [], []);
        var contributors = new List<ContributorDto>();

        // Reconciliation: prefer request-supplied data (from wizard store)
        var reconciliation = reconciliationData;
        if (reconciliation is null && release.Reconciliation is not null)
        {
            var r = release.Reconciliation;
            reconciliation = new ReconciliationSummaryDto(
                r.MatchedCount,
                r.JiraOnlyCount,
                r.GitOnlyCount,
                (double)r.MatchRatePercent,
                r.RunAt);
        }

        var confluence = new ConfluenceTargetDto(
            project.ConfluenceSpaceKey ?? string.Empty,
            project.ConfluenceParentPageId ?? string.Empty);

        var custom = (IReadOnlyDictionary<string, string>)
            customVars.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

        return new ReleaseRenderContextDto(
            new ProjectInfoDto(project.Id, project.Name, project.Description),
            version,
            previousVersion,
            release.CreatedAt,
            repos,
            tickets,
            contributors,
            reconciliation,
            confluence,
            custom);
    }

    private object BuildHandlebarsContext(
        ReleaseRenderContextDto ctx,
        Dictionary<string, string> customSourceDict)
    {
        return new
        {
            project = new
            {
                id = ctx.Project.Id.ToString(),
                name = ctx.Project.Name,
                description = ctx.Project.Description ?? string.Empty
            },
            version = ctx.Version,
            previousVersion = ctx.PreviousVersion,
            releaseDate = ctx.ReleaseDate,
            repositories = ctx.Repositories.Select(r => new
            {
                name = r.Name,
                previousTag = r.PreviousTag,
                nextTag = r.NextTag,
                commitCount = r.CommitCount,
                ticketCount = r.TicketCount,
                jiraFixVersion = r.JiraFixVersion
            }).ToList(),
            tickets = new
            {
                breaking = ctx.Tickets.Breaking.Select(TicketToAnon).ToList(),
                features = ctx.Tickets.Features.Select(TicketToAnon).ToList(),
                fixes = ctx.Tickets.Fixes.Select(TicketToAnon).ToList(),
                other = ctx.Tickets.Other.Select(TicketToAnon).ToList()
            },
            contributors = ctx.Contributors.Select(c => new
            {
                name = c.Name,
                email = c.Email,
                commitCount = c.CommitCount
            }).ToList(),
            reconciliation = ctx.Reconciliation is null ? null : (object)new
            {
                matchedCount = ctx.Reconciliation.MatchedCount,
                jiraOnlyCount = ctx.Reconciliation.JiraOnlyCount,
                gitOnlyCount = ctx.Reconciliation.GitOnlyCount,
                matchRate = ctx.Reconciliation.MatchRate,
                runAt = ctx.Reconciliation.RunAt
            },
            confluence = new
            {
                spaceKey = ctx.Confluence.SpaceKey,
                parentPageId = ctx.Confluence.ParentPageId
            },
            custom = _recorder.CreateRecordingDictionary(customSourceDict)
        };
    }

    private static object TicketToAnon(TicketDto t) =>
        new { id = t.Id, summary = t.Summary, type = t.Type ?? string.Empty, isBreaking = t.IsBreaking };
}
