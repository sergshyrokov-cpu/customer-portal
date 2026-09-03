---
artifact_type: impact_analysis
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T13:56:27Z
updated_at: 2026-09-02T13:56:27Z
produced_by: impact-analyzer
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/reviews/specifications/US-002-spec-review.md
    version: 2
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/reviews/designs/US-002-design-review.md
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 1
supersedes: null
semantic_analysis: TEXT_FALLBACK
---

# Impact Analysis — US-002 Customer Login (v1)

IDEA MCP was unavailable this session (connection failure); all repository
inspection below is direct file read / grep, not semantic/symbol analysis.
Confidence is marked accordingly.

## 1. Executive Summary

US-002 adds one endpoint (`POST /api/v1/sessions`) to an already-scaffolded
ASP.NET Core project. The change is additive and self-contained: no existing
Story-001 file's *business logic* is modified, only `Program.cs` (to
configure an *already-registered* but unconfigured cookie-authentication
scheme) and `GlobalExceptionHandler.cs` (to add one new mapping). Overall
risk is **Medium** — not because the endpoint is complex, but because two
non-obvious implementation traps directly threaten the Story's core security
requirement (uniform anti-enumeration response) if not planned for explicitly
(§13 R-1, R-2).

## 2. Source Artifacts

| Artifact | Version/Status |
|---|---|
| docs/specifications/US-002-spec.md | 2 |
| docs/reviews/specifications/US-002-spec-review.md | 2 (PASS) |
| docs/designs/api/US-002-openapi.yaml | 1 |
| docs/designs/api/US-002-api-design.md | 1 |
| docs/reviews/designs/US-002-design-review.md | 1 (PASS) |
| docs/decisions/US-002-open-decisions.md | 1 (all 7 resolved at HUMAN_SPEC_APPROVAL, front-matter not yet updated — known gap) |
| docs/designs/database/US-001-db-design.md, US-001-entity-model.md | 2 (unchanged; DB_DESIGN NOT_APPLICABLE for this Story) |
| Repository state | `CustomerPortal/` inspected directly (see §6) |

## 3. Business Capability Impact

Introduces: customer authentication (login). Reuses unchanged: registration
(US-001), the `Customer` entity/schema, password hashing infrastructure.
No capability is removed or degraded.

## 4. Module Impact

| Module | Impact | Rationale | Confidence |
|---|---|---|---|
| `CustomerPortal` | Modify/Create (additive) | New endpoint, new DTOs, new exception, new Security-namespace service; two existing files touched | HIGH |
| `CustomerPortal.Tests` | Create (additive) | New test suite for login | HIGH |

## 5. Package Impact

| Package | Responsibility | Impact | Rationale | Constraint |
|---|---|---|---|---|
| `Controllers` | HTTP mapping | Create (`SessionsController`) | New public endpoint | AD-2: no business logic, delegates to a Service/Security component |
| `Models.Requests` | Request DTOs | Create (`LoginRequest`) | New inbound shape | AD-4 |
| `Models.Dtos` | Response DTOs | Create (`LoginResponse`) | New outbound shape | AD-4: no credential field |
| `Validation` | FluentValidation | Create (`LoginRequestValidator`) | FR-3 non-blank checks | AD-5 |
| `Security` | Auth setup, cookie config, password hasher | Create (`ICustomerAuthService`/impl); Reuse (`IPasswordHasher`) | SC-3 explicitly names `ICustomerAuthService` in this namespace | package-map.md: `Security` may depend on `Repositories`, `Models.Entities`, `Config` — **does not list `HttpContext`/MVC types**; see R-3 |
| `Repositories` | EF Core queries | Reuse (`ICustomerRepository.FindByEmailAsync` already exists) | No repository change needed | package-map.md unaffected |
| `Exceptions` | Exception → HTTP mapping | Create (new exception type); Modify (`GlobalExceptionHandler`) | New 401 case | AD-6; see R-1 |
| `Services` | Business logic | **Reuse, no change** | Login logic is explicitly placed in `Security` per SC-3, not `Services` — confirmed not a package-map violation (Security→Repositories is an explicitly allowed dependency) | AD-2 vs. SC-3 — pre-existing, approved exception, not a new one this Story invents |
| `Config` | DI/framework wiring | Modify (`Program.cs`) | Configure the *already-registered* cookie scheme; register `ICustomerAuthService` | AD-7 |
| `Data` | `AppDbContext`, migrations | No change | DB_DESIGN NOT_APPLICABLE, confirmed against actual `Customer` entity (Enabled column already present) | — |

## 6. Expected File Changes

Repository inspected directly (`CustomerPortal/**/*.cs`, `Program.cs`,
`*.csproj`, `appsettings*.json`) — 24 production `.cs` files exist from
US-001; see full list in analysis notes.

### Files To Create

| Path | Responsibility | Reason | Source | Confidence |
|---|---|---|---|---|
| `CustomerPortal/Controllers/SessionsController.cs` | `POST /api/v1/sessions` HTTP mapping | New endpoint | FR-1, api-design §4 | HIGH |
| `CustomerPortal/Models/Requests/LoginRequest.cs` | Inbound `{ email, password }` | New DTO | FR-2, openapi `LoginRequest` | HIGH |
| `CustomerPortal/Models/Dtos/LoginResponse.cs` | Outbound `{ id, email, role }` | New DTO | FR-9, openapi `LoginResponse` | HIGH |
| `CustomerPortal/Validation/LoginRequestValidator.cs` | FluentValidation: `email`/`password` non-blank | FR-3 | Spec §6.1/§6.2 | HIGH |
| `CustomerPortal/Security/ICustomerAuthService.cs` + implementation | Look up by email, verify hash, check `Enabled`, call `SignInAsync` | FR-4, FR-5, FR-6, FR-8 | security-conventions.md SC-3 (names this exact abstraction) | HIGH (name/location is explicit in SC-3); MEDIUM (exact method signature — see R-3) |
| `CustomerPortal/Exceptions/AuthenticationFailedException.cs` (name not fixed by any artifact) | Single exception type for all three failure cases (unknown email / wrong password / disabled) | FR-7, FR-16 | Spec §4, §8 | MEDIUM (necessity HIGH, exact name a planning choice) |
| `CustomerPortal.Tests/Login/*` (naming: mirrors `Registration/` convention) | Integration, service, validation, security-posture tests | NFR-5 | test-writer scope | MEDIUM (exact file split is test-writer's call) |

### Files To Modify

| Path | Change | Reason | Source | Confidence |
|---|---|---|---|---|
| `CustomerPortal/Program.cs` | Configure the **already-registered** `.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie()` call (currently zero options) with `Events.OnRedirectToLogin`/`OnRedirectToAccessDenied` → 401/403 (OD-004:A), `Cookie.Name`, `SlidingExpiration`/`ExpireTimeSpan` (OD-006:A); register `ICustomerAuthService` in DI | FR-8, FR-11, FR-12 | api-design §6 | HIGH — verified by direct read; see R-4 (narrative correction) |
| `CustomerPortal/Exceptions/GlobalExceptionHandler.cs` | Add one switch arm: the new exception → `401`, message not derived from `exception.Message` (see R-1) | FR-7 | Spec §8 | HIGH |

### Files To Reuse (unchanged)

| Path | Why reusable |
|---|---|
| `CustomerPortal/Repositories/ICustomerRepository.cs` / `CustomerRepository.cs` | `FindByEmailAsync(string email)` already exists and matches FR-4 exactly |
| `CustomerPortal/Security/IPasswordHasher.cs` / `BCryptPasswordHasher.cs` | `Verify(password, hash)` already exists and matches FR-5 exactly |
| `CustomerPortal/Models/Entities/Customer.cs` | `Email`, `PasswordHash`, `Enabled`, `Role` already present, no change needed |
| `CustomerPortal/Models/Dtos/ErrorResponse.cs` (+ `FieldError`) | Identical shape reused by the openapi contract (verbatim) |
| `CustomerPortal/Services/ICustomerService.cs` / `CustomerService.cs` | Login logic is placed in `Security` per SC-3, not here — **no change** |
| `CustomerPortal.Tests/Registration/TestWebApplicationFactory.cs` | Generic `WebApplicationFactory<Program>` with isolated in-memory SQLite; despite its folder name, its content is not Registration-specific and is directly reusable for login integration tests (test-writer to decide whether to relocate or reference across namespaces) |

### Files Potentially Affected (not expected to require changes, flagged for confirmation)

| Path | Note |
|---|---|
| `CustomerPortal/appsettings.json` / `appsettings.Development.json` | No entry required by any AC, but R-4/api-design Q-1's cookie-lifetime values are currently only planned as in-code constants; AD-7 favors externalized settings over hard-coded values — flagged as a planning input (§15), not a mandatory change |

## 7. API Impact

New operation only (`login`); no existing operation's contract changes. Full
detail in `docs/designs/api/US-002-openapi.yaml` / `-api-design.md`
(DESIGN_REVIEW PASS, independently re-confirmed against the actual
`ErrorResponse`/`FieldError` C# records — byte-for-byte shape match with the
OpenAPI schema, confirmed by direct read of `Models/Dtos/ErrorResponse.cs`).

## 8. Persistence Impact

None. `DB_DESIGN` verdict `NOT_APPLICABLE` independently re-confirmed against
the actual `Customer.cs` entity: `Email`, `PasswordHash`, `Role`, `Enabled`
all already exist with the exact shape login needs to read. No migration.

## 9. Security Impact

- **Authentication established for the first time in this project's runtime
  behavior** — `AddCookie()` was already *registered* by US-001
  (`Program.cs` line 58-59, confirmed by direct read) but with zero options
  and no caller of `SignInAsync` existed until now. This Story is the first
  to actually *exercise* the scheme, not the first to *register* it — see
  R-4.
- Anti-enumeration (SC-3, OD-003:A): the single most security-critical
  requirement of this Story. Two concrete implementation risks identified
  that could silently violate it — see R-1 (exception-message uniformity)
  and R-2 (timing side-channel). Both are **new findings**, not present in
  any upstream artifact's text, surfaced only by inspecting the actual
  `GlobalExceptionHandler.cs` pattern.
- `ICustomerAuthService`'s need for `HttpContext` to call `SignInAsync` is an
  architecture-boundary question not resolved by any existing convention —
  see R-3.
- No new secret, credential, or PII field introduced.
- No new NuGet dependency: cookie authentication
  (`Microsoft.AspNetCore.Authentication.Cookies`) is part of the shared
  ASP.NET Core framework, already in use, confirmed via `Program.cs`
  `using` directive — not a `.csproj` `PackageReference`.

## 10. Testing Impact

| Area | Expected tests | Maps to AC |
|---|---|---|
| Integration (happy path) | `POST /api/v1/sessions` with valid credentials → `200`, `Set-Cookie`, correct body | AC-001 |
| Integration (failure uniformity) | Wrong password / unknown email / disabled account → **byte-for-byte identical** `401` body, status, and absence of `Set-Cookie` | AC-002, AC-003, AC-004 |
| Security | No credential field in any response body (success or failure); `[AllowAnonymous]` present; role claim exact value | AC-005 |
| Validation | Missing/blank `email`/`password` → `400` + `fieldErrors` | AC-007 |
| Media type | Missing/wrong `Content-Type` → `415` | AC-006 |
| Unit (`ICustomerAuthService`) | Hand-written fakes for `ICustomerRepository`/`IPasswordHasher` (same pattern as US-001's `CustomerServiceTests`), covering all four branches (unknown/wrong-password/disabled/success) | AC-001–AC-004 |
| Security posture | Confirm the cookie's `HttpOnly`/`Secure`/`SameSite` attributes and (if feasible in-process) the challenge-behavior override | design-review M-1, OD-004 |

No mocking-library question this time — US-001 already settled on hand-written
fakes (test-writer's decision, not reopened here).

## 11. Configuration and Dependency Impact

- No new NuGet package.
- No new `appsettings.json` key is *required*, but see §15 planning input on
  externalizing the cookie-lifetime values.
- No profile/environment change beyond what US-001 already established
  (`Testing`, `Development`, default).

## 12. Documentation Impact

- `docs/knowledge/project-state.md` — will need an update at this Story's
  eventual archive to record login as a delivered capability (not done now;
  archive-mode's job).
- Consider (non-blocking, flagged in design-review M-1): a future
  `security-conventions.md` SC-5 update to state `HttpOnly`/`Secure`
  explicitly.

## 13. Risks

| id | Severity | Description | Affected area | Mitigation | Human decision required |
|---|---|---|---|---|---|
| R-1 | **Major** | `GlobalExceptionHandler.cs`'s existing pattern (confirmed by direct read) sets the client-facing `message` to `exception.Message` for every non-500 exception. If the new login-failure exception is thrown with a different message string at each of its three call sites (unknown email / wrong password / disabled), the `401` responses for AC-002/AC-003/AC-004 will **not** be byte-for-byte identical, silently violating FR-7/NFR-5/SC-3/OD-003:A — the Story's core anti-enumeration requirement. | Security, Exceptions | `IMPLEMENTATION_PLANNING` must specify that the new exception is thrown with **one single, constant message string** (e.g. a parameterless exception or a fixed constant), never a case-specific message, at all three throw sites. | No — a clear engineering constraint, not a business decision. |
| R-2 | **Major** | BCrypt hashing/verification is deliberately slow (~50-100ms). If `ICustomerAuthService` returns immediately for an unknown email (no `Verify` call) but performs a real `Verify` call for a wrong password or a disabled account, response **latency** distinguishes "email exists" from "email doesn't exist" — a timing side-channel that defeats the uniform-response design even though the response *body* is identical. FR-7 already explicitly says the system "SHALL NOT reveal ... via ... response timing design" which of the cases occurred, but no upstream artifact states a mechanism. | Security | `IMPLEMENTATION_PLANNING` should specify a dummy `IPasswordHasher.Verify` call (e.g. against a fixed, precomputed dummy BCrypt hash) when no account is found, so the unknown-email path takes comparable time to the wrong-password/disabled paths. | No — but worth a security-reviewer confirmation once implemented. |
| R-3 | Major | `ICustomerAuthService` (per SC-3, lives in `Security`) must call `SignInAsync`, which requires an `HttpContext`. `package-map.md`'s `Security` row lists allowed dependencies as `Repositories, Models.Entities, Config` — it does not mention `HttpContext`/MVC types (unlike `Services`, which explicitly forbids them). This is an *omission*, not an explicit rule either way — the first Story to actually call `SignInAsync` is the first to hit this gap. | Architecture boundary | Recommend `IMPLEMENTATION_PLANNING` adopt the common ASP.NET Core pattern: `Task SignInAsync(HttpContext httpContext, Customer customer)` — the Controller passes its own `HttpContext` in, so `ICustomerAuthService` itself declares no framework-type dependency beyond the parameter. This does not violate any stated package-map rule (only `Services` explicitly forbids `HttpContext`). | Recommend confirming at `IMPLEMENTATION_PLANNING`/`PLAN_REVIEW`, not a full Open Decision — no existing convention is contradicted, only silent on this exact point. |
| R-4 | Minor (documentation accuracy) | Spec FR-11 and `api-design.md` state "this Story is the first to register the ASP.NET Core cookie-authentication scheme." Direct read of `Program.cs` (lines 57-59) shows `.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie()` was **already registered by US-001** (with zero options), to support US-001's SC-4 deny-by-default fallback policy. The *substantive* requirement (configure `Events`/`Cookie`/lifetime options, per OD-004:A/OD-006:A) is unaffected — `Program.cs` is a **Modify** of an existing bare registration, not a first-time addition. No AC, contract, or design decision is invalidated by this correction. | Documentation only | None required to proceed; recommend the Specification/api-design's "first to register" framing be corrected in a future pass for accuracy (optional, non-blocking). | No. |
| R-5 | Minor | `appsettings.json` has no entry for the cookie's idle/absolute lifetime (OD-006:A's 30-min/8h values); if hard-coded in `Program.cs`, this is a minor `AD-7` "no hard-coded settings" tension. | Configuration | Recommend `IMPLEMENTATION_PLANNING` add `Authentication:Cookie:IdleTimeoutMinutes` / `AbsoluteTimeoutHours` to `appsettings.json` instead of in-code constants. | No. |

## 14. Open Decisions

No blocking Open Decisions were identified. All 7 Open Decisions from
`docs/decisions/US-002-open-decisions.md` were resolved at
`HUMAN_SPEC_APPROVAL` (2026-09-02T13:45:47Z, `history.jsonl`); their front
matter still shows `OPEN` (known, previously documented process gap — the
resolutions are authoritative via `workflow-state.yaml`/`history.jsonl`, not
this file).

## 15. Planning Inputs

- Login business logic (lookup, verify hash, check `Enabled`, sign in) goes
  in a new `Security/ICustomerAuthService` per SC-3 — **not** in
  `Services/ICustomerService`, which is reused unchanged.
- `ICustomerRepository.FindByEmailAsync` and `IPasswordHasher.Verify` already
  exist and require **no** interface change.
- `Program.cs`'s cookie-authentication registration already exists (bare);
  this Story **configures** it, does not add it fresh (R-4).
- The new login-failure exception **must** carry one single, constant
  message across all three failure branches (R-1) — this is a hard
  correctness constraint for whoever writes `ICustomerAuthService` and the
  `GlobalExceptionHandler` mapping.
- A dummy hash-verify call is needed on the unknown-email path for timing
  parity (R-2).
- `ICustomerAuthService.SignInAsync`-equivalent method should take
  `HttpContext` as an explicit parameter (R-3).
- No new NuGet package; no new migration; `CustomerService`/
  `CustomerRepository`/`Customer` entity are unmodified.
- Recommend externalizing the two cookie-lifetime numbers to
  `appsettings.json` (R-5).

## 16. Traceability

| Spec AC | Spec section | Design artifact | Affected area | Test category |
|---|---|---|---|---|
| AC-001 | §5 | openapi `login` 200 | Controller, Security, Program.cs | Integration |
| AC-002 | §5 | openapi `login` 401 | Security, Exceptions | Integration, Unit |
| AC-003 | §5 | openapi `login` 401 (identical) | Security, Exceptions | Integration |
| AC-004 | §5 | openapi `login` 401 (identical) | Security, Exceptions | Integration |
| AC-005 | §5 | `LoginResponse` schema | Models.Dtos | Security |
| AC-006 | §5 | openapi `login` 415 | (existing Program.cs middleware, reused) | Integration |
| AC-007 | §5 | openapi `login` 400 | Validation | Validation |

## 17. Analysis Limitations

- IDEA MCP unavailable this session; all findings are from direct file
  reads and `find`/grep over the working tree, not semantic/symbol analysis.
  Confidence markers reflect this.
- Exact test file names/split (§10) are `test-writer`'s decision, not fixed
  here.
- The exact new exception type's name (`AuthenticationFailedException` used
  here as a placeholder) is a naming choice for `IMPLEMENTATION_PLANNING`,
  not fixed by any upstream artifact.

## 18. Readiness Result

`PASS`. The change surface is identified with HIGH confidence for all
Create/Modify file predictions (directly verified against the actual
repository state) and MEDIUM confidence only on exact new-class naming and
test-file splitting. No upstream artifact (Specification, API design, DB
design) needs correction to proceed — R-4's narrative inaccuracy is cosmetic,
not a defect in the Story's actual requirements. R-1/R-2/R-3 are real,
non-obvious risks that `IMPLEMENTATION_PLANNING` must address explicitly;
none of them require reopening `SPECIFICATION` or `API_DESIGN`, since the
Specification's *requirements* (FR-7's timing/message-uniformity mandate) are
already correctly stated — only the *mechanism* to satisfy them was
previously undecided, which is exactly `IMPLEMENTATION_PLANNING`'s job.

```yaml
result:
  verdict: PASS
  stage: IMPACT_ANALYSIS
  story: US-002
  artifact_status: DRAFT
  artifacts:
    - docs/impact-analysis/US-002-impact-analysis.md
  next_stage: IMPLEMENTATION_PLANNING
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "R-1 (Major): the new login-failure exception must carry one single, constant message string across all three failure branches (unknown email/wrong password/disabled), or GlobalExceptionHandler's existing exception.Message-passthrough pattern will break the AC-002/AC-003/AC-004 byte-for-byte-identical response requirement. Real, non-obvious risk found by reading GlobalExceptionHandler.cs directly."
    - "R-2 (Major): a dummy password-hash verification is needed on the unknown-email path to avoid a BCrypt-timing side channel that would let a caller distinguish an unknown email from a wrong password by response latency, defeating FR-7's explicit timing-uniformity requirement."
    - "R-3 (Major): ICustomerAuthService (Security namespace, per SC-3) needs HttpContext to call SignInAsync; package-map.md is silent (not prohibitive) on this -- recommend an explicit HttpContext parameter on the service method, confirmed at PLAN_REVIEW."
    - "R-4 (Minor, documentation accuracy): Program.cs already has AddAuthentication().AddCookie() registered by US-001 (verified by direct read) -- Spec FR-11/api-design's 'first to register' framing is factually inaccurate but the substantive requirement (configure Events/lifetime) is unaffected; Program.cs is a Modify, not a first-time addition. Does not require reopening SPECIFICATION or API_DESIGN."
    - "R-5 (Minor): recommend externalizing the OD-006:A cookie-lifetime values (30min/8h) to appsettings.json rather than in-code constants, per AD-7's general externalized-configuration preference."
    - "No new NuGet dependency; no migration; CustomerService/CustomerRepository/Customer entity unmodified; ICustomerRepository.FindByEmailAsync and IPasswordHasher.Verify already exist and are reused as-is."
```
