---
artifact_type: traceability
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T14:48:41Z
updated_at: 2026-09-02T14:48:41Z
produced_by: reconciliation-reviewer
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/impact-analysis/US-002-impact-analysis.md
    version: 1
  - path: docs/plans/US-002-implementation-plan.md
    version: 2
  - path: docs/evidence/US-002-implementation-report.md
    version: 1
  - path: docs/verification/US-002-implementation-verification.md
    version: 1
  - path: docs/reviews/security/US-002-security-review.md
    version: 1
supersedes: null
---

# Acceptance Criteria → End-to-End Traceability — US-002 Customer Login (v1)

| AC | Story text | Spec ref | Design ref | Plan step | Implementation | Test | Verification | Security | Status |
|---|---|---|---|---|---|---|---|---|---|
| AC-001 | Successful Login | Spec §5 AC-001, FR-1/4/5/6/8/9 | openapi `login` 200; api-design §4.2 | Steps 4,5,6,7,8 | `SessionsController.Login`, `CustomerService.LoginAsync`, `CustomerAuthService.AuthenticateAndSignInAsync` | `LoginApiTests.PostSessions_ValidCredentials_*`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_ValidCredentials_*` | VERIFIED | Reviewed §5/§7 | RECONCILED |
| AC-002 | Invalid Password | Spec §5 AC-002, FR-5/FR-7 | openapi `login` 401 | Steps 1,2,4 | `CustomerAuthService` (`!passwordMatches` branch), `AuthenticationFailedException` | `LoginApiTests.PostSessions_WrongPassword_*`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_WrongPassword_*` | VERIFIED | Reviewed §7/§16 | RECONCILED |
| AC-003 | Unknown Account | Spec §5 AC-003, FR-4/FR-7 | openapi `login` 401 (identical) | Steps 1,2,4 | `CustomerAuthService` (`customer is null` branch, `DummyHash`) | `LoginApiTests.PostSessions_UnknownEmail_*`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_UnknownEmail_*` (2) | VERIFIED | Reviewed §7/§16 (timing parity independently re-confirmed) | RECONCILED |
| AC-004 | Disabled Account | Spec §5 AC-004, FR-6/FR-16 | openapi `login` 401 (identical), OD-003:A | Steps 1,2,4 | `CustomerAuthService` (`!customer.Enabled` branch) | `LoginApiTests.PostSessions_DisabledAccountWithCorrectPassword_*`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_DisabledAccountWithCorrectPassword_*` | VERIFIED | Reviewed §16 | RECONCILED |
| AC-005 | Secure Authentication Response | Spec §5 AC-005, FR-10 | `LoginResponse` schema (openapi) | Step 5 | `Models/Dtos/LoginResponse.cs` (no credential field) | `LoginApiTests.PostSessions_SuccessfulLogin_ResponseBodyContainsNoPasswordOrHash` | VERIFIED | Reviewed §8 | RECONCILED |
| AC-006 | (derived, AC-2) media type | Spec §5 AC-006 | openapi `login` 415 | (reused Program.cs middleware) | Unmodified existing middleware | `LoginApiTests.PostSessions_MissingContentType_Returns415` | VERIFIED | N/A (unchanged control) | RECONCILED |
| AC-007 | (derived) request-shape enforcement | Spec §5 AC-007, FR-3 | openapi `login` 400 | Step 6 | `Validation/LoginRequestValidator.cs` | `LoginRequestValidatorTests` (5), `LoginApiTests.PostSessions_Missing{Email,Password}_*` | VERIFIED | Reviewed §9 | RECONCILED |

7/7 Acceptance Criteria RECONCILED. Additionally traced (not a Story AC, a
plan-mandated invariant spanning AC-002/003/004): byte-for-byte-identical
(excl. `timestamp`) failure response — `AuthenticationFailedException`'s
fixed-message constructor (impact-analysis R-1) — VERIFIED /
independently re-confirmed by both `implementation_verification` v1 and
`security_review` v1.

## Open Decisions applied (all resolved at HUMAN_SPEC_APPROVAL, 2026-09-02T13:45:47Z)

| OD | Resolution | Traced to |
|---|---|---|
| OD-001 | A — `POST /api/v1/sessions` | `SessionsController` route |
| OD-002 | A — `200 OK`, `{id,email,role}` | `LoginResponse`, api-design §3 (explicit AC-4 exception) |
| OD-003 | A — disabled account joins the uniform 401 | `CustomerAuthService`, FR-16 |
| OD-004 | A — 401/403 challenge override, no redirect | `Program.cs` `OnRedirectToLogin`/`OnRedirectToAccessDenied` |
| OD-005 | A — anti-abuse out of scope | No rate-limit code; §10 Out of Scope |
| OD-006 | A — 30min idle / 8h absolute cookie lifetime | `appsettings.json` `Authentication:Cookie`, `Program.cs` `OnValidatePrincipal` |
| OD-007 | A — logout out of scope | No logout endpoint |

## Non-blocking findings carried through the full chain

- FR-11/OD-004's challenge override and the absolute-cookie-cap logic have
  no live/automated exercise in this Story (no `[Authorize]` endpoint
  exists yet) — consistently disclosed by `test_strategy` v1 §10,
  `implementation_verification` v1 §12/§18, and `security_review` v1 I-2.
  Forward note for **US-003**.
- `LoginRequestValidator`'s `NotEmpty()` messages are OS-locale-dependent —
  consistently disclosed as identical, pre-existing US-001 behavior by
  `implementation_report` v1 §7, `implementation_verification` v1 M-1, and
  `security_review` v1 M-1. Not corrected, out of this Story's scope.
- `security-conventions.md` SC-5 doesn't independently mandate
  `HttpOnly`/`Secure` (both correctly set anyway) — `design_review` v1 M-1
  and `security_review` v1 M-2, same underlying documentation gap named
  twice across two stages, not two separate defects.
- Login-CSRF risk analyzed and found mitigated by the pre-existing
  `Content-Type: application/json` enforcement + absence of CORS —
  `security_review` v1 I-1, newly documented in this delivery cycle.
