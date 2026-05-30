using FluentValidation.Results;
using HandlebarsDotNet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Confluence;
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
    private readonly IConfluencePublisher _publisher;
    private readonly IDataProtector _protector;
    private readonly ILogger<ReleaseRenderService> _logger;

    public ReleaseRenderService(
        AppDbContext db,
        IHandlebars hbs,
        MissingTokenRecorder recorder,
        IConfluencePublisher publisher,
        IDataProtectionProvider dataProtection,
        ILogger<ReleaseRenderService> logger)
    {
        _db = db;
        _hbs = hbs;
        _recorder = recorder;
        _publisher = publisher;
        _protector = dataProtection.CreateProtector("ConfluenceConnection.ApiToken");
        _logger = logger;
    }

    // Test-friendly constructor (PrepareAsync tests only — PublishAsync not exercised here)
    internal ReleaseRenderService(AppDbContext db, IHandlebars hbs, MissingTokenRecorder recorder)
        : this(db, hbs, recorder,
            null!,
            new EphemeralDataProtectionProvider(),
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

    public async Task<PublishResultDto> PublishAsync(
        Guid releaseId,
        PublishPagesRequest request,
        CancellationToken ct = default)
    {
        // Validate all titles
        var invalid = request.Pages
            .Where(p => string.IsNullOrWhiteSpace(p.Title) || p.Title.Length > 255)
            .ToList();
        if (invalid.Count > 0)
        {
            throw new ValidationException([
                new ValidationFailure("Pages",
                    "One or more page titles are empty or exceed 255 characters.")
                { ErrorCode = "invalid_page_title" }
            ]);
        }

        var release = await _db.Releases
            .Include(r => r.Project)
            .FirstOrDefaultAsync(r => r.Id == releaseId, ct)
            ?? throw new NotFoundException("Release", releaseId);

        var project = release.Project;

        if (string.IsNullOrEmpty(project.ConfluenceSpaceKey))
            throw new ConflictException(
                "Confluence space key is not configured for this project.",
                "no_confluence_space");

        var connection = await _db.ConfluenceConnections
            .FirstOrDefaultAsync(c => c.IsActive, ct)
            ?? throw new NotFoundException("ConfluenceConnection", "active");

        var conn = new ConfluenceConnectionDto(
            connection.BaseUrl,
            connection.Username,
            _protector.Unprotect(connection.EncryptedApiToken));

        // Load binding kinds so we know which page is the primary ReleaseNotes page
        var bindingKinds = await _db.TemplateBindings
            .Where(b => b.ProjectId == project.Id)
            .Select(b => new { b.Id, b.Kind })
            .ToDictionaryAsync(b => b.Id, b => b.Kind, ct);

        var sortedPages = request.Pages.OrderBy(p => p.SortOrder).ToList();
        var publishedPages = new List<PublishedPageDto>();
        PublishPageDto? primaryPage = null;
        string? primaryPageConfluenceId = null;

        // Publish all pages in sort order
        foreach (var page in sortedPages)
        {
            var parentPageId = page.ParentPageId ?? project.ConfluenceParentPageId ?? string.Empty;
            var result = await _publisher.CreateOrUpdatePageAsync(
                conn,
                project.ConfluenceSpaceKey,
                parentPageId,
                page.Title,
                page.Body,
                page.ExistingConfluencePageId,
                ct);

            if (!result.Success)
                throw new ExternalServiceException(
                    "Confluence",
                    result.ErrorMessage ?? $"Failed to publish page '{page.Title}'",
                    null);

            var published = new PublishedPageDto(
                page.BindingId,
                result.PageId!,
                result.PageUrl!,
                page.Title);

            publishedPages.Add(published);

            // Track primary ReleaseNotes page (first binding of that kind in sort order)
            if (primaryPage is null
                && bindingKinds.TryGetValue(page.BindingId, out var kind)
                && kind == TemplateBindingKind.ReleaseNotes)
            {
                primaryPage = page;
                primaryPageConfluenceId = result.PageId;
            }
        }

        // Append cross-links to the primary ReleaseNotes page for any page with LinkFromReleaseNotes = true
        var linkedPages = publishedPages
            .Where(pp =>
            {
                var src = sortedPages.FirstOrDefault(p => p.BindingId == pp.BindingId);
                return src?.LinkFromReleaseNotes == true && pp.BindingId != primaryPage?.BindingId;
            })
            .ToList();

        if (primaryPage is not null && primaryPageConfluenceId is not null && linkedPages.Count > 0)
        {
            var crossLinks = "\n\n---\n\n## Related Pages\n\n" +
                string.Join("\n", linkedPages.Select(pp => $"- [{pp.Title}]({pp.ConfluenceUrl})"));

            var updatedBody = primaryPage.Body + crossLinks;
            var parentPageId = primaryPage.ParentPageId ?? project.ConfluenceParentPageId ?? string.Empty;

            var updateResult = await _publisher.CreateOrUpdatePageAsync(
                conn,
                project.ConfluenceSpaceKey,
                parentPageId,
                primaryPage.Title,
                updatedBody,
                primaryPageConfluenceId,
                ct);

            if (!updateResult.Success)
            {
                _logger.LogWarning(
                    "PublishAsync releaseId={ReleaseId}: failed to append cross-links to page {PageId}: {Error}",
                    releaseId, primaryPageConfluenceId, updateResult.ErrorMessage);
            }
        }

        _logger.LogInformation(
            "PublishAsync releaseId={ReleaseId} published {Count} pages",
            releaseId, publishedPages.Count);

        return new PublishResultDto(publishedPages);
    }

    public async Task<TemplatePreviewDto> PreviewTemplateAsync(
        Guid templateId,
        TemplatePreviewRequest request,
        CancellationToken ct = default)
    {
        var template = await _db.ReleaseNoteTemplates.FindAsync([templateId], ct)
            ?? throw new NotFoundException("ReleaseNoteTemplate", templateId);

        ReleaseRenderContextDto ctx;
        string? projectName = null;
        string? releaseVersion = null;

        if (string.Equals(request.ContextSource, "project", StringComparison.OrdinalIgnoreCase)
            && request.ProjectId.HasValue)
        {
            var project = await _db.Projects
                .Include(p => p.ProjectRepositories)
                    .ThenInclude(pr => pr.Repository)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value, ct)
                ?? throw new NotFoundException("Project", request.ProjectId.Value);

            var latestRelease = await _db.Releases
                .Include(r => r.ReleaseRepositories)
                    .ThenInclude(rr => rr.Repository)
                .Include(r => r.RepositoryTags)
                .Include(r => r.Reconciliation)
                .Where(r => r.ProjectId == project.Id)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var customVars = await _db.CustomVariables
                .Where(v => v.ProjectId == project.Id)
                .ToListAsync(ct);

            if (latestRelease is not null)
            {
                var primaryRepoId = project.ProjectRepositories.FirstOrDefault(pr => pr.IsPrimary)?.RepositoryId;
                var snapshot = primaryRepoId.HasValue
                    ? latestRelease.ReleaseRepositories.FirstOrDefault(rr => rr.RepositoryId == primaryRepoId.Value)
                    : null;
                var version = snapshot?.NextVersion ?? latestRelease.Version ?? "0.0.0";
                ctx = BuildContext(latestRelease, project, version, customVars, null);
                projectName = project.Name;
                releaseVersion = version;
            }
            else
            {
                ctx = BuildSyntheticContext(project.Name, project.Id);
                projectName = project.Name;
                releaseVersion = "1.0.0";
            }
        }
        else
        {
            ctx = BuildSyntheticContext("Sample Project", Guid.Empty);
        }

        var customDict = ctx.Custom.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var hbsContext = BuildHandlebarsContext(ctx, customDict);

        _recorder.BeginCapture();
        var pageTitleTemplate = $"{{{{project.name}}}} {{{{version}}}} — {template.Name}";
        var titleFn = _hbs.Compile(pageTitleTemplate);
        var bodyFn = _hbs.Compile(template.ContentTemplate);
        var renderedTitle = titleFn(hbsContext);
        var renderedBody = bodyFn(hbsContext);
        var unknownTokens = _recorder.EndCapture();

        return new TemplatePreviewDto(
            renderedTitle,
            renderedBody,
            unknownTokens.ToList(),
            request.ContextSource ?? "synthetic",
            projectName,
            releaseVersion);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ReleaseRenderContextDto BuildSyntheticContext(string projectName, Guid projectId)
    {
        var tickets = new TicketBucketsDto(
            [new TicketDto("PRJ-100", "Upgrade core library", "task", false)],
            [new TicketDto("PRJ-101", "Add export to CSV feature", "story", false)],
            [new TicketDto("PRJ-102", "Fix null reference in payment flow", "bug", false)],
            []);

        var repos = new List<RepoSummaryContext>
        {
            new("sample-api", "", "1.0.0", "1.1.0", 12, 3)
        };

        var contributors = new List<ContributorDto>
        {
            new("Alice Example", "alice@example.com", 7),
            new("Bob Example", "bob@example.com", 5)
        };

        var custom = (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
        {
            ["slackChannel"] = "#releases"
        };

        return new ReleaseRenderContextDto(
            new ProjectInfoDto(projectId, projectName, "Sample project for template preview"),
            "1.1.0",
            "1.0.0",
            DateTimeOffset.UtcNow,
            repos,
            tickets,
            contributors,
            null,
            new ConfluenceTargetDto("REL", "123456789"),
            custom);
    }

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
        var repos = release.ReleaseRepositories.Select(rr => new RepoSummaryContext(
            rr.Repository?.Name ?? string.Empty,
            rr.Repository?.ServiceOwner ?? string.Empty,
            rr.PreviousVersion,
            rr.NextVersion,
            rr.CommitCount,
            rr.TicketCount
        )).ToList();

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
                serviceOwner = r.ServiceOwner,
                previousVersion = r.PreviousVersion,
                nextVersion = r.NextVersion,
                commitCount = r.CommitCount,
                ticketCount = r.TicketCount
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

// Minimal IDataProtectionProvider used only by the test-friendly constructor.
// PublishAsync is never exercised through that path.
file sealed class EphemeralDataProtectionProvider : IDataProtectionProvider
{
    public IDataProtector CreateProtector(string purpose) => new NullProtector();

    private sealed class NullProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => plaintext;
        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
