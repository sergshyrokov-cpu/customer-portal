# Security Conventions

Explicit security decisions for this project. `security-reviewer` and
`design-reviewer` enforce these; `spec-writer` cites them; `aspnet-implementor`
implements to them.

> **These are intentional training-project decisions, not inferred universal
> ASP.NET Core defaults.** A Story may deviate only through a resolved Open
> Decision approved by a human.

## Training-project security policy

```yaml
authentication_model: cookie-based (ASP.NET Core cookie authentication) for initial MVP unless a Story explicitly introduces tokens
password_hashing: BCrypt (BCrypt.Net-Next), not the built-in ASP.NET Core Identity PBKDF2 hasher
password_min_length: 12
password_max_length: 72
password_requirements:
  - at least one uppercase letter
  - at least one lowercase letter
  - at least one digit
  - at least one special character
csrf:
  browser_session_endpoints: enabled (antiforgery token required on unsafe methods)
  stateless_api_endpoints: requires explicit architecture decision
db_admin_console:
  enabled: false
secrets:
  committed_to_repository: forbidden
schema_generation:
  allowed:
    - EF Core Migrations (committed, reviewed)
  forbidden:
    - Database.EnsureCreated() outside isolated test databases
    - Database.EnsureDeleted() outside isolated test databases
```

## SC-1 Passwords

- Hash with `BCrypt.Net.BCrypt.HashPassword` (`BCrypt.Net-Next` package,
  default work factor). The hasher is wrapped in an injectable
  `IPasswordHasher` implementation living in the `Security` namespace. A
  no-op / plaintext hasher is forbidden. The framework's own
  `Microsoft.AspNetCore.Identity.IPasswordHasher<T>` (PBKDF2) is not used, to
  keep hashing behavior and stored-hash format identical across this
  project's history.
- Enforce the `password_*` policy above during request validation (a
  FluentValidation custom rule in `Validation`) **and** re-check in the
  Service before hashing.
- Plaintext passwords: accepted only in the inbound request DTO; never
  persisted, never logged, never returned, never placed on a response DTO.
- The hash is stored in `password_hash` (see `persistence-conventions.md`
  PC-9) and is never returned by any endpoint.

## SC-2 Roles

- `CUSTOMER` — default role on registration.
- `ADMIN` — administrative operations.
- Role claim values are exactly `CUSTOMER` / `ADMIN` (`ClaimTypes.Role`) — no
  `ROLE_` prefix; ASP.NET Core's `[Authorize(Roles = "...")]` compares the raw
  claim value.
- Default account state on registration: enabled (unless a Story's approved
  design says otherwise).

## SC-3 Authentication

- ASP.NET Core cookie authentication
  (`AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
  .AddCookie(...)`), issuing a `ClaimsPrincipal` built from the stored
  account.
- A `ICustomerAuthService` in the `Security` namespace loads the account by
  email and verifies the password hash; it is the sole place that calls
  `SignInAsync`/`SignOutAsync`.
- Failed authentication returns `401` with the standard error body — it does
  not reveal whether the email exists (no account enumeration) unless a
  Story's approved design explicitly allows it.
- **Known gap (from US-001 delivery, unresolved):** ASP.NET Core cookie
  authentication's default unauthorized-access behavior is a `302` redirect
  to a login path, not a `401` response — that default has not yet been
  reconciled with this rule. It was not observable during US-001 (no
  authenticated endpoint existed), but **must be explicitly addressed by the
  first Story that adds an `[Authorize]` endpoint** (expected: US-002
  Customer Login), e.g. by overriding
  `CookieAuthenticationEvents.OnRedirectToLogin` to return `401` for API
  requests instead of redirecting. See
  `docs/evidence/US-001-delivery-summary.md` for the originating finding.

## SC-4 Authorization

- Deny by default: every Controller action requires `[Authorize]` unless the
  approved API design lists it as public (e.g. registration), in which case
  it carries `[AllowAnonymous]` explicitly.
- Role checks with `[Authorize(Roles = "...")]` on the Controller action —
  stated per endpoint in the API design.
- Ownership checks (a customer may act only on their own resource) are
  enforced in the Service layer, not just by role.

## SC-5 CSRF

- Enabled for browser/session endpoints (the MVP default): the antiforgery
  service (`AddAntiforgery()`) issues a token via a dedicated endpoint, and
  every unsafe method (`POST`/`PUT`/`PATCH`/`DELETE`) from a browser session
  validates it; the auth cookie is `SameSite=Strict` (or `Lax` where a
  documented cross-site flow requires it).
- Disabling CSRF validation for a stateless API endpoint requires an approved
  architecture decision recorded for that Story.

## SC-6 Database admin console

- No database browser/admin UI is registered or exposed in **any**
  environment, including test and local (see `persistence-conventions.md`
  PC-1). Never exposed through a permissive routing/security rule.

## SC-7 Secrets & repository hygiene

- No credentials, tokens, private keys, or `.env`/`appsettings.*.Local.json`
  files committed.
- Config secrets come from environment variables / the .NET Secret Manager
  (`dotnet user-secrets`, local only) / externalized config — never
  `appsettings.json` in the repository.
- Generated SQLite database files are git-ignored (see
  `persistence-conventions.md` PC-1).

## SC-8 Schema safety

- Schema generation is restricted to committed EF Core Migrations (see the
  policy block and `persistence-conventions.md` PC-2). `EnsureCreated()` and
  `EnsureDeleted()` outside an isolated test database are forbidden — they can
  destroy or silently diverge the schema from its migration history.

## SC-9 Error & log hygiene

- Error responses follow `api-conventions.md` AC-6 and never leak stack
  traces, SQL, entity/class names, filesystem paths, database connection
  strings, or secrets.
- Logs never contain passwords, password hashes, `Cookie`/`Authorization`
  headers, tokens, or full credential-bearing request bodies.
- Tool-usage telemetry (`docs/hooks/tool-usage.jsonl`) records metadata only
  (tool, timestamp, status, sizes), never full sensitive payloads.
