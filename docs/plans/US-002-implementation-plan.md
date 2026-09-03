---
artifact_type: implementation_plan
story: US-002
version: 2
status: DRAFT
created_at: 2026-09-02T14:00:29Z
updated_at: 2026-09-02T14:12:00Z
produced_by: implementation-planner
inputs:
  - path: docs/stories/US-002-customer-login.md
    version: null
  - path: docs/decisions/US-002-open-decisions.md
    version: 1
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/reviews/specifications/US-002-spec-review.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/reviews/designs/US-002-design-review.md
    version: 1
  - path: docs/impact-analysis/US-002-impact-analysis.md
    version: 1
supersedes: docs/plans/US-002-implementation-plan.md v1
---

# Implementation Plan — US-002 Customer Login (v2)

> **v2 revision note:** fixes `PLAN_REVIEW` v1 finding F-1 (Major). v1 had
> `SessionsController` call `Security.ICustomerAuthService` directly and map
> the returned `Customer` entity to `LoginResponse` inside the Controller —
> `package-map.md`'s Controllers row is an exhaustive allow-list (`Services`,
> `Models.Dtos`, `Models.Requests` only) and `AD-4` requires entity/DTO
> mapping in the Service layer. Fixed by extending
> `Services/ICustomerService` with a new `LoginAsync` method that sits
> between the Controller and `ICustomerAuthService`, mirroring how
> `RegisterAsync` already returns a DTO, never the entity. No Acceptance
> Criterion, contract, or business requirement changed — purely an internal
> wiring correction. See `docs/reviews/plans/US-002-plan-review.md` v1 §14.

## Goal

Implement `POST /api/v1/sessions` end to end exactly as specified in
Specification v2 and designed in API design v1, against the current
`CustomerPortal` codebase (US-001's registration endpoint already
implemented and merged). No persistence change (`DB_DESIGN` NOT_APPLICABLE).

## Source Artifacts

All consumed at the versions listed in the front matter `inputs` above; none
`SUPERSEDED`. `impact_analysis` v1 is the required predicted-change-surface
input — this plan sequences its Create/Modify list and resolves its three
Major risks (R-1, R-2, R-3) into concrete implementation decisions rather
than re-deriving the file list.

## Architectural Changes

Four decisions this plan makes explicit, each resolving a specific
impact-analysis risk or design open question — none introduces new business
behavior beyond what the Specification already requires:

1. **Resolves R-1 (exception-message uniformity).** `AuthenticationFailedException`
   is declared with **no public message-accepting constructor** — its
   message is baked in at the type level:
   `public sealed class AuthenticationFailedException()
   : Exception("Invalid email or password.");`
   This makes it *structurally impossible* (a compile error, not just a
   convention) for any future call site to accidentally pass a
   case-specific message that would break AC-002/AC-003/AC-004's
   byte-for-byte-identical response (FR-7, NFR-5). `CustomerAuthService`
   throws this exact type with no arguments at all three failure points.

2. **Resolves R-2 (timing side-channel).** `CustomerAuthService` always
   calls `IPasswordHasher.Verify` exactly once per login attempt, even when
   no `Customer` is found for the email — verified against a fixed dummy
   hash computed once at class-load (`private static readonly string
   DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())`).
   This keeps the unknown-email path's latency comparable to the
   wrong-password/disabled-account paths, satisfying FR-7's explicit
   "SHALL NOT reveal ... via ... response timing" requirement.

3. **Resolves R-3 (`HttpContext` access from `Security`) and PLAN_REVIEW
   v1 F-1 (Controller must not depend on `Security` directly).**
   `ICustomerAuthService`'s sign-in method takes `HttpContext` as an
   explicit parameter (`Task<Customer> AuthenticateAndSignInAsync(HttpContext
   httpContext, string email, string password)`) — but per plan-review v1's
   finding, `SessionsController` (Controllers) is **not permitted** to call
   `Security` directly (`package-map.md`'s Controllers row is an exhaustive
   allow-list: `Services`, `Models.Dtos`, `Models.Requests` only) or to
   handle a `Customer` entity (`AD-4` requires entity/DTO mapping in the
   Service layer). **Corrected wiring:** `Services/ICustomerService` gets a
   new method, `Task<LoginResponse> LoginAsync(HttpContext httpContext,
   LoginRequest request)`, which calls
   `ICustomerAuthService.AuthenticateAndSignInAsync(httpContext,
   request.Email, request.Password)` and maps the returned `Customer` to
   `LoginResponse` **inside this Service method** — exactly mirroring how
   `RegisterAsync` already returns `CustomerResponse`, never the entity.
   `SessionsController` calls only `ICustomerService.LoginAsync`, passing
   its own `HttpContext` through — satisfying `package-map.md`'s Controller
   allow-list. `HttpContext` now flows `Controller → Service → Security`,
   one hop longer than v1's plan; `Services`' allowed dependencies
   (`package-map.md`) explicitly include `Security` (its "read-only
   helpers" qualifier is stretched by a `SignInAsync`-triggering call — the
   least-bad available option given SC-3's explicit mandate that only
   `ICustomerAuthService` calls `SignInAsync`; flagged as plan-review v1
   M-1, non-blocking, recommend clarifying `package-map.md`'s wording in a
   future pass). Flagged for `PLAN_REVIEW` re-confirmation (Open Question
   1), not a formal Open Decision since no existing convention is
   contradicted — only clarified where it was previously silent.

4. **Refines api-design.md Q-1 (absolute cookie-lifetime cap, OD-006:A).**
   ASP.NET Core's cookie-authentication ticket already carries
   `AuthenticationProperties.IssuedUtc`, set automatically at sign-in — **no
   custom claim is needed** (api-design.md's Q-1 slightly overstated the
   mechanism). `Program.cs`'s `CookieAuthenticationEvents.OnValidatePrincipal`
   compares `context.Properties.IssuedUtc` against the current time via an
   injected `TimeProvider` (not `DateTimeOffset.UtcNow` directly — see
   Testing Strategy) and calls `context.RejectPrincipal()` once the
   absolute cap (8 hours, from configuration) is exceeded.

Cookie name and both lifetime values (OD-006:A's 30-minute idle / 8-hour
absolute) are externalized to `appsettings.json` under a new
`Authentication:Cookie` section (impact-analysis R-5), not hard-coded in
`Program.cs`, consistent with `architecture.md` AD-7.

No transaction-boundary change: login performs zero writes to `customer` (a
single read via the already-existing `ICustomerRepository.FindByEmailAsync`);
`AD-3`'s single-`SaveChangesAsync` branch does not even apply here since
there is no `SaveChangesAsync` call at all in this flow.

## Impact-Analysis Reconciliation

This plan's Files To Create / Files To Modify below match `impact_analysis`
v1 §6 with two material differences from that document's original
classification, both explained here per plan-reviewer's requirement:

1. `appsettings.json` moves from impact-analysis's "Potentially Affected"
   list to a confirmed Modify (Architectural Changes item 4's
   externalization decision, resolving R-5).
2. `Services/CustomerService.cs`, listed by `impact_analysis` v1 §6 as
   "Reuse, no change" (on the premise that login logic lives entirely in
   `Security` per SC-3), is now a **Modify** — plan-review v1 F-1 requires a
   new `LoginAsync` method on `ICustomerService`/`CustomerService` so that
   `SessionsController` never depends on `Security` directly.
   `impact_analysis`'s underlying premise (login business logic sits in
   `Security`, not `Services`) is still correct; what changes is that the
   *Controller-facing entry point* for that logic must be a thin
   `Services`-layer passthrough, not `Security` itself.

## Files To Create

| # | Path | Responsibility |
|---|---|---|
| 1 | `CustomerPortal/Exceptions/AuthenticationFailedException.cs` | single, structurally-fixed-message exception for all three login-failure cases (Architectural Changes item 1) |
| 2 | `CustomerPortal/Security/ICustomerAuthService.cs` + `CustomerAuthService.cs` | look up by email, verify hash (with timing-parity dummy check), check `Enabled`, sign in via `HttpContext` (Architectural Changes items 2–3) |
| 3 | `CustomerPortal/Models/Requests/LoginRequest.cs` | inbound `{ email, password }` DTO |
| 4 | `CustomerPortal/Models/Dtos/LoginResponse.cs` | outbound `{ id, email, role }` DTO |
| 5 | `CustomerPortal/Validation/LoginRequestValidator.cs` | FluentValidation: `email`/`password` non-blank only (Spec §6.1/§6.2 — no format/policy re-check) |
| 6 | `CustomerPortal/Controllers/SessionsController.cs` | `POST /api/v1/sessions` action — calls `ICustomerService.LoginAsync` only |

## Files To Modify

| # | Path | Change |
|---|---|---|
| 7 | `CustomerPortal/Exceptions/GlobalExceptionHandler.cs` | extend the exception-type switch: `AuthenticationFailedException` → `401` |
| 8 | `CustomerPortal/Program.cs` | configure the **already-registered** `.AddCookie()` call (`Cookie.Name`, `ExpireTimeSpan`/`SlidingExpiration` from config, `Events.OnRedirectToLogin`/`OnRedirectToAccessDenied` → 401/403, `Events.OnValidatePrincipal` → absolute-cap check via injected `TimeProvider`); register `ICustomerAuthService`/`CustomerAuthService` and `TimeProvider.System` as scoped/singleton services |
| 9 | `CustomerPortal/appsettings.json` | add `Authentication:Cookie:{Name, IdleTimeoutMinutes, AbsoluteTimeoutHours}` |
| 10 | `CustomerPortal/Services/ICustomerService.cs` + `CustomerService.cs` | add `Task<LoginResponse> LoginAsync(HttpContext httpContext, LoginRequest request)` — calls `ICustomerAuthService.AuthenticateAndSignInAsync`, maps `Customer` → `LoginResponse` (plan-review v1 F-1 fix) |

No `.csproj` production-dependency change. No entity/migration change
(`DB_DESIGN` `NOT_APPLICABLE`, re-confirmed). `Repositories/ICustomerRepository.cs`/
`CustomerRepository.cs`, `Security/IPasswordHasher.cs`/`BCryptPasswordHasher.cs`,
and `Models/Entities/Customer.cs` are reused unmodified. `RegisterAsync`
(already on `ICustomerService`) is untouched — `LoginAsync` is purely
additive to that interface.

## Execution Order

Numbered, dependency-ordered. Each step names its observable completion
evidence — `IMPLEMENTATION` records the actual command/output in the
Implementation Report, this plan only states what "done" looks like.

1. **Create `AuthenticationFailedException`** (`Exceptions/`), with the
   fixed-message constructor from Architectural Changes item 1. *Evidence:*
   `dotnet build` succeeds; no dependents yet.
2. **Extend `GlobalExceptionHandler`'s switch** (Modify #7) to map
   `AuthenticationFailedException` → `401`/`"Unauthorized"`, in the same
   step as Step 1 since the type and its mapping are coupled and neither is
   independently useful. *Evidence:* `dotnet build` succeeds.
3. **Add the `Authentication:Cookie` section** to `appsettings.json`
   (Modify #9) with concrete values (`Name: "CustomerPortal.Auth"`,
   `IdleTimeoutMinutes: 30`, `AbsoluteTimeoutHours: 8`, per OD-006:A).
   *Evidence:* file is valid JSON; `dotnet build` succeeds (no code depends
   on it yet).
4. **Create `ICustomerAuthService`/`CustomerAuthService`** (`Security/`) —
   `AuthenticateAndSignInAsync(HttpContext, email, password)`: normalize/
   look up by email (reuses `ICustomerRepository.FindByEmailAsync`, no
   repository change), verify password (reuses `IPasswordHasher.Verify`,
   with the dummy-hash timing-parity call on the not-found path per
   Architectural Changes item 2), check `Enabled`, throw
   `AuthenticationFailedException` uniformly on any of the three failure
   cases, otherwise build the `CUSTOMER`/`ADMIN` role claim (SC-2) and call
   `httpContext.SignInAsync(...)`, returning the authenticated `Customer`.
   *Evidence:* `dotnet build` succeeds; no dependents yet (`ICustomerService.
   LoginAsync` not added until Step 7).
5. **Create `LoginRequest`** (`Models/Requests/`) and **`LoginResponse`**
   (`Models/Dtos/`) per api-design v1 §4.1/§4.2 exactly (no `format`/
   `maxLength` on `LoginRequest`'s fields, matching the deliberate
   no-re-validation decision). *Evidence:* `dotnet build` succeeds; shapes
   match `openapi.yaml` v1 exactly (`writeOnly` on `password`).
6. **Create `LoginRequestValidator`** (`Validation/`) — `NotEmpty()` on
   `Email` and `Password` only. *Evidence:* `dotnet build` succeeds;
   registered automatically via the existing
   `AddValidatorsFromAssembly(typeof(Program).Assembly)` (no `Program.cs`
   change needed for validator registration itself).
7. **Add `LoginAsync(HttpContext, LoginRequest)` to `ICustomerService`/
   `CustomerService`** (Modify #10, plan-review v1 F-1 fix) — calls
   `ICustomerAuthService.AuthenticateAndSignInAsync(httpContext,
   request.Email, request.Password)` and maps the returned `Customer` to
   `LoginResponse(customer.Id, customer.Email, customer.Role)`, exactly
   mirroring `RegisterAsync`'s existing entity→DTO mapping pattern.
   *Evidence:* `dotnet build` succeeds.
8. **Create `SessionsController`** (`Controllers/`) — `POST
   /api/v1/sessions`, `[AllowAnonymous]`, delegates to
   `ICustomerService.LoginAsync(HttpContext, request)` **only** (never
   touches `Security` or `Models.Entities` directly), returns `200 OK` with
   the resulting `LoginResponse` (no `Location` header — OD-001:A/OD-002:A,
   api-design §3). *Evidence:* `dotnet build` succeeds.
9. **Update `Program.cs`** (Modify #8) — replace the bare `.AddCookie()`
   call with the configured version (Architectural Changes items 3–4):
   read `Authentication:Cookie` config, set `Cookie.Name`, `ExpireTimeSpan`,
   `SlidingExpiration = true`, `Events.OnRedirectToLogin`/
   `OnRedirectToAccessDenied` → `401`/`403` unconditionally (OD-004:A),
   `Events.OnValidatePrincipal` → reject the principal once
   `TimeProvider.System.GetUtcNow() - context.Properties.IssuedUtc` exceeds
   the configured absolute timeout; register `TimeProvider.System` and
   `ICustomerAuthService`/`CustomerAuthService` in DI. *Evidence:* `dotnet
   build` succeeds; application starts (`dotnet run`) without a hosting
   error; the existing SC-4 fallback policy and registration endpoint are
   unaffected (manual smoke check).
10. **Full incremental validation**: `dotnet build`, then a manual
    end-to-end smoke check of the happy path and each documented error case
    (`200`, `400`×2, `401`×3 — wrong password, unknown email, disabled
    account, asserting the three `401` bodies are byte-for-byte identical —
    `415`) via `CustomerPortal.http` or `curl -v` (to also inspect the
    `Set-Cookie` header's `HttpOnly`/`Secure`/`SameSite` attributes), pending
    `TEST_WRITING`'s real automated tests, which supersede this manual check
    once they exist. *Evidence:* recorded request/response pairs (including
    raw headers for the `200` case) in the Implementation Report.
11. **Reconcile documentation**: no `docs/` changes expected beyond the
    Implementation Report itself. Confirm this remains true at
    implementation time; if not, record the deviation.

## Validation Strategy

- `dotnet build` after every step group (1–3, 4, 5–6, 7–9) — do not defer
  all validation to the end.
- Step 10's manual smoke pass explicitly includes a byte-for-byte comparison
  of the three `401` failure bodies (copy/diff the raw JSON), since this is
  the Story's single most security-critical observable behavior and the
  easiest thing to get subtly wrong (e.g. a stray extra space or
  differently-cased field from two different code paths).
- Final full validation before the Implementation Report: `dotnet build`,
  `dotnet test` (once `TEST_WRITING` has produced tests), and the manual
  smoke pass in Step 10.

## Testing Strategy

Executable tests are `test-writer`'s output, not this plan's. Expected
levels (mirrors `impact_analysis` v1 §10, with one addition):

| Level | Target | Acceptance Criteria |
|---|---|---|
| Unit | `CustomerAuthService` (`Security`) and `ICustomerService.LoginAsync` (`Services`) — hand-written fakes for `ICustomerRepository`/`IPasswordHasher`/`ICustomerAuthService`, same pattern as US-001's `CustomerServiceTests` | AC-001, AC-002, AC-003, AC-004 |
| Unit | `LoginRequestValidator` (`FluentValidation.TestHelper`) | AC-007 |
| Integration | `WebApplicationFactory<Program>` HTTP round-trip (reusing US-001's `TestWebApplicationFactory`, relocated or referenced across test namespaces per `impact_analysis` v1 §6) | AC-001, AC-002, AC-003, AC-004, AC-005, AC-006, AC-007 |
| Security | No credential field in any response; `[AllowAnonymous]` present; role claim exact value; **byte-for-byte identical `401` body across AC-002/AC-003/AC-004** (NFR-5); cookie `HttpOnly`/`Secure`/`SameSite=Strict` attributes present on success | AC-002, AC-003, AC-004, AC-005 |
| **New — Absolute cookie-lifetime cap** | `OnValidatePrincipal`'s 8-hour rejection logic, exercised by injecting a fake `TimeProvider` (see New Dependencies) rather than waiting 8 real hours | design-review/api-design Q-1, Architectural Changes item 4 |

The timing side-channel mitigation (Architectural Changes item 2) is
difficult to assert with a strict automated test (timing tests are
inherently flaky); recommend `test-writer` add, at most, a coarse sanity
check (e.g. both paths complete within the same order of magnitude) rather
than a strict equality assertion, and rely primarily on the code-level
guarantee (the dummy-hash call always happens) verified by unit test/code
review, not by measuring wall-clock time.

## Risks

Carried from `impact_analysis` v1 §13, with resolution status:

- **R-1 (Major) — Resolved by this plan.** `AuthenticationFailedException`'s
  fixed-message-only constructor makes the byte-for-byte-identical-response
  requirement a compile-time guarantee, not a code-review convention.
- **R-2 (Major) — Resolved by this plan.** Dummy hash-verify call on the
  unknown-email path (Architectural Changes item 2).
- **R-3 (Major) — Resolved by this plan, flagged for `PLAN_REVIEW`
  re-confirmation.** Explicit `HttpContext` parameter on
  `ICustomerAuthService`'s sign-in method, called only from a new
  `Services`-layer passthrough (`ICustomerService.LoginAsync`) rather than
  directly from `SessionsController` (Open Question 1; also fixes
  `PLAN_REVIEW` v1 F-1 — see Architectural Changes item 3).
- **R-4 (Minor, documentation accuracy) — Not this plan's concern.** No
  action needed; `Program.cs`'s cookie registration already exists, this
  plan correctly treats it as a Modify (Files To Modify #8), not a Create.
- **R-5 (Minor) — Resolved by this plan.** Cookie lifetime values
  externalized to `appsettings.json` (Architectural Changes item 4, Files
  To Modify #9).
- **New (Minor):** the absolute-cookie-cap logic (`OnValidatePrincipal`) is
  the first use of `TimeProvider` in this codebase; if `TEST_WRITING`
  decides not to add the fake-time-provider test dependency (New
  Dependencies below), this logic will only be verified by code review, not
  an automated test. Acceptable if explicitly decided, not a silent gap.

## New Dependencies

**One candidate, test-only, not added by this plan:**
`Microsoft.Extensions.TimeProvider.Testing` (provides `FakeTimeProvider`) in
`CustomerPortal.Tests.csproj`, to let `test-writer` simulate the passage of
8 hours for the absolute-cookie-cap test without a real wait or a
timing-flaky sleep. If `TEST_WRITING` determines this is needed, that stage
proposes it and it requires explicit human approval per `AGENTS.md`, the
same as any new dependency — this plan does not add it. Flagged here for
`PLAN_REVIEW`/human visibility (Open Question 2), mirroring how US-001
flagged its mocking-library question.

**None required for production code** — `TimeProvider.System` is part of
the base class library (`System`namespace, .NET 8), not a NuGet package.

## Configuration Changes

`appsettings.json` gains a new `Authentication:Cookie` section (Files To
Modify #9, concrete values in Execution Order Step 3). No change to
`ConnectionStrings:Default` or `Persistence:AutoMigrate`. No new
environment/profile.

## Open Questions

1. **`HttpContext` parameter on `ICustomerAuthService`** (Architectural
   Changes item 3 / R-3) — a human or `plan-reviewer` should confirm this is
   the right resolution to package-map.md's silence on this point, since it
   is the first time any `Security`-namespace component needs a framework
   type.
2. **Fake-time-provider test dependency** (New Dependencies) — flagged for
   `PLAN_REVIEW`/human visibility before `TEST_WRITING` runs, so that stage
   doesn't have to make a new-dependency call unilaterally, same pattern as
   US-001's mocking-library question.

## Result

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION_PLANNING
  story: US-002
  artifact_status: DRAFT
  artifacts:
    - docs/plans/US-002-implementation-plan.md
  next_stage: PLAN_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "v2 fixes PLAN_REVIEW v1 F-1 (Major): SessionsController no longer calls Security.ICustomerAuthService directly or maps Customer to LoginResponse itself. Added ICustomerService.LoginAsync(HttpContext, LoginRequest) as the Controller-facing entry point, calling ICustomerAuthService internally and mapping the entity to a DTO inside the Service layer, mirroring RegisterAsync's existing pattern. Services/CustomerService.cs reclassified from Reuse to Modify vs. impact_analysis v1 (explained in Impact-Analysis Reconciliation)."
    - "Resolves impact_analysis v1's 3 Major risks with concrete, verifiable decisions: R-1 via a structurally fixed-message exception (compile-time guarantee, not convention); R-2 via a dummy hash-verify call on the unknown-email path; R-3 via an explicit HttpContext parameter on ICustomerAuthService, now correctly called only from the Services layer, flagged for PLAN_REVIEW re-confirmation (Open Question 1)."
    - "Refined api-design.md Q-1: the absolute cookie-lifetime cap needs no custom IssuedUtc claim -- AuthenticationProperties.IssuedUtc already exists on the ticket; implemented via CookieAuthenticationEvents.OnValidatePrincipal + an injected TimeProvider (testable), not DateTimeOffset.UtcNow directly."
    - "R-5 resolved: cookie name and both lifetime values externalized to a new appsettings.json Authentication:Cookie section, not hard-coded."
    - "One candidate test-only dependency (Microsoft.Extensions.TimeProvider.Testing) flagged for PLAN_REVIEW/human visibility (Open Question 2), not added by this plan."
    - "No production NuGet dependency, no migration, no change to CustomerRepository/Customer entity/IPasswordHasher -- reused unmodified, confirmed against impact_analysis v1."
```
