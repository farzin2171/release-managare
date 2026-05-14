# Research: Repository Release Management Platform

**Phase 0 output** — All decisions resolved. No NEEDS CLARIFICATION items remain.

---

## Decision 1: SQLite over PostgreSQL

**Decision**: Use SQLite (WAL mode) as the only database engine.

**Rationale**: Single-tenant deployment, single Docker container, no concurrent write contention beyond what WAL handles. Eliminating a separate database process reduces operational complexity (no Postgres container, no connection pooling config, no separate backup tooling). EF Core 10 supports SQLite fully including migrations. WAL mode provides sufficient read concurrency for the expected load (< 100 users, mostly read traffic from Viewers).

**Alternatives considered**: PostgreSQL — rejected because it requires a separate container, adds network overhead, and provides no benefit for single-tenant read-heavy workloads at this scale.

**Reference**: [docs/03-plan.md](../../docs/03-plan.md) § Database

---

## Decision 2: EF Core 10 with SQLite WAL Mode

**Decision**: Enable WAL mode via `PRAGMA journal_mode=WAL` in the `OnConfiguring` override or via a migration seed.

**Rationale**: WAL mode allows concurrent reads while a write is in progress, which matters when background sync jobs write repository/commit data while Viewer HTTP requests read the same tables.

**Implementation pattern**:
```csharp
// In AppDbContext.OnConfiguring or DependencyInjection.cs
optionsBuilder.UseSqlite(connectionString, o =>
    o.CommandTimeout(30));

// After connection is opened (via SavedChanges event or migration):
db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
db.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
```

**Alternatives considered**: WAL mode via SQLitePCLRaw direct call — same result but less idiomatic with EF Core abstractions.

---

## Decision 3: Azure DevOps Client Library

**Decision**: Use `Microsoft.TeamFoundationServer.Client` + `Microsoft.VisualStudio.Services.Client` NuGet packages for the `AzureDevOpsGitProvider` implementation.

**Rationale**: These are the official Microsoft-maintained client libraries for Azure DevOps REST API v7.1. They handle authentication, pagination, and serialization. The `VssConnection` + `GitHttpClient` API provides strongly-typed access to repository listing, tag enumeration, commit range queries, and pull request queries — all required operations for `IGitProvider`.

**Key API patterns**:
```csharp
var creds = new VssBasicCredential(string.Empty, pat);
using var connection = new VssConnection(new Uri(orgUrl), creds);
var gitClient = connection.GetClient<GitHttpClient>();

// List repos:
var repos = await gitClient.GetRepositoriesAsync(project, ct);

// Commits between tags:
var criteria = new GitQueryCommitsCriteria { ItemVersion = from, CompareVersion = to };
var commits = await gitClient.GetCommitsAsync(repoId, criteria, ct: ct);
```

**Alternatives considered**: Plain `HttpClient` against Azure DevOps REST API directly — viable but requires manual pagination, auth header management, and response deserialization. The client library is preferred for type safety and reduced boilerplate.

---

## Decision 4: Markdown → Confluence Storage Format via Markdig

**Decision**: Use `Markdig` with a custom `IMarkdownRenderer` that emits Confluence Storage Format (XHTML-based) instead of HTML.

**Rationale**: `ConfluencePublisher` receives Markdown (the user's edited release notes) and must POST Confluence Storage Format XML to the Confluence REST API. Markdig's renderer pipeline is extensible — a custom `HtmlRenderer` subclass can override rendering for each AST node type (headings, paragraphs, code blocks, lists, emphasis) to emit the Confluence equivalents (`<h2>`, `<p>`, `<ac:structured-macro ac:name="code">`, `<ul>/<li>`, `<strong>/<em>`).

**Key mapping table**:
| Markdown | Confluence Storage Format |
|----------|--------------------------|
| `# H1` | `<h1>` |
| `**bold**` | `<strong>` |
| `` `code` `` | `<code>` |
| ` ```block``` ` | `<ac:structured-macro ac:name="code"><ac:plain-text-body>` |
| `[text](url)` | `<a href="url">text</a>` |
| `- item` | `<ul><li>` |

**Alternatives considered**: Pandoc CLI — adds an external binary dependency to the Docker image, complicates the build. Hand-rolled regex replacement — fragile, hard to maintain. Markdig custom renderer is the cleanest purely managed solution.

---

## Decision 5: HandlebarsDotNet for Release Note Templates

**Decision**: Use `HandlebarsDotNet` for release note template rendering.

**Rationale**: Handlebars is a well-known, logic-minimal template language that non-developer Admins can understand. It supports `{{#each tickets}}`, `{{#if isBreaking}}`, and partials — sufficient for the structured variable placeholders required by FR-024. `HandlebarsDotNet` is the idiomatic .NET port with near-identical syntax to the JavaScript original.

**Template variable model** (passed to `Handlebars.Compile(template)(model)`):
```
{
  project: { name, version, repositoryNames[] },
  sections: {
    breaking: TicketGroup[],
    features: TicketGroup[],
    fixes: TicketGroup[],
    other: TicketGroup[]
  },
  contributors: string[],
  repositories: { name, fromTag, toTag, commitCount }[]
}
```

**Alternatives considered**: Scriban — more powerful but heavier; Liquid — good alternative but HandlebarsDotNet is simpler and has broader .NET adoption for this use case.

---

## Decision 6: Polly v8 Configuration Pattern

**Decision**: Use `Microsoft.Extensions.Http.Polly` (Polly v8 integration for `IHttpClientBuilder`).

**Rationale**: Jira and Confluence clients are registered as typed `HttpClient`s. Polly v8's `AddPolicyHandler` extension on `IHttpClientBuilder` attaches retry policies declaratively. The retry policy: 3 retries, exponential backoff (2^attempt seconds), triggered on HTTP 429 and 5xx, max 30 s total.

**Implementation pattern**:
```csharp
services.AddHttpClient<IJiraService, JiraService>()
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

**Alternatives considered**: `Microsoft.Extensions.Http.Resilience` (newer API) — functionally equivalent but requires .NET 8+ and differs in configuration style. `Microsoft.Extensions.Http.Polly` is specified in the approved library list and is well-understood.

---

## Decision 7: ASP.NET Core JWT with Refresh Token Rotation

**Decision**: Use `Microsoft.AspNetCore.Authentication.JwtBearer` with a custom refresh token store in the `Users` table (hashed refresh token column, expiry timestamp).

**Rationale**: No external identity provider. Tokens expire after 8 hours (FR-027). Refresh token rotation: each use of `/auth/refresh` issues a new access token + new refresh token and invalidates the previous refresh token (stored hash updated). This prevents replay after token theft.

**Implementation**:
- Access token: 8-hour JWT signed with HS256, carrying `sub` (user ID) and `role` claims
- Refresh token: 256-bit random bytes, SHA-256 hashed before storage, 30-day expiry
- `/auth/setup` endpoint: auto-disables by checking if any Admin user exists in DB before processing

**Alternatives considered**: ASP.NET Core Identity full stack — adds significant complexity (identity tables, role tables, claim tables) not needed for a two-role system. Custom minimal implementation is simpler and sufficient.

---

## Decision 8: openapi-typescript Codegen Workflow

**Decision**: Generate the TypeScript API client from the Swashbuckle-produced OpenAPI JSON at `http://localhost:5000/swagger/v1/swagger.json` via `npx openapi-typescript`.

**Rationale**: Principle II mandates the frontend API client be generated from the OpenAPI spec. Running `npx openapi-typescript http://localhost:5000/swagger/v1/swagger.json -o src/lib/api.d.ts` produces a typed schema. A thin wrapper using `fetch` (or a generated fetch client) maps the schema types to runtime calls. This ensures backend contract changes are immediately surfaced as TypeScript compile errors in the frontend.

**Workflow integration**: `npm run codegen` script in `frontend/package.json` runs this command. CI must run codegen and fail if the generated file differs from committed.

---

## Decision 9: Conventional Commit Regex Pattern (TDD-First)

**Decision**: Pure C# regex, TDD-first via `IConventionalCommitParser`. Tests written and failing before implementation starts (Principle III mandatory enforcement).

**Patterns**:
```
Header: ^(?<type>\w+)(\((?<scope>[^)]+)\))?(?<breaking>!)?:\s*(?<desc>.+)$
Body breaking: BREAKING[ -]CHANGE: (case-sensitive, anywhere in body)
Jira scope: ^[A-Z]{2,10}-\d+$
```

**Test fixture coverage required** (from docs/03-plan.md):
- All standard types: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert
- Scope with and without Jira ticket
- Breaking via `!` in header
- Breaking via `BREAKING CHANGE:` in body
- Multi-line bodies
- Empty bodies
- Non-conventional: `WIP: blah`, `fix stuff`, `Merge pull request #123`

---

## Decision 10: First-Run Bootstrap Endpoint

**Decision**: `/api/v1/auth/setup` — available only when no Admin user exists in the database. Auto-disables by returning `410 Gone` once the first Admin is created.

**Rationale**: FR-028 requires a one-time setup wizard. Checking the `Users` table on every request to this endpoint is a single DB read with negligible overhead. Simpler than a persistent flag or migration seed with environment variable coupling.

**Security note**: This endpoint is the sole exception to JWT authentication on write endpoints. It MUST check `db.Users.AnyAsync(u => u.Role == Role.Admin)` and return 410 if true before processing the request body.
