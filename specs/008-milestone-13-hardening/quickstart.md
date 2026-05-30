# Quickstart: Milestone 13 — Security, Service Ownership & UX Hardening

**Branch**: `009-milestone-13-hardening`
**Date**: 2026-05-30

---

## Prerequisites

- Milestone 12 smoke test passing.
- `.env` / `appsettings.Development.json` accessible.
- `dotnet ef` CLI available.

---

## Implementation Sequence

### Step 1 — TDD gate: Write failing unit tests (backend)

Before writing any production code, create and confirm **red** tests for:

- `SetupKeyAuthorizationFilterTests` — covers: missing header → 401; wrong key → 401; correct key + empty DB → passes through; correct key + existing user → still passes through (user existence is checked by the service layer, not the filter).
- `SetupKeyStartupValidatorTests` — covers: key absent + no users → `StopApplication()` called and fatal logged; key absent + users exist → no stop; key present → no stop.

```powershell
dotnet test backend/tests --filter "FullyQualifiedName~SetupKey"
# Expect: all new tests fail (red)
```

---

### Step 2 — Domain layer changes

In `RepoManager.Domain/Entities/`:

1. `Repository.cs` — add `public string? ServiceOwner { get; set; }`.
2. `ReleaseNoteTemplate.cs` — add `public bool IsSystem { get; set; }`.

---

### Step 3 — Application layer changes

In `RepoManager.Application/`:

1. **DTOs**: Add `string? ServiceOwner` to `RepositoryDto` and `UpdateRepositoryRequest`. Add `bool IsSystem` to `ReleaseNoteTemplateDto`. Create `RepoSummaryContext` record. Extend `ReleaseRenderContext` with `IReadOnlyList<RepoSummaryContext> Repositories`.
2. **Validators**: Add `RuleFor(x => x.ServiceOwner).MaximumLength(120).When(...)` to `UpdateRepositoryRequestValidator`.
3. **ITemplateService**: Add `Task<ReleaseNoteTemplateDto> CloneAsync(int templateId, CancellationToken ct = default)`.

---

### Step 4 — Infrastructure: EF Core config and migrations

1. Update `RepositoryConfiguration.cs` — configure `ServiceOwner` column (nullable, max 120).
2. Update `ReleaseNoteTemplateConfiguration.cs` — configure `IsSystem` column (non-nullable, default `false`).
3. Create `ReleaseSummaryTemplateBody.cs` seed data constant.
4. Run migrations:

```powershell
dotnet ef migrations add AddColumn_Repositories_ServiceOwner `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api

dotnet ef migrations add AddColumn_Templates_IsSystem `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api

dotnet ef database update `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api
```

5. Add `migrationBuilder.InsertData(...)` call to the `AddColumn_Templates_IsSystem` migration's `Up` method (using `ReleaseSummaryTemplateBody.Default` constant).

---

### Step 5 — Infrastructure: Service implementations

1. `RepositoryService.cs` — pass `ServiceOwner` through in `MapToDto` and `UpdateAsync`.
2. `TemplateService.cs`:
   - Guard `UpdateAsync` and `DeleteAsync` with `if (template.IsSystem) throw new ForbiddenException("system_template_readonly")`.
   - Implement `CloneAsync` with the auto-increment naming algorithm (inside a DB transaction).
3. `TemplateRenderingService.cs` — extend `BuildContextAsync` to fetch `ReleaseRepository` join rows and populate `context.Repositories` with `RepoSummaryContext` projections.
4. `ProjectService.cs` — in `CreateAsync`, after committing the new project, query for the system template and insert a `ProjectTemplateBinding` (same transaction).

---

### Step 6 — Api layer: Startup validator and filter

1. Create `SetupKeyStartupValidator.cs` — implement `IHostedService`; read config, check user count, call `StopApplication()` on misconfiguration.
2. Create `SetupKeyAuthorizationFilter.cs` — implement `IAsyncActionFilter`; constant-time header comparison.
3. Register in `Program.cs`:
   ```csharp
   builder.Services.AddHostedService<SetupKeyStartupValidator>();
   builder.Services.AddScoped<SetupKeyAuthorizationFilter>();
   ```
4. Apply to setup endpoint: `[ServiceFilter(typeof(SetupKeyAuthorizationFilter))]` on `AuthController.Setup`.
5. Configure `UseSerilogRequestLogging` to exclude `X-Setup-Key` from enriched diagnostic context.

---

### Step 7 — Api layer: httpOnly refresh token cookie

In `AuthController.Refresh`:

```csharp
Response.Cookies.Append("refreshToken", newRefreshToken, new CookieOptions
{
    HttpOnly = true,
    Secure   = true,
    SameSite = SameSiteMode.Strict,
    Path     = "/api/v1/auth",
    MaxAge   = TimeSpan.FromDays(30)
});
```

Update the refresh endpoint to read the token from `Request.Cookies["refreshToken"]`. Apply the same cookie to the login response.

---

### Step 8 — Api layer: Clone endpoint and response schema updates

1. Add `POST /api/v1/templates/{id}/clone` to `TemplatesController`.
2. Ensure all repository and template response DTOs now include the new fields (`serviceOwner`, `isSystem`).
3. Run `dotnet build` — confirm zero warnings.

---

### Step 9 — Backend integration tests (green gate)

```powershell
dotnet test backend/tests
```

All tests should pass, including:
- `SetupKeyAuthorizationFilterTests` — all scenarios green.
- `SetupKeyStartupValidatorTests` — all scenarios green.
- New integration tests: `SetupEndpointTests`, `ServiceOwnerTests`, `TemplateSystemFlagTests`, `TemplateRenderContextTests`.

---

### Step 10 — Frontend: Regenerate OpenAPI client

```powershell
cd frontend
npm run generate:api   # or equivalent openapi-typescript command
```

Verify the generated client includes `serviceOwner` on `RepositoryDto`, `isSystem` on `ReleaseNoteTemplateDto`, and the new `POST /templates/{id}/clone` operation.

---

### Step 11 — Frontend: Auth store and API client interceptor

1. `authStore.ts` — add `scheduleRefresh(accessToken)` action. Call it in `login()` and in `onRehydrateStorage` hydration callback.
2. `apiClient.ts` — add the 401-intercept → refresh → retry once interceptor with shared `refreshPromise`. Add the `_retried` flag to prevent loops. On refresh failure: clear auth store, navigate to `/login`, show toast `"Your session has expired. Please log in again."`.

---

### Step 12 — Frontend: UI components

1. `RepositoryEditPanel.tsx` — add "Service Owner" `Input` field (label, placeholder, max 120). Admin only edit; Viewer sees read-only text.
2. `TemplatesTable.tsx` — render `[System]` `Badge` on rows where `isSystem === true`. Replace Edit/Delete buttons with a single Clone button for system rows.
3. `ReleasesTable.tsx` — add `DropdownMenu` (kebab) on Draft rows visible to Admin only. Wire "Delete draft" option to confirmation `AlertDialog` → `DELETE` request → fade-out animation + toast.
4. `ReleaseDetailPage.tsx` — add "Delete draft" `Button` (destructive variant) in header for Draft + Admin. Same confirmation + navigate-away-on-success flow. Handle `404` with "Release not found" fallback UI.

---

## Smoke Test (Milestone 13)

1. **Setup key enforcement**: Deploy with `RELEASE_MANAGER_SETUP_KEY` unset and an empty database → application refuses to start; fatal message appears in logs.
2. **Setup key protection**: Set the key, restart, call `POST /api/v1/auth/setup` without the header → `401`; with wrong key → `401`; with correct key → `201`, admin created.
3. **Service Owner in release summary**: Set `ServiceOwner = "Platform Team"` on a repo; create a release including that repo; preview the "Release Summary (Default)" template → table row shows "Platform Team".
4. **Silent session renewal**: Shorten JWT lifetime to 30 s in `appsettings.Development.json`; log in; wait 28 s; make an API call → call succeeds with no redirect to login.
5. **Delete Draft UI**: Admin creates a Draft release; clicks "Delete draft" from the list → confirmation dialog appears; confirms → release disappears with fade animation and toast; navigating to its URL returns "Release not found".
