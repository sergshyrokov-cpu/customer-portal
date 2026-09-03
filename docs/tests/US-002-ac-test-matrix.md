---
artifact_type: ac_test_matrix
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T14:24:46Z
updated_at: 2026-09-02T14:24:46Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/plans/US-002-implementation-plan.md
    version: 2
supersedes: null
---

# Acceptance Criteria → Test Matrix — US-002 Customer Login (v1)

| AC | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Valid login, HTTP | Integration | `LoginApiTests` | `PostSessions_ValidCredentials_Returns200WithSessionCookieAndSafeBody` | `200`, `Set-Cookie` (`HttpOnly`, `SameSite=Strict`), body `{id,email,role}`, no credential field | Written, RED (project-wide compile failure) |
| AC-001 | Valid authentication, orchestration | Unit | `CustomerAuthServiceTests` | `AuthenticateAndSignInAsync_ValidCredentials_SignsInWithCorrectRoleClaimAndReturnsCustomer` | returns `Customer`; `SignInAsync` called with the correct scheme and role claim | Written, RED (compile) |
| AC-001 | Valid authentication, email case-insensitivity | Unit | `CustomerAuthServiceTests` | `AuthenticateAndSignInAsync_EmailDiffersOnlyByCase_StillAuthenticates` | authenticates against the lowercase-stored account | Written, RED (compile) |
| AC-001 | Successful login, response mapping | Unit | `CustomerServiceLoginTests` | `LoginAsync_SuccessfulAuthentication_MapsCustomerToLoginResponse` | `LoginResponse` fields match the authenticated `Customer` | Written, RED (compile) |
| AC-002 | Wrong password, HTTP | Integration | `LoginApiTests` | `PostSessions_WrongPassword_Returns401WithNoCookie` | `401`, no `Set-Cookie` | Written, RED (compile) |
| AC-002 | Wrong password, orchestration | Unit | `CustomerAuthServiceTests` | `AuthenticateAndSignInAsync_WrongPassword_ThrowsAuthenticationFailedException` | throws `AuthenticationFailedException` | Written, RED (compile) |
| AC-003 | Unknown email, HTTP | Integration | `LoginApiTests` | `PostSessions_UnknownEmail_Returns401` | `401`, no `Set-Cookie` | Written, RED (compile) |
| AC-003 | Unknown email, orchestration | Unit | `CustomerAuthServiceTests` | `AuthenticateAndSignInAsync_UnknownEmail_ThrowsAuthenticationFailedException` | throws `AuthenticationFailedException` | Written, RED (compile) |
| AC-003 | Unknown email, timing parity (impact-analysis R-2) | Unit | `CustomerAuthServiceTests` | `AuthenticateAndSignInAsync_UnknownEmail_StillCallsPasswordVerifyOnceForTimingParity` | `IPasswordHasher.Verify` invoked exactly once | Written, RED (compile) |
| AC-003/AC-002 | Malformed-but-present email → auth failure, not `400` | Integration | `LoginApiTests` | `PostSessions_MalformedEmailButPresentValue_TreatedAsAuthenticationFailureNot400` | `401` (not `400`) | Written, RED (compile) |
| AC-004 | Disabled account, correct password, HTTP | Integration | `LoginApiTests` | `PostSessions_DisabledAccountWithCorrectPassword_Returns401` | `401`, no `Set-Cookie` | Written, RED (compile) |
| AC-004 | Disabled account, orchestration | Unit | `CustomerAuthServiceTests` | `AuthenticateAndSignInAsync_DisabledAccountWithCorrectPassword_ThrowsAuthenticationFailedException` | throws `AuthenticationFailedException` | Written, RED (compile) |
| AC-002/AC-003/AC-004 | All three failures share one exception message (impact-analysis R-1) | Unit | `CustomerAuthServiceTests` | `AuthenticateAndSignInAsync_AllThreeFailureCases_ThrowExceptionWithIdenticalMessage` | identical `Exception.Message` across all three | Written, RED (compile) |
| AC-002/AC-003/AC-004 | All three failures produce indistinguishable HTTP responses (NFR-5, OD-003:A) | Security | `LoginSecurityPostureTests` | `PostSessions_UnknownEmailWrongPasswordAndDisabledAccount_ProduceIndistinguishableResponses` | identical status/error/message/path/fieldErrors-presence (excl. `timestamp`); no `Set-Cookie` on any | Written, RED (compile) |
| AC-002/AC-003/AC-004 | Authentication failure propagates unchanged through the Service layer | Unit | `CustomerServiceLoginTests` | `LoginAsync_AuthenticationFailure_PropagatesExceptionUnchanged` | `AuthenticationFailedException` propagates from `LoginAsync` | Written, RED (compile) |
| AC-005 | Successful login response excludes credentials | Integration | `LoginApiTests` | `PostSessions_ValidCredentials_Returns200WithSessionCookieAndSafeBody` (same method, additional assertions) | no `password`/`passwordHash` property in body | Written, RED (compile) |
| AC-005 | Response body never contains password/hash (raw string check) | Integration | `LoginApiTests` | `PostSessions_SuccessfulLogin_ResponseBodyContainsNoPasswordOrHash` | raw body excludes plaintext and `passwordHash` | Written, RED (compile) |
| AC-006 | Missing `Content-Type` | Integration | `LoginApiTests` | `PostSessions_MissingContentType_Returns415` | `415` | Written, RED (compile) |
| AC-007 | Missing/blank `email` | Unit | `LoginRequestValidatorTests` | `Validate_MissingEmail_IsInvalidWithEmailFieldError` | `Email` field error | Written, RED (`CS0246`: `LoginRequestValidator` not found) |
| AC-007 | Missing/blank `password` | Unit | `LoginRequestValidatorTests` | `Validate_MissingPassword_IsInvalidWithPasswordFieldError` | `Password` field error | Written, RED (compile) |
| AC-007 | Missing `email`, HTTP | Integration | `LoginApiTests` | `PostSessions_MissingEmail_Returns400WithEmailFieldError` | `400`, `fieldErrors[].field="email"` | Written, RED (compile) |
| AC-007 | Missing `password`, HTTP | Integration | `LoginApiTests` | `PostSessions_MissingPassword_Returns400WithPasswordFieldError` | `400`, `fieldErrors[].field="password"` | Written, RED (compile) |
| (derived, Spec §6.1) | Malformed email does not fail validation (deliberate no-re-validation) | Unit | `LoginRequestValidatorTests` | `Validate_MalformedButNonBlankEmail_IsValid` | no `Email` error | Written, RED (compile) |
| (derived, Spec §6.2) | Policy-violating password does not fail validation (deliberate no-re-validation) | Unit | `LoginRequestValidatorTests` | `Validate_ShortPasswordFailingRegistrationPolicy_IsValid` | no `Password` error | Written, RED (compile) |
| (derived) | Well-formed request passes validation | Unit | `LoginRequestValidatorTests` | `Validate_NonBlankEmailAndPassword_IsValid` | `result.IsValid == true` | Written, RED (compile) |

Every Acceptance Criterion (AC-001..AC-007) has at least one mapped
scenario across at least two test levels. No mandatory AC is uncovered.
The absolute cookie-lifetime cap (`OnValidatePrincipal`, OD-006:A) is
**not** in this matrix — it is not tied to a Story Acceptance Criterion;
see `test_strategy` v1 §10 for the excluded-scenario justification.

**Status legend:** "RED (compile)" = the whole `CustomerPortal.Tests`
project fails to build because this test method's file (or a file it
depends on transitively via the shared project) references a not-yet-existing
production type; expected and correct for this Story, exactly mirroring
US-001's precedent — see `test_generation_report` v1 §5/§6 for the exact
compiler evidence and the full list of assumed-not-yet-existing symbols.
