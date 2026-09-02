---
artifact_type: test_strategy
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T14:24:46Z
updated_at: 2026-09-02T14:24:46Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-002-customer-login.md
    version: null
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/impact-analysis/US-002-impact-analysis.md
    version: 1
  - path: docs/plans/US-002-implementation-plan.md
    version: 2
  - path: docs/reviews/plans/US-002-plan-review.md
    version: 2
supersedes: null
---

# Test Strategy — US-002 Customer Login (v1)

## 1. Scope

Cover every Acceptance Criterion of US-002 (AC-001..AC-007, Specification
v2 §5) across unit, integration, and security levels, per
`implementation_plan` v2's Testing Strategy section. No persistence-level
tests are added — `DB_DESIGN`/this Story's schema impact is
`NOT_APPLICABLE` (login is read-only against the existing `customer`
table). No test targets out-of-scope behavior (logout, rate limiting, MFA,
OAuth, password recovery, profile management — Spec §10).

## 2. Selected Test Levels

| Level | Target | Rationale |
|---|---|---|
| Unit | `CustomerAuthService` (`Security`) | isolate the anti-enumeration decision logic (unknown email / wrong password / disabled account → identical exception) and the timing-parity dummy hash-verify call (impact-analysis R-2), without a full ASP.NET Core host — `SignInAsync` is exercised via a fake `IAuthenticationService` resolved from a `DefaultHttpContext`'s `RequestServices` |
| Unit | `ICustomerService.LoginAsync` (`Services`) | isolate the Controller-facing entry point's own responsibility (delegate to `ICustomerAuthService`, map `Customer` → `LoginResponse`) added by plan-review v1 F-1's fix, independent of the authentication decision itself |
| Unit | `LoginRequestValidator` | isolate FluentValidation's non-blank-only rules (Spec §6.1/§6.2's deliberate no-re-validation decision) from the HTTP pipeline |
| Integration | `POST /api/v1/sessions` via `WebApplicationFactory<Program>` (reusing US-001's `TestWebApplicationFactory`, referenced across the `CustomerPortal.Tests.Login` namespace, not relocated) | exercise the real pipeline end to end: routing, model binding, `CustomerService.LoginAsync` → `CustomerAuthService`, `GlobalExceptionHandler`'s new `401` mapping, the cookie actually issued |
| Security | HTTP round-trip, byte-for-byte (excluding `timestamp`) response comparison across all three failure causes | the Story's single most security-critical observable behavior (SC-3, OD-003:A) |

No new mocking-library question: US-001 already settled on hand-written
fakes; this Story reuses that decision (`FakeCustomerRepository`,
`FakePasswordHasher`, plus new `FakeAuthenticationService` and
`FakeCustomerAuthService`, all hand-written, no new production/test
dependency).

**Fake-time-provider dependency (implementation-plan v2 Open Question 2,
resolved `add-fake-time-provider:yes` at `HUMAN_PLAN_APPROVAL`):** not
added by this stage. See §10 and §11 — the approved test (the absolute
8-hour cookie-lifetime cap) turned out to have no executable target within
this Story's scope once test design began; see the excluded-scenario
justification below rather than a placeholder test built only to consume
the approved dependency.

## 3. Positive Scenarios

- Valid email + correct password for an enabled account → `200`,
  `Set-Cookie` present (`HttpOnly`, `SameSite=Strict`), body
  `{ id, email, role }`, no credential field (AC-001, AC-005).
- Email compared case-insensitively at lookup, mirroring registration's
  normalization (BR-002) — `CustomerAuthService` itself lowercases the
  submitted value before calling `ICustomerRepository.FindByEmailAsync`
  (the repository does an exact match, same as US-001's).
- Successful sign-in carries the account's actual role claim (`CUSTOMER`
  or `ADMIN`), not a hard-coded value (unlike registration, which is
  always `CUSTOMER`) (SC-2).

## 4. Negative Scenarios

- Wrong password for a known, enabled account → uniform `401` (AC-002).
- Unknown email → the **identical** `401` as AC-002 (AC-003).
- Correct password for a known but **disabled** account → the **identical**
  `401` as AC-002/AC-003 (AC-004, OD-003:A).
- Missing/blank `email` or `password` → `400` with the corresponding
  `fieldErrors[].field` (AC-007).
- Missing/wrong `Content-Type` → `415` (AC-006).
- Malformed-but-present email value (e.g. `not-an-email`) → treated as an
  authentication failure (`401`), **not** a `400` — this is the deliberate
  no-re-validation decision (Spec §6.1) and is explicitly asserted, not
  merely assumed.

## 5. Boundary Scenarios

None specific to this Story beyond what US-001 already covers for `email`/
`password` shape (registration owns those boundaries; login intentionally
does not re-check them — see §4's malformed-email case, which is the
Story-specific boundary that matters here: presence vs. format).

## 6. Validation Scenarios

- `LoginRequestValidator`'s two rules (`email` non-blank, `password`
  non-blank) tested in isolation (`Validate()` called directly).
- Explicitly asserted (not just absence-of-failure): a malformed email and
  a policy-violating-length password are **both still valid** at the
  `LoginRequestValidator` level — pinning the deliberate absence of format/
  policy re-validation as a tested contract, not an accidental gap.

## 7. Security Scenarios

- **Anti-enumeration (the Story's core security property):** unknown
  email, wrong password, and disabled account produce responses that are
  identical in status code, `error`, `message`, `path`, and
  `fieldErrors`-presence — the only field excluded from comparison is
  `timestamp` (necessarily unique per request, `api-conventions.md` AC-6).
  Additionally verified: none of the three failure responses carries a
  `Set-Cookie` header.
- **Timing-parity mechanism (impact-analysis R-2):** unit-tested directly —
  `IPasswordHasher.Verify` is called exactly once even when no account is
  found for the submitted email (a spy fake counts invocations). This is
  the code-level guarantee; a wall-clock timing assertion is deliberately
  **not** attempted (timing tests are inherently flaky — the plan itself
  recommends against a strict equality assertion on real elapsed time).
- **Message-uniformity guarantee (impact-analysis R-1):** unit-tested
  directly — all three `CustomerAuthService` failure branches are asserted
  to throw `AuthenticationFailedException` instances with the exact same
  `Message` string.
- Successful-login response body contains neither the submitted plaintext
  password nor any `passwordHash`-shaped value (AC-005).
- Role claim carries the exact stored value (`CUSTOMER`/`ADMIN`), asserted
  via the fake `IAuthenticationService`'s captured `ClaimsPrincipal`
  (`ClaimTypes.Role`).

## 8. Persistence Scenarios

None — `DB_DESIGN` `NOT_APPLICABLE` for this Story (re-confirmed by
`impact_analysis` v1 against the actual `Customer` entity: `Enabled`
already exists, no schema change). Login is read-only; no new persistence
test is needed beyond what US-001's `CustomerPersistenceTests` already
covers for the `customer` table.

## 9. Required Fixtures

- `TestWebApplicationFactory` (US-001, `CustomerPortal.Tests.Registration`)
  — reused as-is via a cross-namespace `using`, not relocated or modified.
  Its content is generic (`WebApplicationFactory<Program>` + isolated
  in-memory SQLite) despite its folder name.
- Hand-written `FakeCustomerRepository` / `FakePasswordHasher` (local to
  `CustomerAuthServiceTests`, mirroring US-001's pattern of per-file
  private fakes rather than a shared test-utility class).
- New hand-written `FakeAuthenticationService : IAuthenticationService` —
  captures the `scheme`/`ClaimsPrincipal` passed to `SignInAsync`; every
  other member throws `NotSupportedException` (not exercised by
  `CustomerAuthService`).
- New hand-written `FakeCustomerAuthService : ICustomerAuthService` (local
  to `CustomerServiceLoginTests`) — a simple succeed/fail double, isolating
  `CustomerService.LoginAsync`'s own mapping responsibility from the real
  authentication decision.
- `AppDbContext` access via `factory.Services.CreateScope()` to directly
  flip a seeded account's `Enabled` flag to `false` for the disabled-account
  scenarios (no admin/disable endpoint exists — this is the only way to
  reach that state deterministically).

## 10. Excluded Scenarios (with justification)

- **Absolute cookie-lifetime cap (`OnValidatePrincipal`, OD-006:A) —
  excluded from this stage's automated tests, despite the approved
  `Microsoft.Extensions.TimeProvider.Testing` dependency
  (Open Question 2, approved `yes`).** Discovered during test design, not
  assumed: this Story adds no `[Authorize]`-protected endpoint (login
  itself is `[AllowAnonymous]`; api-design.md Q-2 / SPEC_REVIEW v2 M-1
  already flagged this same root cause for the OD-004 challenge-behavior
  test). Without a protected resource to call after time-travelling the
  fake clock forward 8 hours, there is nothing observable at the HTTP level
  to assert a "rejected" outcome against — `OnValidatePrincipal`'s effect
  is only visible on the *next* authenticated request, and none exists
  yet. Writing a unit test would require `IMPLEMENTATION`/
  `IMPLEMENTATION_PLANNING` to first extract the absolute-cap comparison
  into a directly testable, named component (e.g. a small pure
  method/class) — that is an implementation-shape decision this stage is
  not authorized to invent unilaterally (test-writer must not resolve
  architectural decisions). **Recommendation, not a decision:** either (a)
  `IMPLEMENTATION` extracts a directly unit-testable comparison for
  immediate coverage now, or (b) the first genuine end-to-end test for
  this logic is added when **US-003** (Profile View) introduces the first
  `[Authorize]` endpoint. The `Microsoft.Extensions.TimeProvider.Testing`
  package is **not added** by this stage since there is no test to write
  against it yet; adding an unused test dependency was judged worse than
  reporting the gap honestly. This does not block `TEST_WRITING`'s `PASS`
  verdict — it is not tied to any Acceptance Criterion.
- Concurrent/parallel login attempts, brute-force/rate-limiting behavior —
  explicitly out of scope (OD-005:A).
- Logout — explicitly out of scope (OD-007:A); no test targets a
  non-existent endpoint.
- Cookie-auth `401`/`403` challenge override (OD-004:A) on a **protected**
  endpoint — same root cause as the absolute-cap exclusion above (no
  protected endpoint exists in this Story); `Program.cs`'s configuration of
  this is verified by code review at `IMPLEMENTATION_VERIFICATION`, not by
  an automated test here (api-design.md Q-2, carried).

## 11. Known Limitations

- **Red-phase tests do not compile yet, by necessity**, same as US-001's
  precedent: `LoginRequest`, `LoginResponse`, `ICustomerAuthService`/
  `CustomerAuthService`, `AuthenticationFailedException`,
  `LoginRequestValidator`, `SessionsController`, and
  `ICustomerService.LoginAsync` do not exist in the repository yet.
  **Unlike US-001** (a from-scratch project with zero pre-existing tests),
  this Story's compile failure affects the **entire** `CustomerPortal.Tests`
  project, including US-001's previously-passing regression tests — a
  single C# project compiles as one unit, so no test in the assembly can
  currently execute. This is the expected and only possible red state, not
  a regression: once `IMPLEMENTATION` adds the missing production symbols,
  US-001's existing tests are expected to pass again unchanged (their own
  source was not modified, only `CustomerServiceTests.cs`'s constructor
  call sites, which required a mechanical, non-behavioral update — see
  `test_generation_report` v1 §4).
- Only 3 distinct `CS0246` errors were reported by the compiler in this
  pass even though more not-yet-existing symbols are referenced (e.g.
  `LoginRequest`, `CustomerAuthService`, `AuthenticationFailedException`);
  Roslyn appears to suppress some cascading errors once an earlier
  unresolved symbol breaks a file's semantic binding. Additional errors are
  expected to surface incrementally as `IMPLEMENTATION` adds each missing
  piece — this is normal and does not indicate anything was skipped; the
  full set of assumed symbols is listed explicitly in
  `test_generation_report` v1 §6 regardless of what the compiler currently
  surfaces.
- No IDE MCP server is configured for this .NET track; all evidence comes
  from direct `dotnet build` invocations, not semantic analysis.

## 12. Open Decisions Affecting Testing

None remaining. All 7 Open Decisions (`OD-001`–`OD-007`) were resolved at
`HUMAN_SPEC_APPROVAL`. `implementation_plan` v2's two Open Questions were
both resolved at `HUMAN_PLAN_APPROVAL`: Open Question 1 (`HttpContext`
parameter) confirmed A; Open Question 2 (fake-time-provider dependency)
approved `yes` but, per §10 above, has no executable target within this
Story and was not exercised.
