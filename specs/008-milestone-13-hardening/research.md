---

# Research: Milestone 13 — Security, Service Ownership & UX Hardening

**Branch**: `009-milestone-13-hardening`
**Date**: 2026-05-30

All seven decisions below were resolved from first-principles research; no external unknowns remain.

---

## Decision 1 — Startup validation mechanism for `RELEASE_MANAGER_SETUP_KEY`

**Decision**: `SetupKeyStartupValidator : IHostedService` registered via `AddHostedService<SetupKeyStartupValidator>()`. Its `StartAsync` reads `IConfiguration["RELEASE_MANAGER_SETUP_KEY"]`, queries `IUserRepository` (or `UserManager`) for any existing users, and calls `IHostApplicationLifetime.StopApplication()` then logs a `Fatal` message if the key is absent and no users exist. The hosted service runs after DI is built but before the Kestrel listener opens, so no requests can slip through.

**Rationale**: `IHostedService.StartAsync` is async, has full DI access, and runs in the correct lifecycle phase (post-DI, pre-request). `IStartupFilter` is synchronous and cannot easily query the database. A guard in `Program.cs` before `app.Run()` is also viable but tightly couples startup validation to the composition root.

**Alternatives considered**:
- `IStartupFilter` — synchronous; database access requires blocking calls, which is discouraged in .NET 10.
- Guard block in `Program.cs` before `app.Run()` — rejected; mixes startup validation into the composition root, making it hard to test in isolation.
- Middleware that checks on the first request — rejected; a race window exists between startup and first request.

---

## Decision 2 — Action filter vs middleware for `X-Setup-Key` validation

**Decision**: `SetupKeyAuthorizationFilter : IAsyncActionFilter` applied as `[ServiceFilter(typeof(SetupKeyAuthorizationFilter))]` on the `AuthController.Setup` action only. The filter reads `context.HttpContext.Request.Headers["X-Setup-Key"]`, compares it to the configured key using a constant-time comparison (`CryptographicOperations.FixedTimeEquals`), and short-circuits with `401` if invalid.

**Rationale**: An action filter is endpoint-scoped; middleware applies globally and requires explicit path matching to restrict its effect. Using a filter is simpler, more explicit, and easier to unit-test via `ActionExecutingContext` mocks.

**Alternatives considered**:
- Global middleware with `if (context.Request.Path == "/api/v1/auth/setup")` guard — rejected; path string matching is fragile and the logic belongs at the endpoint level.
- Authorization policy attribute — rejected; custom `IAuthorizationRequirement` for a one-off pre-auth header check is overengineered.

---

## Decision 3 — Serilog redaction of `RELEASE_MANAGER_SETUP_KEY`

**Decision**: Two layers of protection:
1. The `SetupKeyAuthorizationFilter` and `SetupKeyStartupValidator` MUST NOT pass the key value as a structured log parameter (enforced by code review and naming convention — the variable is always called `configuredKey` and never interpolated into log messages, only compared).
2. Register a custom `UseSerilogRequestLogging` `MessageEnricher` via `options.EnrichDiagnosticContext` that explicitly skips adding `X-Setup-Key` headers to the diagnostic context.

No custom `IDestructuringPolicy` is needed because the key is never passed as a destructured object — it lives only as a local variable inside the filter and validator.

**Rationale**: Belt-and-suspenders. The simplest approach (never log the value) is sufficient; a destructuring policy adds complexity only if there were a risk of the value appearing in a captured object graph, which there isn't for a string config value.

**Alternatives considered**:
- `IDestructuringPolicy` that intercepts `IConfiguration` — overcomplicated; the key is a simple string never captured in a destructured type.

---

## Decision 4 — httpOnly cookie for refresh token in ASP.NET Core

**Decision**: In `AuthController.Refresh`, after issuing the new refresh token, call:

```csharp
Response.Cookies.Append("refreshToken", newRefreshTokenValue, new CookieOptions
{
    HttpOnly  = true,
    Secure    = true,                     // HTTPS only
    SameSite  = SameSiteMode.Strict,      // No cross-site sending
    Path      = "/api/v1/auth",           // Scoped to auth endpoints only
    MaxAge    = TimeSpan.FromDays(30)     // Matches refresh token DB lifetime
});
```

The `POST /api/v1/auth/refresh` endpoint reads the token from `Request.Cookies["refreshToken"]` rather than the request body. The `POST /api/v1/auth/login` response also sets the cookie in addition to (or instead of) returning the refresh token in the JSON body.

**Rationale**: `HttpOnly = true` prevents JavaScript access. `Secure = true` enforces HTTPS. `SameSite = Strict` prevents CSRF on the refresh endpoint. `Path = "/api/v1/auth"` ensures the cookie is not sent on every API request, reducing header overhead. The frontend does not need to read or store the refresh token value — the browser sends it automatically on requests to `/api/v1/auth`.

**Alternatives considered**:
- `localStorage` — explicitly prohibited by FR-D06 and security best practices.
- `sessionStorage` — JavaScript-accessible; same risk profile as `localStorage`.
- Memory-only (no persistence) — loses session on browser tab close or page refresh.

---

## Decision 5 — Frontend JWT `exp` claim decode

**Decision**: Decode the `exp` claim from the access token's base64url payload using the browser's built-in `atob`:

```typescript
function getTokenExpiry(token: string): number {
  const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
  return payload.exp * 1000; // convert Unix seconds → milliseconds
}
```

The proactive refresh timer is set as:
```typescript
const msUntilRefresh = getTokenExpiry(accessToken) - Date.now() - 120_000; // 2 minutes early
if (msUntilRefresh > 0) setTimeout(() => scheduleRefresh(), msUntilRefresh);
```

This function is called both after login and on every app load (when the auth store hydrates from persisted state).

**Rationale**: Standard browser `atob` is sufficient for base64url decode with the two-character substitutions. No additional library dependency is needed.

**Alternatives considered**:
- `jwt-decode` npm package — functional but adds a dependency for one function; rejected in favour of the zero-dependency inline approach.
- `jose` library — overkill; full JWT verification is handled by the backend, not the frontend.

---

## Decision 6 — Axios 401 interceptor with in-flight queue

**Decision**: Use a module-level mutable variable `let refreshPromise: Promise<string> | null = null` in the API client module. The response interceptor:

```typescript
apiClient.interceptors.response.use(null, async (error) => {
  const originalRequest = error.config;
  if (error.response?.status !== 401 || originalRequest._retried) {
    return Promise.reject(error);
  }
  originalRequest._retried = true;

  if (!refreshPromise) {
    refreshPromise = apiClient
      .post<{ accessToken: string }>('/auth/refresh')
      .then(r => r.data.accessToken)
      .finally(() => { refreshPromise = null; });
  }

  const newToken = await refreshPromise;
  useAuthStore.getState().setAccessToken(newToken);
  originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
  return apiClient(originalRequest);
});
```

**Rationale**: A single `Promise` reference shared across all concurrent 401 responses guarantees exactly one refresh call regardless of how many requests were in flight. The `_retried` flag prevents infinite loops if the refresh itself returns 401.

**Alternatives considered**:
- `BehaviorSubject` queue (Angular-style) — requires RxJS, which is not in the approved library list.
- Per-request retry with exponential backoff — does not prevent multiple simultaneous refresh calls.

---

## Decision 7 — EF Core seeding strategy for "Release Summary (Default)" system template

**Decision**: Use `migrationBuilder.InsertData(...)` in the `Up` method of the `AddColumn_Templates_IsSystem` migration rather than `modelBuilder.Entity<ReleaseNoteTemplate>().HasData(...)`.

```csharp
migrationBuilder.InsertData(
    table: "ReleaseNoteTemplates",
    columns: new[] { "Name", "Body", "IsSystem", "CreatedAt" },
    values: new object[] { "Release Summary (Default)", ReleaseSummaryTemplateBody.Default, true, DateTime.UtcNow }
);
```

The Handlebars template body is defined as a `const string` in a companion `ReleaseSummaryTemplateBody` static class in the Infrastructure project, keeping the migration file concise.

**Rationale**: `HasData` couples seed content to the EF Core model snapshot and generates spurious migration diffs whenever the seed data changes. `InsertData` in the migration runs exactly once, is explicit, and has no model coupling. The migration is the authoritative source of truth for when the row appeared.

**Alternatives considered**:
- `HasData` in entity configuration — rejected; model snapshot coupling causes confusing future migration noise.
- Programmatic seeding in `DbContext.OnModelCreating` with a `database.EnsureCreated` guard — rejected; not transactional with the schema migration, and bypasses the migration history table.
