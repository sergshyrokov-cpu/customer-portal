---
artifact_type: security_review
story: US-001
version: 1
status: DRAFT
created_at: 2026-09-01T13:10:37Z
updated_at: 2026-09-01T13:10:37Z
produced_by: security-reviewer
inputs:
  - path: docs/evidence/US-001-implementation-report.md
    version: 4
  - path: docs/verification/US-001-implementation-verification.md
    version: 2
  - path: docs/specifications/US-001-spec.md
    version: 2
  - path: docs/designs/api/US-001-api-design.md
    version: 2
  - path: docs/designs/database/US-001-db-design.md
    version: 2
  - path: docs/reviews/designs/US-001-design-review.md
    version: 2
  - path: docs/plans/US-001-implementation-plan.md
    version: 2
  - path: docs/reviews/plans/US-001-plan-review.md
    version: 2
  - path: docs/decisions/US-001-open-decisions.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 4
informational_findings: 3
security_sensitive: true
runtime_checks: PARTIAL
semantic_analysis: TEXT_FALLBACK
---

# Security Review — US-001 Customer Registration

## 1. Executive Summary

**Result: PASS.** Independent adversarial review of the ASP.NET Core/EF
Core/SQLite implementation of US-001, the first `SECURITY_REVIEW` for this
Story on the new stack. `implementation_verification` v2 (PASS, 0
Critical/0 Major/3 Minor) is consumed as current functional evidence, not
re-derived; this review instead adversarially probes the eight specific
areas raised for this stage plus the standard checklist.

No Critical or Major finding. Password handling (BCrypt via
`BCrypt.Net-Next`, default work factor, dual-layer policy enforcement),
credential non-exposure, the SC-4 deny-by-default posture, and the newly
added content-type-enforcement middleware are all sound. Four Minor
findings are recorded, three of them genuine "landmines for a future
Story" rather than exploitable weaknesses in US-001's actual surface
(cookie-auth's default 302-redirect challenge behavior contradicts SC-3's
401 requirement but has nothing to challenge yet; `AddAntiforgery()` is
registered with no enforcement wired anywhere; the email-uniqueness
check-then-act race produces `500` instead of `409` under concurrent
duplicate registration, matching the retired Spring Boot track's accepted
equivalent and OD-005:A). Three Informational observations are recorded,
including confirmation that no secret, credential, or path-traversal risk
exists anywhere in this Story's change set.

- **Critical / Major:** 0 / 0
- **Runtime checks:** PARTIAL (live smoke checks performed for the
  registration flow and content-type enforcement; no protected endpoint
  exists yet to observe the cookie-auth challenge finding directly)
- **Recommended next action:** proceed to `RECONCILIATION`.

## 2. Reviewed Artifacts

See front matter `inputs` above — all current, none `SUPERSEDED`.

## 3. Security-Relevant Scope

One new public endpoint (`POST /api/v1/customers`), the project's first —
which also means the project's first exercise of its authentication/
authorization posture (SC-3, SC-4), password-hashing posture (SC-1, SC-2),
and API/error-handling conventions (AC-2, AC-6) all at once. Assets:
customer email (PII, low sensitivity), password (never persisted in
plaintext), password hash (`password_hash`, BCrypt), account role/enabled
state. Trust boundaries: external client → `CustomersController`;
`CustomersController` → `CustomerService`; `CustomerService` →
`CustomerRepository` → `AppDbContext`/SQLite; developer environment →
repository (new `.config/dotnet-tools.json`, `App_Data/` path).

## 4. Environment and Tools

.NET SDK 9.0.314 (targets `net8.0`, runtime 8.0.27). SQLite file-based
(`./App_Data/customer-portal.db`) for local/dev, isolated in-memory for
tests. Tools: `Read`/`Grep` for code and configuration inspection, a live
`dotnet run` session for the targeted runtime checks in §§5–6, `git
status`/`grep` for repository hygiene (§17). No IDE MCP server configured
for this .NET track (`semantic_analysis: TEXT_FALLBACK`). No dependency
vulnerability scanner was run (§14) — not claimed as vulnerability-free.

## 5. Authentication Review

**SC-4 fallback policy + `[AllowAnonymous]` — confirmed sound, no bypass
found.** `[AllowAnonymous]` on `CustomersController.Register` is the
framework-standard override that always wins over any authorization
requirement including a `FallbackPolicy`, independent of routing
specifics — confirmed by reading both the policy registration (`Program.cs`)
and the action attribute directly; this is not a text-search inference,
it's how ASP.NET Core's authorization middleware is documented and
implemented to resolve `[AllowAnonymous]` vs. policy requirements. No
HTTP-verb-tunneling middleware (`UseHttpMethodOverride` or equivalent) is
registered anywhere — confirmed by `grep`, zero matches — so no
verb-tunneling bypass vector exists. Cookie authentication
(`AddAuthentication(...).AddCookie()`) issues no cookie yet (no login flow
exists in this Story's scope), so an unauthenticated request simply has no
credential to present; consistent with the design.

**Finding (F-1, Minor):** `AddCookie()` is registered with no
configuration overrides, meaning it retains ASP.NET Core's default
challenge behavior — a `302` redirect to a login path (`/Account/Login`
default) rather than a `401` status. `security-conventions.md` SC-3
explicitly requires "Failed authentication returns `401` with the standard
error body." This cannot be observed today because **no protected
endpoint exists yet** in the running application — `Register` is correctly
`[AllowAnonymous]`, and nothing else is routed. It is nonetheless a real,
already-committed configuration gap against an approved requirement, not
a hypothetical one: the moment a future Story (e.g. US-002 login, or any
authenticated endpoint) is added without separately fixing this, an
unauthenticated request to it will 302-redirect instead of returning the
required `401`. Recommend configuring
`options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode =
401; return Task.CompletedTask; }` (or equivalent) before or alongside the
next Story that introduces a protected endpoint.

## 6. Authorization Review

Single operation, public by design (SC-4 lists registration as the
approved exception). No role or ownership check applies to this Story
(none required — `x-authorization: none` per api-design v2, matches
Specification §7 SEC-1). Service methods are not independently callable
from outside the DI-wired `CustomersController` → `CustomerService` chain
(no other entry point exists) — confirmed via `grep` for other
`ICustomerService` consumers, none found. No insecure-direct-object-access
risk: the one operation is a `POST` create with no identifier-based
lookup.

## 7. Password and Credential Handling

- Plaintext password: present only on the inbound `RegistrationRequest`
  record — confirmed no other type or method holds it; `CustomerService`
  passes it once to `passwordHasher.Hash(...)` and never stores or logs
  it. **Q3 confirmed:** `BCryptPasswordHasher.Hash` calls
  `BCrypt.Net.BCrypt.HashPassword(password)` with no explicit work-factor
  override — `BCrypt.Net-Next`'s documented default work factor is `10`
  (a real, non-trivial BCrypt cost, not a no-op or weakened stub),
  matching SC-1's "default work factor" requirement exactly and SC-2's
  hashing posture. `Verify` (unused by this Story, reserved for the
  future login flow) is likewise the real library implementation, not a
  stub.
- Password hash: stored only in `password_hash` (`TEXT`,
  `HasMaxLength(60)`); never appears in `CustomerResponse` (the type has
  no such property at all, not merely an excluded field) or in any log
  statement — confirmed via `grep` for `Log(` calls in
  `Services`/`Controllers`/`Security`, zero matches touching request data.
- Dual-layer policy enforcement (FR-6): request-layer
  `RegistrationRequestValidator` (FluentValidation, thoroughly tested) and
  a Service-layer re-check (`CustomerService.IsPolicyCompliant`).
  **Q4, independently assessed:** because `[ApiController]`'s automatic
  model-state validation always short-circuits before the action executes
  when `ModelState.IsValid == false` (confirmed by reading
  `ConfigureApiBehaviorOptions`'s `InvalidModelStateResponseFactory`
  wiring in `Program.cs`, and independently by `implementation_verification`
  v2 §11's runtime confirmation that request validation is genuinely
  active), the Service-layer re-check's failure branch is **provably**
  unreachable via the only entry point into `CustomerService`
  (`CustomersController`) — not merely "probably" unreachable. Concur
  with `implementation_verification` v2's Minor/test-coverage framing
  (F-3 there): this is a defense-in-depth code path with zero current
  exploitability, not a security weakness, because the primary control it
  backs up cannot itself be bypassed through the application's only entry
  point. No escalation warranted.

## 8. Sensitive Data Exposure

`CustomerResponse` (`Id`, `Email`, `Role`, `CreatedAt`) — no credential
field exists on the type. `RegistrationRequest` is a `record`, so its
compiler-generated `ToString()` would render the plaintext password if
ever logged or interpolated — confirmed no code path does this (`grep`
found no `ToString()` call, string interpolation, or logging statement
touching the request object anywhere in `Services`/`Controllers`). Error
responses (`ErrorResponse`) never include request data — confirmed by
reading `GlobalExceptionHandler` and the `InvalidModelStateResponseFactory`
(field-level messages are FluentValidation's own static rule messages,
independently confirmed non-echoing by
`RegistrationRequestValidatorTests.Validate_PasswordFailureMessage_NeverEchoesSubmittedValue`,
which passes). The new `UnsupportedMediaTypeException` message echoes only
the client-supplied `Content-Type` **header** value (never the request
body) — see §10 Q2 for the full assessment; not a credential or body-data
leak.

## 9. Input Validation

Server-side only (FluentValidation + the `System.Text.Json`
`UnmappedMemberHandling.Disallow` unknown-field rejection), confirmed
genuinely active at runtime by `implementation_verification` v2's
integration-test evidence, independently re-confirmed by this review's own
reading of the `InvalidModelStateResponseFactory` wiring. Length
constraints explicit (`email` ≤254, `password` 12–72 **bytes** — the
multi-byte boundary case is explicitly tested). Malformed JSON and
oversized/wrong-shape input are rejected with `400`, not silently
accepted. Validation errors do not reveal internal details (static rule
messages only).

## 10. API Security

**Q1 (SC-4/AllowAnonymous), Q2 (content-type middleware) — both addressed
below with adversarial framing.**

- **Q1:** see §5 — sound, no bypass found beyond the F-1 landmine already
  recorded.
- **Q2, content-type-enforcement middleware:** read
  `Program.cs`'s new `app.Use(...)` block directly. It reads only
  `Request.ContentLength` (a header value, `long?`, no body access) and
  `Request.ContentType`/`Headers.TransferEncoding` (also headers) — **no
  request body is read, buffered, or materialized by this check**, so it
  introduces no memory-amplification or slow-body DoS vector; the check is
  O(1) regardless of the claimed or actual body size. The thrown
  exception's message interpolates only the client's own `Content-Type`
  header string into a JSON field via `WriteAsJsonAsync`
  (`System.Text.Json`), which JSON-escapes control characters and quotes
  automatically — no JSON-injection or response-splitting risk (this is a
  JSON body field, not an HTTP response header, so header-injection is not
  applicable either). **Confirmed: no DoS vector, no information-disclosure
  risk beyond reflecting the client's own already-sent header value back
  to itself** — informational only, not a finding (§21 Informational,
  I-1).
- Response fields minimized (§8). Error responses uniform across
  `400`/`409`/`415`/`500` (independently re-verified by
  `implementation_verification` v2 §8/§11 — this review did not re-run
  the live smoke checks a third time, relying on that stage's fresh,
  independently-reproduced evidence from the same session as authoritative
  current evidence per this Skill's own guidance to reuse
  `implementation_verification`'s confirmed results rather than
  re-deriving them).

## 11. Persistence Security

Password never stored in plaintext (no such column/field exists anywhere
in the entity). `password_hash` explicit length/nullability
(`TEXT`, `HasMaxLength(60)`, required) — confirmed via
`CustomerConfiguration.cs` and independently via
`implementation_verification` v2's runtime EF Core model-metadata test
evidence. Email uniqueness enforced via `uq_customer_email` (exact-match
unique index) plus Service-layer lowercase normalization for
case-insensitivity (OD-006:A) — see §16 Q5 for the concurrency angle.
Database file location: `App_Data/customer-portal.db`, file-based (not
silently in-memory for local/dev), excluded from Git by extension-based
`.gitignore` rules — independently re-confirmed this review (`grep` on
`.gitignore`, §17). No `Database.EnsureCreated()`/`EnsureDeleted()` used
outside the isolated test fixtures (confirmed via `grep` — the only
`EnsureCreated`/`EnsureDeleted` calls, if any, would need to appear
outside `CustomerPortal.Tests/`; none found in production code). Schema
change is a single, reviewed, committed EF Core Migration matching
`db-design` v2 §8.2 exactly (already independently verified twice this
session by `implementation_verification`).

## 12. SQLite and Application Configuration

No database browser/admin UI registered or exposed in any profile —
confirmed via `grep` across `Program.cs` and both `appsettings*.json`
files, zero matches for any such registration. **Q7, independently
re-confirmed:** the `App_Data` path is a fixed, hardcoded relative string
in `appsettings.json` — no user input contributes to it anywhere in the
codebase, so no path-traversal vector exists; no `UseStaticFiles()` (or
equivalent) middleware is registered (confirmed via `grep`, zero matches),
so nothing serves directory listings or raw files from `App_Data/` over
HTTP even if it wanted to. No secret or connection-string credential is
committed — SQLite's connection string carries no username/password to
begin with, and a full `grep` sweep of every changed/new production file
this session for password-literal/secret/API-key patterns returned zero
matches (§17). `Persistence:AutoMigrate` is `false` in the base
`appsettings.json` and only `true` in `appsettings.Development.json` — an
appropriately scoped default, not a blanket unsafe setting.

## 13. Logging and Telemetry

`GlobalExceptionHandler.logger.LogError(exception, "Unhandled exception")`
is the only logging call touching request-adjacent data in the new code,
and it fires only on the generic `500` branch, logging the `Exception`
object (never the raw request body, never `RegistrationRequest`) —
confirmed by direct code reading (§7/§8). No other `Log`/`LogInformation`/
`LogWarning` call exists anywhere in `Services`/`Controllers`/`Security`
(confirmed via `grep`, zero matches). `docs/hooks/tool-usage.jsonl`
(session tool telemetry, outside this Story's change set) was not
inspected in depth this review — out of this Story's affected-component
scope; no Story-introduced telemetry path exists to review.

## 14. Dependencies

No new NuGet package (`.csproj` PackageReference) was added by this
Story — independently re-confirmed by reading both `.csproj` files this
review, matching `implementation_verification` v2's finding. **Q8:**
`.config/dotnet-tools.json` (new this session) declares `dotnet-ef`
8.0.11 as a **local development tool**, not a package reference in any
`.csproj` — it is never loaded into the running application process and
carries zero runtime attack surface for the deployed app. It contains no
secret (tool name, version, command array only — standard, public NuGet
tool metadata) and is pinned to an exact version matching the project's EF
Core package versions (reduces version-drift/confusion risk rather than
introducing one). No vulnerability-database scan was performed for either
the application's packages or this dev tool — not claimed as
vulnerability-free; recommend an approved scanner (e.g. `dotnet list
package --vulnerable`) if/when organizational policy requires it. This is
Informational (I-2), not a finding requiring correction.

## 15. Security Test Coverage

| Security property | Test | Status |
|---|---|---|
| Password hashed, not plaintext | `CustomerServiceTests.RegisterAsync_ValidRequest_PersistsHashedPasswordNotPlaintext` | PASS |
| Password hash never in response | `RegistrationSecurityPostureTests.PostCustomers_SuccessfulRegistration_ResponseBodyContainsNoPasswordOrHash` | PASS |
| Invalid password rejected | `RegistrationRequestValidatorTests` (`[Theory]`, 5 cases + 2 byte-boundary cases) | PASS |
| Invalid email rejected | `RegistrationRequestValidatorTests` (3 cases) | PASS |
| Duplicate email enforced | `CustomerServiceTests`/`CustomerRegistrationApiTests`/`CustomerPersistenceTests` (3 tests, different layers) | PASS |
| Validation message never echoes value | `RegistrationRequestValidatorTests.Validate_PasswordFailureMessage_NeverEchoesSubmittedValue` | PASS |
| No database browser/admin UI publicly accessible | N/A — none exists to test (confirmed absent, §12) | N/A |
| Endpoint access matches approved public status | `RegistrationSecurityPostureTests.PostCustomers_Unauthenticated_IsNotRejectedForLackOfAuthentication`, `AuthorizationFallbackPolicy_RequiresAuthenticatedUser` | PASS |

No test asserts a "method was called" without also asserting the
resulting state/output — reviewed test bodies directly (not just names),
consistent with `implementation_verification` v2's Test Quality Review
(§14 there).

## 16. Abuse Case Review

| Scenario | Expected protection | Evidence | Status | Finding |
|---|---|---|---|---|
| Repeated duplicate registration (same email, rapid succession) | Second attempt rejected, no second account | Sequential: `409` (tested, passes). **Concurrent** (Q5): see below | PARTIAL | F-2 |
| Malformed email | `400` | Tested, passes | OK | — |
| Weak/invalid password | `400` | Tested, passes | OK | — |
| Unexpected request fields (e.g. attempting to submit `role`/`enabled`) | Rejected (`additionalProperties: false` / unmapped-member disallow) | `RegistrationRequest` is a positional `record(Email, Password)` — no `role`/`enabled` property exists on the type for JSON binding to target even before the unmapped-member check runs; confirmed via direct type read | OK | — |
| Non-JSON / oversized-Content-Type-mismatch body | `415` | Tested + independently re-verified live this session and last (`implementation_verification` v2 §8) | OK | — |
| Response inspected for sensitive fields | No password/hash present | Tested (multiple layers) | OK | — |
| Rate limiting / brute-force registration spam | Explicitly out of scope | OD-005:A (approved, unchanged from the original resolution — stack-neutral) | Accepted | — |

**Q5, TOCTOU race condition — independently assessed, not just cited:**
`CustomerService.RegisterAsync` calls `repository.ExistsByEmailAsync`
(a `SELECT`) then, if false, `repository.AddAsync` (an `INSERT` via
`SaveChangesAsync`) — two round trips with no explicit transaction or
row-level locking spanning them. Two concurrent requests for the same
email could both observe "does not exist" before either commits; the
second `INSERT` then trips `uq_customer_email`'s unique-index constraint
at the database level, throwing an EF Core `DbUpdateException` that
**`GlobalExceptionHandler`'s switch does not explicitly match** (only
`DuplicateEmailException`/`InvalidPasswordException`/
`UnsupportedMediaTypeException` are cased) — it falls through to the
generic `500` branch. Traced the full consequence: the `500` response
still uses the generic "An unexpected error occurred." message (not
`exception.Message`), so **no internal detail (SQL, exception type, stack
trace) leaks** even under this race — SC-9 holds. The logged exception
(server-side only) contains the email and the constraint-violation detail,
never the plaintext password — no credential-leak risk in the race path
either. **Net assessment: this is a robustness/correctness gap (wrong
status code under a narrow race window), not a confidentiality,
integrity, or authorization defect** — the unique constraint still
correctly prevents the duplicate row from ever being persisted; data
integrity is fully intact. This exact issue (check-then-act via
`existsByEmail` then insert) existed identically in the retired Spring
Boot implementation and was reviewed there as Minor/accepted given
OD-005:A (anti-abuse/concurrency explicitly out of scope) — confirmed via
`workflow-state.yaml`'s carried history (`SEC F-3`/`MF-3`). OD-005:A's
resolution is unchanged and stack-neutral this session. Recorded as F-2,
Minor, same disposition as the prior track: accepted, with a
recommendation that a follow-up Story map `DbUpdateException` (or
specifically a unique-constraint violation) to `409` for robustness.

## 17. Repository Hygiene

Full `grep` sweep of every new/changed production file this session for
password-literal, secret, API-key, and embedded-connection-credential
patterns: **zero matches** beyond the legitimate `SC-1`/hasher-related
source code itself (which was excluded from the match set by construction
of the search, then manually spot-checked to confirm no false negative).
`.gitignore` covers `**/App_Data/`, `*.db`, `*.db-shm`, `*.db-wal` —
independently re-confirmed by reading the file directly this review. No
generated SQLite database file is present in the working tree or staged
for commit (re-confirmed via `git status --porcelain
--untracked-files=all`, consistent with `implementation_verification` v2
§4). `.config/dotnet-tools.json` contains no secret (§14). No `.env` file,
no private key, no MCP configuration copy present in this Story's change
set.

## 18. Deviations

None beyond what `implementation_report` v4 and
`implementation_verification` v2 already disclose and this review
independently confirmed accurate (§§5–16). No undisclosed security-
sensitive change found.

## 19. Findings

| ID | Severity | Category | Affected artifact/file | Evidence | Required correction | Loop-back |
|---|---|---|---|---|---|---|
| F-1 | Minor | AUTHENTICATION | `CustomerPortal/Program.cs` (`AddCookie()`, unconfigured) | SC-3 requires `401` on failed authentication; default cookie-auth challenge is a `302` redirect. Not observable today (no protected endpoint exists yet) | Configure `CookieAuthenticationOptions.Events.OnRedirectToLogin` (and `OnRedirectToAccessDenied`) to return `401`/`403` instead of redirecting, before or alongside the Story that adds the first protected endpoint | none (non-blocking; no exploitable surface exists in US-001) |
| F-2 | Minor | PERSISTENCE | `CustomerPortal/Services/CustomerService.cs`, `CustomerPortal/Exceptions/GlobalExceptionHandler.cs` | Check-then-act email uniqueness under concurrent duplicate requests trips an unmapped `DbUpdateException` → `500` instead of `409`; no data-integrity or confidentiality impact (constraint still prevents the duplicate row; no internal detail leaks) | Map `DbUpdateException` (or a unique-constraint-specific subtype check) to `409` in `GlobalExceptionHandler` in a follow-up Story; accepted for US-001 per OD-005:A (anti-abuse/concurrency explicitly out of scope), matching the retired Spring Boot track's identical, already-accepted disposition | none (accepted per approved OD-005:A) |
| F-3 | Minor | CONFIGURATION | `CustomerPortal/Program.cs` (`AddAntiforgery()`) | Registered but no enforcement mechanism (filter/middleware/`ValidateRequestAsync` call) is wired anywhere; harmless today since `Register` is correctly exempt and no other endpoint exists, but SC-5's "CSRF stays enabled for every other endpoint" is not actually true in the running system yet | When a future Story adds a session-mutating, non-exempt endpoint, explicitly wire antiforgery enforcement (filter or middleware) at that time — registration alone does nothing | none (non-blocking; no exploitable surface exists in US-001) |
| F-4 | Minor (carried, unchanged) | TEST_COVERAGE / DOCUMENTATION | `docs/architecture/architecture.md` AD-3 | Carried from `implementation_verification` v2 F-4 / `plan-review` v2 F-1 — no new security content; listed here only for completeness of the carried-forward record | Future `architecture.md` wording clarification | none |

No `Critical` finding. No `Major` finding.

## 20. Positive Controls

- BCrypt via `BCrypt.Net-Next`, default (non-trivial) work factor —
  confirmed real, not stubbed.
- Plaintext password confirmed unreachable outside the inbound DTO;
  password hash confirmed absent from every response type and every log
  call, by direct code reading, not inference from naming.
- SC-4 deny-by-default fallback policy confirmed correctly overridden by
  `[AllowAnonymous]` for the one approved public endpoint, with no
  verb-tunneling or routing bypass found.
- Content-type-enforcement middleware confirmed to introduce no DoS or
  injection vector — reads headers only, JSON-escapes its one
  header-derived message field.
- No database browser/admin UI, no static-file serving, no path-traversal
  vector, no secret, no credential anywhere in this Story's change set —
  each independently re-confirmed via direct `grep`, not merely assumed.
- Error responses uniformly leak no internal detail across every status
  code this Story produces, including under the one identified race
  condition (F-2).
- Dual-layer password-policy enforcement confirmed architecturally sound:
  the redundant Service-layer check is provably unreachable given the
  primary control's confirmed-active runtime wiring, not merely assumed
  dead code.

## 21. Open Decisions

**No blocking security Open Decisions were identified.** OD-002 (CSRF
classification, B), OD-003 (duplicate-email response, A), OD-005
(anti-abuse/concurrency, A) remain resolved and correctly, consistently
implemented — independently re-confirmed this review, not merely trusted
from prior stages.

## 22. Review Limitations

- No IDE MCP server configured; all findings are text-search/direct-read
  based, qualified as such throughout — no semantic-tool-based dependency
  or call-graph analysis was possible or claimed.
- No dependency vulnerability scanner was run (§14) — dependency review is
  limited to "what was added" (nothing new) and "what is present"
  (versions read directly from `.csproj`), not CVE-database matching.
- F-1 (cookie-auth challenge behavior) could not be observed via a live
  HTTP round-trip because no protected endpoint exists yet in the running
  application to trigger it — the finding rests on reading the framework's
  documented default behavior and the absence of any overriding
  configuration in `Program.cs`, not on an executed reproduction.
- This review reused `implementation_verification` v2's build/test/AC-
  coverage/persistence evidence rather than re-running `dotnet build`/
  `dotnet test`/a full live smoke pass a third time in the same session,
  per this Skill's own guidance to consume current, already-independently-
  verified evidence rather than re-deriving it; this review's own
  additive checks were targeted `grep`/direct-read inspections (§§10, 12,
  17) rather than a full re-execution.

## 23. Verdict Rationale

Zero Critical, zero Major findings. Password handling, credential
non-exposure, the SC-4 authentication/authorization posture, and the new
content-type-enforcement middleware are all independently confirmed sound
under adversarial review, not merely accepted from prior stages' framing.
Four Minor findings are recorded: three are genuine, already-committed
configuration gaps against approved conventions (SC-3's `401` requirement;
SC-5's "CSRF enabled for every other endpoint" claim) that have **zero
currently-exploitable surface** in US-001 specifically (no protected or
non-exempt endpoint exists yet for either gap to matter against), and one
(F-2) is a robustness gap under a narrow concurrency race with no
confidentiality/integrity impact, explicitly accepted per an approved,
unchanged Open Decision (OD-005:A) and consistent with how the retired
Spring Boot track treated the identical issue. None require a code change
before this Story can proceed — all four are forward-looking
recommendations for later Stories, correctly not escalated to block a
Story whose own actual behavior is secure. Per Step 21, this qualifies as
`PASS`: no Critical/Major finding, `implementation_verification` is
current and `PASS`, required security tests pass, no blocking security
Open Decision.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-001
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-001-security-review.md
  next_stage: RECONCILIATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "F-1 (Minor, AUTHENTICATION): cookie auth's default 302-redirect challenge behavior contradicts SC-3's 401 requirement; not observable today (no protected endpoint exists), but a real gap for the next Story that adds one. Configure OnRedirectToLogin/OnRedirectToAccessDenied to return 401/403 at that time."
    - "F-2 (Minor, PERSISTENCE, accepted per OD-005:A): concurrent duplicate registration can trip an unmapped DbUpdateException -> 500 instead of 409; no data-integrity or confidentiality impact (constraint still prevents the duplicate row, no internal detail leaks). Same disposition as the retired Spring Boot track's identical issue. Follow-up Story to map it to 409."
    - "F-3 (Minor, CONFIGURATION): AddAntiforgery() registered with no enforcement wired anywhere; harmless today (Register is correctly exempt, no other endpoint exists), but SC-5's 'CSRF enabled for every other endpoint' isn't actually true yet. Wire enforcement explicitly when a future Story adds a non-exempt endpoint."
    - "F-4 (Minor, carried, no new security content): architecture.md AD-3 wording ambiguity, carried from implementation_verification v2 / plan-review v2."
    - "Informational: content-type-enforcement middleware independently confirmed to introduce no DoS vector (header-only checks, no body access) and no injection/disclosure risk (JSON-escaped, reflects only the client's own header value)."
    - "Informational: .config/dotnet-tools.json (dotnet-ef local tool) is dev-only, zero runtime footprint, no secret, pinned version -- confirmed out of SEC-11 concern scope."
    - "Informational: BCrypt.Net-Next default work factor (10) confirmed real and non-trivial, not a stub -- matches SC-1/SC-2 exactly."
```
