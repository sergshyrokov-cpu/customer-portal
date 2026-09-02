---
artifact_type: security_review
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T14:42:45Z
updated_at: 2026-09-02T14:42:45Z
produced_by: security-reviewer
inputs:
  - path: docs/evidence/US-002-implementation-report.md
    version: 1
  - path: docs/verification/US-002-implementation-verification.md
    version: 1
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 2
informational_findings: 2
security_sensitive: true
runtime_checks: PARTIAL
semantic_analysis: TEXT_FALLBACK
---

# Security Review — US-002 Customer Login (v1)

## 1. Executive Summary

Independent adversarial review of the login implementation, not limited to
re-confirming `implementation_verification` v1's findings. **0 Critical, 0
Major.** The Story's core anti-enumeration property (uniform response
across unknown-email/wrong-password/disabled-account) is verified as a
genuine, structural guarantee, and its timing-parity mechanism is more
robust than documented (the dummy hash uses the *same* BCrypt work factor
as real hashes, not merely "a hash"). One new abuse case was analyzed from
scratch — **login CSRF** — and found to be already mitigated by an
existing, pre-US-002 control (`Content-Type: application/json`
enforcement + no CORS policy configured), not by anything this Story
added; this reasoning was never previously documented anywhere in the
Story's artifacts and is recorded here for the first time (Informational,
non-blocking). 2 Minor findings carried/newly noted, both non-blocking.
**Verdict: PASS.** Recommend `RECONCILIATION`.

## 2. Reviewed Artifacts

| Artifact | Version |
|---|---|
| docs/evidence/US-002-implementation-report.md | 1 |
| docs/verification/US-002-implementation-verification.md | 1 (PASS) |
| docs/specifications/US-002-spec.md | 2 |
| docs/reviews/specifications/US-002-spec-review.md | 2 (PASS) |
| docs/designs/api/US-002-api-design.md, -openapi.yaml | 1 |
| docs/reviews/designs/US-002-design-review.md | 1 (PASS) |
| docs/plans/US-002-implementation-plan.md | 2 |
| docs/reviews/plans/US-002-plan-review.md | 2 (PASS) |
| docs/decisions/US-002-open-decisions.md | 1 (all 7 resolved at HUMAN_SPEC_APPROVAL) |

`implementation_verification` v1 verdict `PASS`, current — precondition
satisfied.

## 3. Security-Relevant Scope

One new public endpoint (`POST /api/v1/sessions`) that authenticates a
Customer and establishes an ASP.NET Core cookie session. Trust boundaries:
external client → `SessionsController` → `CustomerService.LoginAsync` →
`CustomerAuthService.AuthenticateAndSignInAsync` → `ICustomerRepository`/
`IPasswordHasher` → SQLite. Assets: password hash (read-only), email,
role, session cookie. This Story registers the project's cookie-auth
scheme's *configuration* for the first time (it was bare-registered by
US-001); no `[Authorize]`-protected resource exists yet to fully exercise
that configuration end to end.

## 4. Environment and Tools

.NET 8 / ASP.NET Core 8, SQLite (file-based dev, isolated in-memory test).
No IDE MCP server configured — `semantic_analysis: TEXT_FALLBACK` (direct
`Read`/`Grep`/`git diff` on every touched file, not symbol-graph
analysis). `runtime_checks: PARTIAL` — `dotnet build`/`dotnet test`
independently reproduced (56/56 pass); no new live smoke run performed by
this stage (reused `implementation_verification`'s evidence per this
Skill's own guidance to avoid redundant re-execution when nothing tracked
changed since it ran). No dependency-vulnerability scanner available or
run.

## 5. Authentication Review

- `CustomerAuthService` is confirmed (by direct read) the **only** caller
  of `SignInAsync` in the diff — matches SC-3.
- Cookie scheme configuration (`Program.cs`): `Cookie.HttpOnly = true`,
  `Cookie.SecurePolicy = CookieSecurePolicy.Always`,
  `Cookie.SameSite = SameSiteMode.Strict`, `SlidingExpiration = true`,
  externalized idle/absolute timeouts. `OnRedirectToLogin`/
  `OnRedirectToAccessDenied` overridden to `401`/`403` (OD-004:A) —
  confirmed present, correctly unconditional (no server-rendered login
  page exists in this project, so no legitimate redirect case is lost).
- **Not independently exercisable in this Story:** the challenge override
  and the absolute-cap rejection (`OnValidatePrincipal`) have no
  `[Authorize]` endpoint to observe against. Verified by code inspection
  only. This is the same, correctly disclosed limitation from
  `test_strategy` v1 §10 and `implementation_verification` v1 §12/§18 —
  not a new gap, carried forward, not re-litigated as a new finding.
- SC-4's global deny-by-default fallback policy (from US-001) is untouched
  by this diff (confirmed: no `git diff` hunk touches the
  `AddAuthorizationBuilder()`/`SetFallbackPolicy` lines).

## 6. Authorization Review

Not materially applicable — this Story adds no role- or ownership-checked
operation. `[AllowAnonymous]` on `SessionsController.Login` is the correct,
approved posture (SC-4 lists login as public alongside registration).

## 7. Password and Credential Handling

- Plaintext password exists only on `LoginRequest` (confirmed: no other
  type in the diff carries a `string Password`/similar property).
- Verification reuses `IPasswordHasher.Verify` (`BCrypt.Net-Next`) —
  **no new hashing mechanism introduced**, confirmed by `git diff`
  showing zero changes to `Security/BCryptPasswordHasher.cs`.
- **Timing-parity mechanism independently re-verified more precisely than
  `implementation_report`/`implementation_verification` described:**
  `CustomerAuthService.DummyHash` is generated via
  `BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())` — the
  **same** hashing call (and therefore the same default work factor) used
  to hash real customer passwords. This means the dummy-path `Verify` call
  costs the *same* computational work as a real-path `Verify` call, not
  merely "some comparable-magnitude work" — a stronger guarantee than
  documented elsewhere in this Story's artifacts. Recorded here as a
  Positive Control (§20), not merely accepted on faith.
- `AuthenticationFailedException`'s sealed, no-public-constructor design
  (re-confirmed by direct read) makes the anti-enumeration response
  uniformity a compiler-enforced invariant, independently the strongest
  security control observed in this review.
- No password appears in any log statement in the diff (`git diff` shows
  no new `logger.Log*` call referencing `request`/`password`).

## 8. Sensitive Data Exposure

`LoginResponse(long Id, string Email, string Role)` — no credential field
by construction (confirmed: type has exactly 3 properties, none
password/hash-shaped). `ErrorResponse` (reused, unmodified) carries no
credential field on any failure path either. `GlobalExceptionHandler`'s
`Message: exception.Message` for `AuthenticationFailedException` is safe
precisely because that type's message is fixed and generic (§7). No
debug/trace output observed in the diff.

## 9. Input Validation

`LoginRequestValidator`'s `NotEmpty()`-only rules are the approved,
deliberate design (Spec §6.1/§6.2) — re-confirmed correct: re-validating
format/policy at login would itself be a minor side channel (a `400` for
malformed input vs. a `401` for a merely-unmatched one could, in
principle, let an attacker distinguish "well-formed but wrong" from
"malformed" email guesses; not doing so is the *safer* choice, and this
Story's tests explicitly assert it, not just permit it by omission).

## 10. API Security

Matches `openapi.yaml` v1: no undocumented status code, no extra response
field beyond `id`/`email`/`role` observed in the live smoke transcript
(`implementation_report` v1 §5, independently spot-checked here for
absence of any field beyond the three documented ones). `[AllowAnonymous]`
correctly scoped to the one new action only.

## 11. Persistence Security

Not applicable — no schema/entity change (`git diff` confirms zero touch
to `Data/`, `Models/Entities/`, or `Migrations/`). Login only reads
`customer` via the pre-existing, unmodified repository method.

## 12. SQLite and Application Configuration

No database browser/admin UI introduced (unchanged from US-001). No new
connection string or profile. `Authentication:Cookie` config values are
non-secret operational parameters (name, two timeout numbers) — no
secret-management concern.

## 13. Logging and Telemetry

Spot-checked `docs/hooks/tool-usage.jsonl` for the specific credential-like
strings used in this Story's tests/manual smoke check (`Str0ng&Pass!word`,
`Correct1!Pass`, `smoke@example.com`) — **zero matches**, confirming the
harness's own tool-usage telemetry does not capture full sensitive tool
payloads for this Story's work, consistent with `AGENTS.md`'s "metadata
only" rule.

## 14. Dependencies

**No new dependency** in either `.csproj` (`git diff` on both files: no
change). Confirmed the approved-but-unused `Microsoft.Extensions.TimeProvider.Testing`
was correctly **not** added (`test_generation_report` v1's disclosed
decision, independently re-verified against the actual `.csproj`
contents). No vulnerability scan available/performed; no new dependency
means no new exposure from this Story regardless.

## 15. Security Test Coverage

`CustomerAuthServiceTests` (7 tests) directly assert the anti-enumeration
and timing-parity invariants at their source; `LoginSecurityPostureTests`
asserts the same invariants at the HTTP boundary, field-by-field excluding
only `timestamp`; `LoginApiTests` asserts no credential field appears on
success. Reviewed the test *code* (not just results): none of these tests
mocks away the property it claims to verify (§14 of
`implementation_verification` v1, independently re-confirmed by this
review's own read of the same files).

## 16. Abuse Case Review

| Scenario | Expected protection | Evidence | Status | Finding |
|---|---|---|---|---|
| Enumerate valid emails via response body/status | Identical `401` for all 3 failure causes | `AuthenticationFailedException`'s fixed message (structural); `LoginSecurityPostureTests` | Protected | None |
| Enumerate valid emails via response timing | Comparable-cost `Verify` call on every path | `DummyHash` uses the same `BCrypt.HashPassword` call as real hashes (§7) | Protected (mitigated at the algorithmic-cost level, not just call-count) | None |
| **Login CSRF** — attacker auto-submits a cross-site form/fetch with the attacker's own known credentials to plant the attacker's session cookie in the victim's browser | No cross-site request should reach the endpoint with attacker-controlled effect | (1) The endpoint requires `Content-Type: application/json` (enforced by the existing, unmodified `Program.cs` middleware that rejects non-JSON bodies with `415`) — a plain HTML `<form>` cannot set this content type. (2) No `AddCors`/`UseCors` is configured anywhere in `Program.cs` (confirmed absent) — a cross-origin `fetch()`/XHR using `application/json` triggers a CORS preflight that this API does not answer permissively, so browsers block the real request from ever being sent. | **Mitigated by pre-existing controls, not by anything new in this Story** | Informational — see §19 I-1: this reasoning was never documented anywhere (Spec, api-design, or prior reviews only reasoned "same posture as registration's OD-002", without this specific mechanism); recommend recording it in `security-conventions.md` so future Stories don't have to re-derive it. |
| Credential stuffing / brute force | Out of scope, explicitly (OD-005:A) | N/A | Accepted, per approved decision | None (already recorded) |
| Oversized request body (DoS-shaped) | N/A — no Story-specific control | Kestrel's default max-request-body-size applies; identical exposure already existed for `POST /api/v1/customers` since US-001 | Not this Story's introduction; repository-wide concern, out of this Skill's Story-scoped remit | None (out of scope, not newly introduced) |

## 17. Repository Hygiene

`git status --short` re-inspected: no secret-like file, no generated
`.db`/`.db-shm`/`.db-wal` file tracked or staged (`.gitignore` confirmed
to cover `**/App_Data/`, `*.db`, `*.db-shm`, `*.db-wal` — pre-existing,
unchanged). No credential-like string found in any new/modified file
(re-grepped this Story's diff for `password`/`secret`/`key` literals —
only found in variable/property names and doc comments, no literal
values).

## 18. Deviations

None from approved security requirements. The one process observation
(§16's login-CSRF reasoning was implicit, never explicit) is not a
deviation from an *approved* requirement — no requirement to document it
existed; it is a documentation-completeness recommendation, not a
violation.

## 19. Findings

| id | Severity | Category | Affected artifact | Observed | Expected | Risk | Required correction | Loop-back |
|---|---|---|---|---|---|---|---|---|
| M-1 (carried) | Minor | INPUT_VALIDATION | `CustomerPortal/Validation/LoginRequestValidator.cs` | `NotEmpty()` messages are OS-locale-dependent | English messages per `api-conventions.md` AC-6 examples | Low — cosmetic, no data exposure | None required this Story; confirmed identical pre-existing US-001 behavior, out of scope. Candidate for a future cross-cutting Story. | None (no action) |
| M-2 (new) | Minor | AUTHENTICATION | `docs/architecture/security-conventions.md` | SC-5 only mandates `SameSite`; `HttpOnly`/`Secure` (both correctly set) are not independently required by any written convention | SC-5 should state the full cookie-attribute baseline explicitly | Low — this Story's actual configuration is *more* secure than the written minimum, not less | Recommend a future `security-conventions.md` update to state `HttpOnly`/`Secure` explicitly (same recommendation `design-review` v1 M-1 already made; repeated here as confirmed still outstanding, not re-counted as a second distinct defect) | None (documentation only) |
| I-1 (new) | Informational | API_SECURITY | `docs/architecture/security-conventions.md` / `docs/designs/api/US-002-api-design.md` | Login-CSRF risk was never explicitly analyzed anywhere in this Story's artifacts (only reasoned by analogy to registration's CSRF exemption); this review independently found and confirmed the actual mitigating mechanism (`Content-Type: application/json` enforcement + no CORS) | A security-sensitive reasoning chain like this should be written down once, not left to be re-derived by each reviewer | None currently (the mitigation is real and independently verified) | Recommend `security-conventions.md` record this JSON-content-type + no-CORS rationale as the project's general login/mutation-endpoint CSRF posture, so it does not depend on informal analogy in future Stories | None (documentation only) |
| I-2 (carried) | Informational | AUTHENTICATION | `CustomerPortal/Program.cs` | `OnValidatePrincipal`'s absolute-cap rejection and the `OnRedirectToLogin`/`OnRedirectToAccessDenied` overrides have no live/automated exercise in this Story (no `[Authorize]` endpoint exists yet) | Confirmed by code reading only | Low today (no protected resource to misuse); becomes relevant the moment US-003 adds one | Recommend `US-003`'s own `IMPLEMENTATION_VERIFICATION`/`SECURITY_REVIEW` explicitly exercise both mechanisms live, since that Story is what finally makes them observable | None (forward-looking note for the next Story) |

No Critical or Major finding.

## 20. Positive Controls

- Anti-enumeration response uniformity is a **compiler-enforced** invariant
  (`AuthenticationFailedException`'s sealed, argument-less design), not a
  convention that could silently regress.
- Timing-parity dummy hash uses the *identical* hashing call as real
  password hashes — same algorithmic cost, not just "a similarly-shaped
  operation."
- `SignInAsync` is called from exactly one place in the entire codebase
  (confirmed by `Grep` across `CustomerPortal/`), matching SC-3 exactly.
- Cookie: `HttpOnly`, `Secure` (`Always`), `SameSite=Strict`, externalized
  lifetime — exceeds the written minimum in `security-conventions.md`.
- No new dependency, no new CORS policy, no new database exposure, no new
  logging of sensitive data.
- Login CSRF independently analyzed and found mitigated by an existing,
  unrelated control (JSON content-type enforcement + absence of CORS) —
  verified, not assumed.

## 21. Open Decisions

No blocking security Open Decisions were identified. All 7 Story-level
Open Decisions were resolved at `HUMAN_SPEC_APPROVAL`; none of the
findings in §19 rises to a decision that must block progression — both new
items (M-2, I-1) are recommendations to strengthen written documentation
of already-correct behavior, not gaps in the behavior itself.

## 22. Review Limitations

- No IDE MCP / semantic analysis tool available; all findings are from
  direct file reads and `Grep`/`git diff`, disclosed per finding where it
  matters (e.g. "confirmed by Grep across `CustomerPortal/`" is a
  text-search claim, not a guaranteed-exhaustive symbol-reference count).
- No dependency-vulnerability database scan performed (none was needed —
  no new dependency).
- `OnValidatePrincipal`/challenge-override runtime behavior verified by
  code reading only, not a live authenticated request against a protected
  resource (none exists yet in this Story) — disclosed, not a hidden gap.
- Did not re-run the live manual smoke check performed by
  `implementation_report`/`implementation_verification`; reused their
  transcripts as this stage's runtime evidence since nothing tracked
  changed since they ran.

## 23. Verdict Rationale

`PASS`. 0 Critical, 0 Major. The Story's core security property
(anti-enumeration) is independently confirmed as a genuinely strong,
structural control — stronger than its own documentation claimed in one
respect (timing-parity uses matched algorithmic cost, not just a call-count
guarantee). A new abuse case (login CSRF) was analyzed from first
principles rather than accepted by analogy, and found already mitigated by
an existing, verified control. All findings are Minor/Informational,
none blocking, and all point to documentation improvements rather than
behavioral defects. Recommend `RECONCILIATION`.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-002
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-002-security-review.md
  next_stage: RECONCILIATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "M-1 (carried, Minor): LoginRequestValidator NotEmpty() messages are OS-locale-dependent -- confirmed identical pre-existing US-001 behavior, not this Story's introduction, not corrected."
    - "M-2 (new, Minor): security-conventions.md SC-5 doesn't independently mandate HttpOnly/Secure (both correctly set anyway) -- same as design-review v1 M-1, confirmed still outstanding as a documentation gap only."
    - "I-1 (new, Informational): analyzed login-CSRF from first principles (not by analogy) -- found mitigated by the existing Content-Type: application/json enforcement + absence of any CORS policy, verified by direct Program.cs read. This specific reasoning had never been documented anywhere in the Story's artifacts; recommend recording it in security-conventions.md as the project's general JSON-API CSRF posture."
    - "I-2 (carried, Informational): OnValidatePrincipal absolute-cap and the 401/403 challenge overrides remain code-reviewed-only, no live exercise possible until US-003 adds the first [Authorize] endpoint -- forward note for that Story's own verification/security review."
    - "Positive control confirmed more precisely than documented elsewhere: the timing-parity dummy hash uses the identical BCrypt.HashPassword call as real customer hashes (same work factor), not merely a comparable-magnitude operation."
```
