---
artifact_type: test_strategy
story: US-001
version: 2
status: ARCHIVED
created_at: 2026-08-31T11:25:00Z
updated_at: 2026-09-02T13:11:31Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-001-register-customer.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 2
  - path: docs/designs/api/US-001-api-design.md
    version: 2
  - path: docs/designs/database/US-001-db-design.md
    version: 2
  - path: docs/impact-analysis/US-001-impact-analysis.md
    version: 2
  - path: docs/plans/US-001-implementation-plan.md
    version: 2
  - path: docs/reviews/plans/US-001-plan-review.md
    version: 2
supersedes: docs/tests/US-001-test-strategy.md v1
---

# Test Strategy — US-001 Customer Registration (v2)

> **v2 revision note:** full rewrite for xUnit/ASP.NET Core, not a
> terminology pass. v1 targeted JUnit 5/Spring Boot Test classes that were
> deleted this session along with the rest of the Spring Boot implementation.
> `CustomerPortal.Tests/` currently has **zero** test files — this is a
> from-scratch, test-first suite against a verified-empty codebase, not a
> delta.

## 1. Scope

Cover every Acceptance Criterion of US-001 (AC-001..AC-007, Specification
v2 §5) across unit, integration, persistence, and security levels, per
`implementation_plan` v2's Testing Strategy section. No test targets
out-of-scope behavior (login, password reset, email verification, MFA,
account activation, profile management, rate limiting — Spec §10).

## 2. Selected Test Levels

| Level | Target | Rationale |
|---|---|---|
| Unit | `CustomerService` | isolate business rules (uniqueness, normalization, hashing delegation, role/enabled defaults) from HTTP and persistence |
| Unit | `RegistrationRequestValidator` | isolate FluentValidation rules from the HTTP pipeline |
| Integration | `POST /api/v1/customers` via `WebApplicationFactory<Program>` | exercise the real pipeline: routing, model binding, `System.Text.Json` unknown-field rejection, `GlobalExceptionHandler`, the new SC-4 fallback policy |
| Persistence | `AppDbContext` against isolated in-memory SQLite | column constraints, unique-index collision, UTC audit timestamps, `IAuditable` wiring |
| Security | HTTP round-trip + DI-resolved `IAuthorizationPolicyProvider` | credential non-exposure, authentication boundary of the new endpoint, the SC-4 fallback policy itself |

**Mocking-library decision (implementation_plan v2 Open Question 1,
resolved by this stage):** hand-written fake implementations of
`ICustomerRepository` and `IPasswordHasher` are used for
`CustomerService` unit tests, **not** a mocking library (`Moq`/
`NSubstitute`). Rationale: `TEST_WRITING`, like `IMPLEMENTATION_PLANNING`,
should not add a dependency without explicit human approval when a
zero-dependency alternative fully covers the need; a hand-written fake for
two small interfaces (three methods total) is simple, readable, and
requires no new package. No new dependency is proposed for this Story.

## 3. Positive Scenarios

- Valid email + policy-compliant password → account created, role
  `CUSTOMER`, `enabled = true`, `201` + `Location` + safe body (AC-001).
- Password is hashed before storage, never stored or returned in plaintext
  (AC-004, AC-005).
- Email is normalized to lowercase before the uniqueness check and before
  persistence (OD-006:A).

## 4. Negative Scenarios

- Duplicate email (exact case and differing case) → rejected, no second
  account (AC-002).
- Malformed / missing email → `400` with an `email` field error (AC-003).
- Password violating any single policy rule (too short, too long, missing
  uppercase/lowercase/digit/special) → `400` with a `password` field error
  (AC-006).
- Missing/wrong `Content-Type` → `415` (AC-007).
- Unknown JSON field in the request body → `400` (Spec §6.3, api-design v2
  §3).

## 5. Boundary Scenarios

- Password exactly 72 ASCII bytes → valid.
- Password exceeding 72 **bytes** via a multi-byte character while under
  72 **characters** → invalid (spec-review F-5; the byte bound, not the
  character count, is authoritative).
- Email exactly at / one over the 254-character maximum.

## 6. Validation Scenarios

- Every `RegistrationRequestValidator` rule tested in isolation
  (`Validate()` called directly, no HTTP pipeline) — required, format,
  length, and each password character-class rule individually.
- Validation failure messages never echo the submitted value (SC-9).

## 7. Security Scenarios

- The new endpoint is reachable without authentication and is not rejected
  with `401`/`403` (SC-4's stated exception).
- The new global SC-4 fallback authorization policy is actually configured
  to require an authenticated user (`DenyAnonymousAuthorizationRequirement`
  present) — verified by inspecting the resolved
  `IAuthorizationPolicyProvider`, not by probing a nonexistent second
  route (there is no other endpoint yet to probe against; inspecting the
  configured policy object directly is the deterministic alternative).
- Successful-registration response body contains neither the submitted
  plaintext password nor any `passwordHash`-shaped value.

## 8. Persistence Scenarios

- `SaveChangesAsync` sets `CreatedAt`/`UpdatedAt` in UTC
  (`DateTimeOffset.Offset == TimeSpan.Zero`) via the already-scaffolded
  `IAuditable` override.
- An exact-duplicate email insert violates the `uq_customer_email` unique
  index (`DbUpdateException`) — case-insensitive collision is a
  **Service**-layer guarantee (lowercasing before insert), not a database
  collation guarantee, so it is covered at the unit level (§3
  `CustomerServiceTests`), not asserted again at the raw-DB level with
  differing case (db-design v2 §5 explicitly states no `LOWER(email)`
  functional index exists).
- `CustomerConfiguration` sets the expected `HasMaxLength`/nullability on
  `Email` (254) and `PasswordHash` (60), inspected via EF Core model
  metadata (`IModel`/`IEntityType`) rather than relying on SQLite to
  enforce a length constraint it does not enforce at the engine level
  (persistence-conventions.md PC-4/PC-9).

## 9. Required Fixtures

- `TestWebApplicationFactory : WebApplicationFactory<Program>` —
  substitutes an isolated in-memory SQLite connection for `AppDbContext`,
  applies committed migrations via `Database.MigrateAsync()` (not
  `EnsureCreated()`, per PC-2), one connection per factory instance. Shared
  by the Integration and Security test classes.
- Hand-written `FakeCustomerRepository` / `FakePasswordHasher` (inline in
  `CustomerServiceTests`) — see §2.
- A dedicated `SqliteConnection`/`AppDbContext` pair per persistence test
  class instance (`IAsyncLifetime`), isolated from both the Integration
  fixture and the local file database.

## 10. Excluded Scenarios (with justification)

- Concurrent duplicate-registration race condition (two simultaneous
  requests for the same email) — out of scope per OD-005:A (anti-abuse/
  concurrency hardening deferred); the unique index still prevents a
  duplicate row even if the observed status code for a race loser is not
  asserted here.
- Rate limiting / anti-abuse — explicitly out of scope (OD-005:A).
- `GET /api/v1/customers/{id}` — not implemented by this Story (the
  `Location` header target); no test targets it.

## 11. Known Limitations

- **Red-phase tests do not compile yet, by necessity.** This is a
  from-scratch Story: `Customer`, `ICustomerRepository`, `IPasswordHasher`,
  `ICustomerService`, `RegistrationRequestValidator`, and
  `CustomersController` do not exist in the repository yet. Tests that
  reference them (unit tests, persistence tests) fail at **compile time**
  (`CS0234`/`CS0246`), not at assertion time — this is the correct and
  only possible "red" state for a statically-typed language with zero
  production code, and is distinct from a test-code defect (see
  `test_generation_report` v2 §5 for the exact compiler evidence and the
  reasoning that these are expected, not invalid, failures).
- The Integration and Security test files (`CustomerRegistrationApiTests`,
  `RegistrationSecurityPostureTests`, `TestWebApplicationFactory`)
  reference no not-yet-existing production symbol and are individually
  free of compiler errors — but `dotnet test` still cannot execute them
  because the whole `CustomerPortal.Tests` project must build as one unit,
  and the unit-test files in the same project do not yet compile.
- No IDE MCP server is configured for this .NET track; all evidence in
  `test_generation_report` v2 comes from direct `dotnet build`/`dotnet
  test` invocations, not semantic analysis.

## 12. Open Decisions Affecting Testing

None. OD-001..OD-006 are all resolved and stack-neutral (§14 of
`impact_analysis` v2). The mocking-library question (implementation_plan
v2 Open Question 1) is resolved by this stage — see §2.
