---
artifact_type: implementation_report
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T14:34:17Z
updated_at: 2026-09-02T14:34:17Z
produced_by: aspnet-implementor
inputs:
  - path: docs/plans/US-002-implementation-plan.md
    version: 2
  - path: docs/reviews/plans/US-002-plan-review.md
    version: 2
  - path: docs/tests/US-002-ac-test-matrix.md
    version: 1
  - path: docs/tests/US-002-test-strategy.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/specifications/US-002-spec.md
    version: 2
supersedes: null
tests_status: PASS
build_status: PASS
diagnostics_status: PASS
security_sensitive: true
attempt: 1
---

# Implementation Report — US-002 Customer Login (v1)

## 1. Summary

Implemented `POST /api/v1/sessions` end to end, exactly per
`implementation_plan` v2's 11 steps, against the existing US-001 codebase.
All 24 red-phase tests from `TEST_WRITING` now pass, alongside all 32
pre-existing US-001 tests (56/56 total, 0 failures). Build clean, 0
warnings. `dotnet format --verify-no-changes` clean. A live manual smoke
check confirmed every documented status code and the exact `Set-Cookie`
attributes on a running instance. No Acceptance Criterion, contract, or
approved design was reinterpreted.

## 2. Source Artifacts

| Artifact | Version |
|---|---|
| docs/specifications/US-002-spec.md | 2 |
| docs/reviews/specifications/US-002-spec-review.md | 2 (PASS) |
| docs/designs/api/US-002-api-design.md, -openapi.yaml | 1 |
| docs/reviews/designs/US-002-design-review.md | 1 (PASS) |
| docs/impact-analysis/US-002-impact-analysis.md | 1 (PASS) |
| docs/plans/US-002-implementation-plan.md | 2 |
| docs/reviews/plans/US-002-plan-review.md | 2 (PASS) |
| docs/tests/US-002-test-strategy.md | 1 |
| docs/tests/US-002-ac-test-matrix.md | 1 |
| docs/evidence/US-002-test-generation-report.md | 1 |
| docs/decisions/US-002-open-decisions.md | 1 (all 7 resolved at HUMAN_SPEC_APPROVAL) |

`HUMAN_PLAN_APPROVAL` recorded 2026-09-02T14:15:06Z (`workflow-state.yaml`
history), comment `OD-HttpContext:A, add-fake-time-provider:yes`.

## 3. Implemented Acceptance Criteria

| AC | Implementation location | Test | Status |
|---|---|---|---|
| AC-001 | `Controllers/SessionsController.Login`, `Services/CustomerService.LoginAsync`, `Security/CustomerAuthService.AuthenticateAndSignInAsync` | `LoginApiTests.PostSessions_ValidCredentials_Returns200WithSessionCookieAndSafeBody`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_ValidCredentials_SignsInWithCorrectRoleClaimAndReturnsCustomer` | PASS |
| AC-002 | `CustomerAuthService` (wrong-password branch), `Exceptions/AuthenticationFailedException`, `GlobalExceptionHandler` | `LoginApiTests.PostSessions_WrongPassword_Returns401WithNoCookie`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_WrongPassword_ThrowsAuthenticationFailedException` | PASS |
| AC-003 | `CustomerAuthService` (unknown-email branch, dummy hash-verify) | `LoginApiTests.PostSessions_UnknownEmail_Returns401`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_UnknownEmail_*` (2 tests) | PASS |
| AC-004 | `CustomerAuthService` (disabled-account branch) | `LoginApiTests.PostSessions_DisabledAccountWithCorrectPassword_Returns401`, `CustomerAuthServiceTests.AuthenticateAndSignInAsync_DisabledAccountWithCorrectPassword_ThrowsAuthenticationFailedException` | PASS |
| AC-002/003/004 (uniformity) | `AuthenticationFailedException`'s fixed-message constructor | `CustomerAuthServiceTests.AuthenticateAndSignInAsync_AllThreeFailureCases_ThrowExceptionWithIdenticalMessage`, `LoginSecurityPostureTests.PostSessions_UnknownEmailWrongPasswordAndDisabledAccount_ProduceIndistinguishableResponses` | PASS |
| AC-005 | `Models/Dtos/LoginResponse` (no credential field), `CustomerService.LoginAsync` mapping | `LoginApiTests.PostSessions_SuccessfulLogin_ResponseBodyContainsNoPasswordOrHash` | PASS |
| AC-006 | Existing `Program.cs` content-type middleware (unmodified, reused) | `LoginApiTests.PostSessions_MissingContentType_Returns415` | PASS |
| AC-007 | `Validation/LoginRequestValidator` | `LoginApiTests.PostSessions_Missing{Email,Password}_Returns400With*FieldError`, `LoginRequestValidatorTests` (5 tests) | PASS |

## 4. Change Set

### Files Created (Planned)

| File | Plan step |
|---|---|
| `CustomerPortal/Exceptions/AuthenticationFailedException.cs` | Step 1 |
| `CustomerPortal/Security/ICustomerAuthService.cs` | Step 4 |
| `CustomerPortal/Security/CustomerAuthService.cs` | Step 4 |
| `CustomerPortal/Models/Requests/LoginRequest.cs` | Step 5 |
| `CustomerPortal/Models/Dtos/LoginResponse.cs` | Step 5 |
| `CustomerPortal/Validation/LoginRequestValidator.cs` | Step 6 |
| `CustomerPortal/Controllers/SessionsController.cs` | Step 8 |

### Files Modified (Planned)

| File | Plan step | Change |
|---|---|---|
| `CustomerPortal/Exceptions/GlobalExceptionHandler.cs` | Step 2 | Added `AuthenticationFailedException => (401, "Unauthorized")` switch arm |
| `CustomerPortal/appsettings.json` | Step 3 | Added `Authentication:Cookie:{Name, IdleTimeoutMinutes, AbsoluteTimeoutHours}` |
| `CustomerPortal/Services/ICustomerService.cs` | Step 7 | Added `Task<LoginResponse> LoginAsync(HttpContext, LoginRequest)` |
| `CustomerPortal/Services/CustomerService.cs` | Step 7 | Added `ICustomerAuthService` constructor parameter; implemented `LoginAsync` (delegates + maps entity → DTO, mirroring `RegisterAsync`) |
| `CustomerPortal/Program.cs` | Step 9 | Configured the already-registered `.AddCookie()` call: `Cookie.Name`/`HttpOnly`/`SecurePolicy`/`SameSite=Strict` from config, `SlidingExpiration`+`ExpireTimeSpan` (idle, from config), `Events.OnRedirectToLogin`/`OnRedirectToAccessDenied` → 401/403, `Events.OnValidatePrincipal` → absolute-cap rejection via a DI-resolved `TimeProvider`; registered `ICustomerAuthService`/`CustomerAuthService` and `TimeProvider.System` |

### Test Files Created/Modified

Owned by `test-writer` (`TEST_WRITING` stage); listed here for completeness,
not re-authored by this Skill: `CustomerPortal.Tests/Validation/LoginRequestValidatorTests.cs`,
`CustomerPortal.Tests/Security/CustomerAuthServiceTests.cs`,
`CustomerPortal.Tests/Security/LoginSecurityPostureTests.cs`,
`CustomerPortal.Tests/Services/CustomerServiceLoginTests.cs`,
`CustomerPortal.Tests/Login/LoginApiTests.cs` (created);
`CustomerPortal.Tests/Services/CustomerServiceTests.cs` (modified, disclosed
non-behavioral fixture update — see `test_generation_report` v1 §3).

### Required Supporting Change

None beyond what §4's tables already list — every file touched was
explicitly named in `implementation_plan` v2's Files To Create/Modify.

### Unexpected / Unrelated

**One accidental artifact created and removed during this Skill's own
manual smoke-check step, disclosed here:** a stray
`CustomerPortal/docs/hooks/tool-usage.jsonl` (6 lines) was created because
a shell command was run with the working directory temporarily changed to
`CustomerPortal/` during the live smoke test (§5), causing the harness's
own tool-usage telemetry hook to write to a relative path under that
directory instead of the repository root's `docs/hooks/tool-usage.jsonl`
(514 lines, correct, untouched). Deleted immediately upon discovery, before
this report was written; confirmed absent from `git status` below. Not a
production or test file; no Story content was lost (only 6 duplicate
telemetry lines, harmless).

No other unrelated or unexpected file appears in the change set.

## 5. Validation Evidence

**Build:**
```
$ dotnet build
CustomerPortal -> .../CustomerPortal.dll
CustomerPortal.Tests -> .../CustomerPortal.Tests.dll
Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.
```

**Full test suite:**
```
$ dotnet test
Пройден!   : не пройдено 0, пройдено 56, пропущено 0, всего 56, длительность 2 s.
```
56/56 pass: all 24 new US-002 tests (previously RED at compile time per
`test_generation_report` v1) now pass, and all 32 pre-existing US-001
tests continue to pass unchanged.

**Formatting:**
```
$ dotnet format --verify-no-changes
(no output, exit code 0)
```

**Live manual smoke check** (`dotnet run` on `http://localhost:5299`,
`--no-launch-profile`, `ASPNETCORE_ENVIRONMENT=Development`), against a
fresh local SQLite file, superseding-but-consistent-with the automated
tests above — run specifically to inspect raw response headers the
automated `HttpClient`-based tests already assert on programmatically, as
an independent confirmation:

| Request | Result |
|---|---|
| `POST /api/v1/customers` (register `smoke@example.com`) | `201`, body has `role: "CUSTOMER"` |
| `POST /api/v1/sessions` valid credentials | `200`; `Set-Cookie: CustomerPortal.Auth=...; path=/; secure; samesite=strict; httponly`; body `{"id":1,"email":"smoke@example.com","role":"CUSTOMER"}` — no credential field |
| `POST /api/v1/sessions` wrong password | `401`, `{"status":401,"error":"Unauthorized","message":"Invalid email or password.","path":"/api/v1/sessions","fieldErrors":null}` (only `timestamp` differs from the unknown-email case below) |
| `POST /api/v1/sessions` unknown email | `401`, identical body shape/content to the wrong-password case above (verified byte-for-byte except `timestamp`) |
| `POST /api/v1/sessions` missing `Content-Type` | `415`, `"Content-Type 'application/x-www-form-urlencoded' is not supported."` |
| `POST /api/v1/sessions` missing `password` | `400`, `fieldErrors: [{"field":"password", ...}]` |
| `GET /swagger/v1/swagger.json` | confirmed `"/api/v1/sessions"` present in the generated OpenAPI document |

No dedicated persistence/contract-test tooling run beyond `dotnet test`
(no schema change this Story, `DB_DESIGN` `NOT_APPLICABLE`, re-confirmed —
no migration exists or was generated).

## 6. Configuration Changes

`appsettings.json` (Files Modified table, Step 3): added
`Authentication:Cookie:Name = "CustomerPortal.Auth"`,
`IdleTimeoutMinutes = 30`, `AbsoluteTimeoutHours = 8`, per
`implementation_plan` v2 Execution Order Step 3 (OD-006:A, concretized by
`API_DESIGN`/`IMPLEMENTATION_PLANNING` within its approved 30–60min/8–12h
range). No change to `ConnectionStrings:Default` or
`Persistence:AutoMigrate`. No new environment/profile.

## 7. Deviations and Discovered Problems

- **Confirmed pre-existing, not a regression:** `LoginRequestValidator`'s
  `NotEmpty()` field-error messages render in the server's OS locale (e.g.
  "'Password' должно быть заполнено." on this Russian-locale machine)
  because no explicit `.WithMessage(...)` override is set — discovered
  during the live smoke check (§5), not by the automated tests (which only
  assert `field == "password"`, not message text). Verified this is
  **identical, already-shipped behavior** in US-001's own
  `RegistrationRequestValidator`'s `NotEmpty()` rules (reproduced live:
  `POST /api/v1/customers` with a blank email produces the same localized
  message). Since `LoginRequestValidator` intentionally mirrors that exact
  pattern and the localization behavior is pre-existing (not introduced,
  not worsened, not specific to this Story), **no change was made** — fixing
  it would be an opportunistic, out-of-scope correction to
  already-approved, already-shipped US-001 behavior. Flagged here for
  `IMPLEMENTATION_VERIFICATION`/`SECURITY_REVIEW` awareness and as a
  candidate for a future cross-cutting Story if the human wants
  English-only error messages guaranteed regardless of server locale.
- **api-design.md Q-1 simplification, already recorded in the approved
  plan, confirmed as implemented exactly as planned:** the absolute
  cookie-lifetime cap uses `AuthenticationProperties.IssuedUtc` (already
  present on the ticket) rather than a custom claim, and compares against a
  DI-resolved `TimeProvider` (not `DateTimeOffset.UtcNow` directly) —
  confirmed present in `Program.cs` exactly as `implementation_plan` v2
  Architectural Changes item 4 specified.
- **One accidental, immediately-remediated artifact** — see §4's
  Unexpected/Unrelated subsection.
- No other deviation from `implementation_plan` v2. All 11 steps executed
  in the planned order; no step was skipped, reordered, or found
  infeasible against repository reality.

## 8. Open Decisions

None touched or newly required. All 7 Story-level Open Decisions
(`OD-001`–`OD-007`) were already resolved at `HUMAN_SPEC_APPROVAL`; both
`implementation_plan` Open Questions were resolved at
`HUMAN_PLAN_APPROVAL`. No security-sensitive ambiguity was encountered
during implementation that the approved artifacts did not already resolve.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-002
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-002-implementation-report.md
  next_stage: IMPLEMENTATION_VERIFICATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "56/56 tests pass (24 new + 32 pre-existing US-001, unchanged). Build clean, 0 warnings. dotnet format --verify-no-changes clean."
    - "Live manual smoke check confirmed exact Set-Cookie attributes (secure; samesite=strict; httponly) and byte-for-byte-identical (excl. timestamp) 401 bodies across wrong-password/unknown-email, beyond what the automated HttpClient-header assertions already covered."
    - "Discovered (not introduced): LoginRequestValidator's NotEmpty() messages are OS-locale-dependent, identical pre-existing behavior already shipped in US-001's RegistrationRequestValidator -- not fixed, out of this Story's scope, flagged for awareness."
    - "One accidental artifact (misdirected tool-usage.jsonl from a temporary cd during the live smoke check) created and removed before this report; confirmed absent from git status."
    - "api-design.md Q-1's IssuedUtc/TimeProvider simplification implemented exactly as the approved plan specified."
```
