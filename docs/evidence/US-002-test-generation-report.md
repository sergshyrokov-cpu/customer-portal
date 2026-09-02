---
artifact_type: test_generation_report
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T14:24:46Z
updated_at: 2026-09-02T14:24:46Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-002-test-strategy.md
    version: 1
  - path: docs/tests/US-002-ac-test-matrix.md
    version: 1
  - path: docs/plans/US-002-implementation-plan.md
    version: 2
supersedes: null
---

# Test Generation Report — US-002 Customer Login (v1)

## 1. Story

US-002 — Customer Login. From-scratch test suite for a Story added on top
of the already-implemented, already-tested US-001 codebase — not a
from-scratch project (contrast with US-001's own TEST_WRITING report,
which had zero pre-existing tests to preserve).

## 2. Test Files Created

| File | Test Class | # Tests |
|---|---|---|
| `CustomerPortal.Tests/Validation/LoginRequestValidatorTests.cs` | `LoginRequestValidatorTests` | 5 |
| `CustomerPortal.Tests/Security/CustomerAuthServiceTests.cs` | `CustomerAuthServiceTests` | 7 |
| `CustomerPortal.Tests/Services/CustomerServiceLoginTests.cs` | `CustomerServiceLoginTests` | 2 |
| `CustomerPortal.Tests/Login/LoginApiTests.cs` | `LoginApiTests` | 9 |
| `CustomerPortal.Tests/Security/LoginSecurityPostureTests.cs` | `LoginSecurityPostureTests` | 1 |

**24 new tests** across 5 new files.

## 3. Test Files Modified

| File | Change | Reason |
|---|---|---|
| `CustomerPortal.Tests/Services/CustomerServiceTests.cs` | Added a private `NeverCalledCustomerAuthService : ICustomerAuthService` stub (throws if invoked) and updated the 5 existing `new CustomerService(...)` call sites to pass it as a third constructor argument. | `CustomerService`'s constructor gains a required `ICustomerAuthService` dependency for `LoginAsync` (implementation-plan v2 / plan-review v1 F-1). `RegisterAsync` never touches it. **No assertion, scenario, or expected outcome in any existing test method was changed** — this is a mechanical fixture update to keep the file compiling against the new, approved constructor shape, the same class of change accepted for US-001's `CustomerSchemaTest` fixture fix (implementation_verification v1 MF-1). |

No other existing test file was touched. `TestWebApplicationFactory.cs`
(US-001) is reused **unmodified** via a cross-namespace `using` from
`LoginApiTests.cs`/`LoginSecurityPostureTests.cs`.

## 4. Commands Used

```
dotnet build
dotnet build CustomerPortal.Tests -v:normal
dotnet clean && dotnet build
```

No IDE MCP server is configured for this .NET track; all evidence below is
from direct CLI invocation, consistent with US-001's precedent.

## 5. Tests Executed

**None could execute.** `dotnet build` (and therefore `dotnet test`, which
depends on it) fails for the whole solution because `CustomerPortal.Tests`
does not compile — see §6. This affects every test in the project,
including US-001's 32 previously-passing regression tests, none of which
were themselves modified (only `CustomerServiceTests.cs`'s constructor
call sites, per §3). This is expected and is the correct red-phase state
for adding a required dependency to an existing, already-implemented
class — not a project-wide regression, but a build-unit-level consequence
of C# compiling one project as a single unit (same class of finding
US-001's own test_generation_report v2 documented, §11 there / §7 below).

## 6. Compiler Evidence (actual, reproduced twice — a fresh `dotnet build`
and a `dotnet clean && dotnet build`, identical result both times)

```
CustomerPortal -> ...\CustomerPortal\bin\Debug\net8.0\CustomerPortal.dll   (production project: unaffected, builds clean)

CustomerPortal.Tests\Validation\LoginRequestValidatorTests.cs(9,22): error CS0246:
  Не удалось найти тип или имя пространства имен "LoginRequestValidator"
CustomerPortal.Tests\Services\CustomerServiceLoginTests.cs(34,52): error CS0246:
  Не удалось найти тип или имя пространства имен "ICustomerAuthService"
CustomerPortal.Tests\Services\CustomerServiceTests.cs(45,59): error CS0246:
  Не удалось найти тип или имя пространства имен "ICustomerAuthService"

Ошибок: 3
```

(Russian-locale toolchain; `CS0246` = "type or namespace name could not be
found".) The production `CustomerPortal` project itself builds
successfully and is completely unaffected — confirming these failures are
isolated to the new/updated test code referencing not-yet-implemented
production symbols, not a pre-existing defect.

**Only 3 distinct errors were reported**, even though more not-yet-existing
symbols are directly referenced elsewhere in the new test files (e.g.
`CustomerAuthService` the concrete class, `LoginRequest`, `LoginResponse`,
`AuthenticationFailedException`, `ICustomerService.LoginAsync`). This was
independently re-verified by grepping the actual source for each name
(confirmed absent from `CustomerPortal/`) and re-running the build twice
including a full clean — the count is stable and reproducible, not a
transient/incomplete build. The most likely explanation is that Roslyn
suppresses some cascading semantic-binding errors once an earlier
unresolved symbol breaks a type's binding within the same file (e.g.
`ICustomerAuthService` failing as a base-interface reference in
`CustomerServiceLoginTests.cs`'s nested `FakeCustomerAuthService` class
appears to suppress further errors for that file's other undefined
references). This is reported transparently rather than assumed benign:
**§7 lists every symbol this test suite requires, independent of what the
compiler currently surfaces**, so `IMPLEMENTATION` has the complete
picture regardless of compiler error-reporting behavior.

## 7. Assumed Production API Surface (for `IMPLEMENTATION`)

Every symbol below is referenced by at least one test in this suite but
does not yet exist in `CustomerPortal/`. This list is normative for what
these tests require — not a suggestion, a specification of the exact shape
the tests were written against (consistent with `implementation_plan` v2's
Files To Create/Modify):

| Symbol | Kind | Shape required by tests |
|---|---|---|
| `CustomerPortal.Exceptions.AuthenticationFailedException` | class | Parameterless constructor; `Message` is a fixed, identical string across every instance (asserted directly) |
| `CustomerPortal.Security.ICustomerAuthService` | interface | `Task<Customer> AuthenticateAndSignInAsync(HttpContext httpContext, string email, string password)` |
| `CustomerPortal.Security.CustomerAuthService` | class | Constructor `(ICustomerRepository, IPasswordHasher)`; implements the interface above; normalizes email to lowercase before lookup; calls `IPasswordHasher.Verify` exactly once per call (including when no account is found); throws `AuthenticationFailedException` for unknown email / wrong password / disabled account; on success calls `HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)` with a `ClaimTypes.Role` claim equal to the stored `Role` |
| `CustomerPortal.Models.Requests.LoginRequest` | record | `(string Email, string Password)`, mirroring `RegistrationRequest`'s `[JsonUnmappedMemberHandling(Disallow)]` style |
| `CustomerPortal.Models.Dtos.LoginResponse` | record | `(long Id, string Email, string Role)` |
| `CustomerPortal.Validation.LoginRequestValidator` | class | `AbstractValidator<LoginRequest>`; `NotEmpty()` on `Email` and `Password` only — **no** `EmailAddress()`/`MaximumLength()`/password-policy rules |
| `CustomerPortal.Controllers.SessionsController` | class | `POST /api/v1/sessions`, `[AllowAnonymous]`, calls `ICustomerService.LoginAsync(HttpContext, LoginRequest)` only, returns `200 OK` with the `LoginResponse` (no `Location` header) |
| `CustomerPortal.Services.ICustomerService.LoginAsync` | method (new, on existing interface) | `Task<LoginResponse> LoginAsync(HttpContext httpContext, LoginRequest request)` — calls `ICustomerAuthService.AuthenticateAndSignInAsync`, maps the returned `Customer` to `LoginResponse`, does not catch/translate `AuthenticationFailedException` |
| `CustomerPortal.Services.CustomerService` constructor | change (existing class) | Gains a third parameter, `ICustomerAuthService customerAuthService` |
| `CustomerPortal.Exceptions.GlobalExceptionHandler` | change (existing class) | New switch arm: `AuthenticationFailedException => (401, "Unauthorized")` |

## 8. Passing Existing Tests

None currently executable (§5) — this is a build-level, not a
per-test-level, consequence. All 32 of US-001's tests are expected to pass
again, unchanged, once `IMPLEMENTATION` adds the symbols in §7 (their
source was not modified; only `CustomerServiceTests.cs`'s constructor call
sites, a non-behavioral fixture update per §3).

## 9. Expected Failing New Tests

All 24 new tests (§2) are expected to fail at **compile time** first
(§5/§6), then — once `IMPLEMENTATION` makes the project buildable — at
**assertion time**, until the corresponding production behavior exists.
This two-phase red state (compile-red, then assertion-red) is the same
pattern US-001 documented for its own from-scratch stage, applied here to
a smaller, additive symbol set layered on an already-working codebase.

## 10. Unexpected Failures

None. Every failure traced to §6/§7 is attributable to a specific,
named, not-yet-implemented production symbol this Story's approved plan
calls for — none is a test-code defect, an invalid fixture, or a
contradiction with an approved requirement.

## 11. Untested Acceptance Criteria

None. All 7 Acceptance Criteria (AC-001..AC-007) have at least one mapped
test at two or more levels — see `docs/tests/US-002-ac-test-matrix.md` v1.

## 12. Open Decisions

None blocking. All 7 Story-level Open Decisions were resolved at
`HUMAN_SPEC_APPROVAL`; both `implementation_plan` Open Questions were
resolved at `HUMAN_PLAN_APPROVAL`. One approved-but-unused test dependency
is explicitly reported, not silently dropped: `test_strategy` v1 §10
documents why the approved `Microsoft.Extensions.TimeProvider.Testing`
package (Open Question 2, approved `yes`) has no executable test target
within this Story's scope (no `[Authorize]` endpoint exists yet to observe
the absolute-cookie-cap rejection) — the package was **not** added to
`CustomerPortal.Tests.csproj`, since adding an unused dependency was judged
worse than reporting the gap.

## 13. Overall Result

`PASS`. Every Acceptance Criterion has test coverage; `test_strategy` and
`ac_test_matrix` exist with valid front matter; 24 executable tests exist
under `CustomerPortal.Tests/` and are correctly red for the expected reason
(referencing not-yet-implemented production symbols, per an exhaustively
documented list in §7); no test weakens an assertion to pass; no existing
test's scenario or assertion was altered (only a mechanical constructor-call
update, disclosed in §3); the one approved-but-inapplicable test dependency
is explicitly reported rather than silently used or silently dropped; no
blocking Open Decision remains.

```yaml
result:
  verdict: PASS
  stage: TEST_WRITING
  story: US-002
  artifact_status: DRAFT
  artifacts:
    - docs/tests/US-002-test-strategy.md
    - docs/tests/US-002-ac-test-matrix.md
    - docs/evidence/US-002-test-generation-report.md
  next_stage: IMPLEMENTATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "24 new tests across 5 new files, all AC-001..AC-007 covered at 2+ levels each. Red phase confirmed via dotnet build (reproduced after dotnet clean): 3 distinct CS0246 errors surfaced; a full list of all 10 not-yet-existing/changed production symbols this suite requires is recorded in section 7 regardless of compiler surfacing behavior."
    - "CustomerServiceTests.cs (US-001, existing) required a disclosed, non-behavioral fixture update: a NeverCalledCustomerAuthService stub added and threaded through 5 existing constructor calls, because CustomerService's constructor gains a required ICustomerAuthService dependency. No assertion/scenario/outcome changed."
    - "impact-analysis R-1 (exception-message uniformity) and R-2 (timing-parity dummy hash-verify) both got dedicated unit tests, not just NFR-5's HTTP-level check -- CustomerAuthServiceTests directly asserts the invariant at its source."
    - "Discovered during test design (not assumed): the approved absolute-cookie-cap test (OD-006:A, Open Question 2, TimeProvider.Testing) has no executable target in this Story -- no [Authorize] endpoint exists yet to observe OnValidatePrincipal's rejection. Documented as an excluded scenario with justification (test_strategy v1 section 10) and NOT added the dependency for an unused test; recommended for IMPLEMENTATION (extract a testable seam) or US-003 (first real protected endpoint)."
    - "SignInAsync is exercised in a pure unit test (no web host) via a fake IAuthenticationService resolved from a DefaultHttpContext's RequestServices -- avoids needing an integration test for CustomerAuthService's own logic."
```
