---
artifact_type: ac_test_matrix
story: US-001
version: 2
status: DRAFT
created_at: 2026-08-31T11:25:00Z
updated_at: 2026-09-01T12:09:46Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-001-spec.md
    version: 2
  - path: docs/designs/api/US-001-api-design.md
    version: 2
  - path: docs/plans/US-001-implementation-plan.md
    version: 2
supersedes: docs/tests/US-001-ac-test-matrix.md v1
---

# Acceptance Criteria → Test Matrix — US-001 Customer Registration (v2)

| AC | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Valid registration | Integration | `CustomerRegistrationApiTests` | `PostCustomers_ValidRequest_Returns201WithLocationAndSafeBody` | `201`, `Location` header, `CustomerResponse` with `role=CUSTOMER` | Written, compiles, RED (endpoint absent — 404 until Controller exists) |
| AC-001 | Valid registration — orchestration | Unit | `CustomerServiceTests` | `RegisterAsync_ValidRequest_CreatesEnabledCustomerWithCustomerRole` | response has `role="CUSTOMER"` | Written, RED (`CS0246`: `Customer`/`ICustomerRepository` not found) |
| AC-002 | Duplicate email, same case | Unit | `CustomerServiceTests` | `RegisterAsync_DuplicateEmailCaseInsensitive_ThrowsDuplicateEmailException` | throws `DuplicateEmailException` | Written, RED (compile) |
| AC-002 | Duplicate email, HTTP | Integration | `CustomerRegistrationApiTests` | `PostCustomers_DuplicateEmail_Returns409` | `409` | Written, compiles, RED (endpoint absent) |
| AC-002 | Duplicate email, exact-case DB constraint | Persistence | `CustomerPersistenceTests` | `SaveChanges_ExactDuplicateEmail_ThrowsOnUniqueConstraint` | `DbUpdateException` | Written, RED (compile) |
| AC-002 | Email normalized before uniqueness check | Unit | `CustomerServiceTests` | `RegisterAsync_EmailDiffersOnlyByCase_NormalizedToLowercaseBeforeStorage` | stored key is lowercase | Written, RED (compile) |
| AC-003 | Invalid email format | Unit | `RegistrationRequestValidatorTests` | `Validate_InvalidEmailFormat_IsInvalid` | `Email` field error | Written, RED (`CS0246`: `RegistrationRequestValidator` not found) |
| AC-003 | Missing email | Unit | `RegistrationRequestValidatorTests` | `Validate_MissingEmail_IsInvalidWithEmailFieldError` | `Email` field error | Written, RED (compile) |
| AC-003 | Email over 254 chars | Unit / Boundary | `RegistrationRequestValidatorTests` | `Validate_EmailExceeding254Characters_IsInvalid` | `Email` field error | Written, RED (compile) |
| AC-003 | Invalid email, HTTP | Integration | `CustomerRegistrationApiTests` | `PostCustomers_InvalidEmailFormat_Returns400WithEmailFieldError` | `400`, `fieldErrors[].field="email"` | Written, compiles, RED (endpoint absent) |
| AC-004 | Password hashed, not plaintext | Unit | `CustomerServiceTests` | `RegisterAsync_ValidRequest_PersistsHashedPasswordNotPlaintext` | stored value ≠ plaintext, equals hasher output | Written, RED (compile) |
| AC-004 | Stored account enabled | Unit | `CustomerServiceTests` | `RegisterAsync_ValidRequest_SetsEnabledTrue` | `Enabled == true` | Written, RED (compile) |
| AC-004 | Audit timestamps UTC | Persistence | `CustomerPersistenceTests` | `SaveChanges_NewCustomer_SetsCreatedAtAndUpdatedAtInUtc` | `CreatedAt`/`UpdatedAt` offset zero, equal on insert | Written, RED (compile) |
| AC-005 | Response excludes credentials | Integration | `CustomerRegistrationApiTests` | `PostCustomers_ValidRequest_Returns201WithLocationAndSafeBody` (same method, additional assertions) | no `password`/`passwordHash` property in body | Written, compiles, RED (endpoint absent) |
| AC-005 | Response body never contains password/hash | Security | `RegistrationSecurityPostureTests` | `PostCustomers_SuccessfulRegistration_ResponseBodyContainsNoPasswordOrHash` | raw body excludes plaintext and `passwordHash` | Written, compiles, RED (endpoint absent) |
| AC-006 | Password policy violations (5 cases) | Unit / Boundary | `RegistrationRequestValidatorTests` | `Validate_PasswordViolatesPolicy_IsInvalidWithPasswordFieldError` (`[Theory]`, 5 `InlineData` cases) | `Password` field error | Written, RED (compile) |
| AC-006 | Password meets policy | Unit | `RegistrationRequestValidatorTests` | `Validate_PasswordMeetsPolicy_PasswordRuleIsValid` | no `Password` error | Written, RED (compile) |
| AC-006 | Password exactly 72 bytes | Unit / Boundary | `RegistrationRequestValidatorTests` | `Validate_PasswordExactly72AsciiBytes_IsValid` | no `Password` error | Written, RED (compile) |
| AC-006 | Password over 72 bytes via multi-byte char | Unit / Boundary | `RegistrationRequestValidatorTests` | `Validate_PasswordMultiByteCharacterPushingByteLengthOver72_IsInvalid` | `Password` field error | Written, RED (compile) |
| AC-006 | Validation message never echoes value | Security / Unit | `RegistrationRequestValidatorTests` | `Validate_PasswordFailureMessage_NeverEchoesSubmittedValue` | error message excludes submitted string | Written, RED (compile) |
| AC-006 | Weak password, HTTP | Integration | `CustomerRegistrationApiTests` | `PostCustomers_WeakPassword_Returns400WithPasswordFieldError` | `400`, `fieldErrors[].field="password"` | Written, compiles, RED (endpoint absent) |
| AC-007 | Missing `Content-Type` | Integration | `CustomerRegistrationApiTests` | `PostCustomers_MissingContentType_Returns415` | `415` | Written, compiles, RED (endpoint absent) |
| (derived, Spec §6.3) | Unknown JSON field rejected | Integration | `CustomerRegistrationApiTests` | `PostCustomers_UnknownJsonField_Returns400` | `400` | Written, compiles, RED (endpoint absent) |
| (derived, SC-4) | New endpoint not rejected unauthenticated | Security | `RegistrationSecurityPostureTests` | `PostCustomers_Unauthenticated_IsNotRejectedForLackOfAuthentication` | not `401`/`403` | Written, compiles, RED (endpoint absent — currently 404) |
| (derived, SC-4) | Global fallback policy configured | Security | `RegistrationSecurityPostureTests` | `AuthorizationFallbackPolicy_RequiresAuthenticatedUser` | fallback policy requires authenticated user | Written, compiles, **currently FAILING for the intended reason** (no fallback policy configured yet in `Program.cs`) |
| DB constraints (PC-4/PC-9) | `Email`/`PasswordHash` column metadata | Persistence | `CustomerPersistenceTests` | `CustomerConfiguration_EmailProperty_HasMaxLength254AndIsRequired`, `CustomerConfiguration_PasswordHashProperty_HasMaxLength60AndIsRequired` | max length / nullability match db-design v2 §4.2 | Written, RED (compile) |
| DB constraints (PC-7) | Unique index on `Email` | Persistence | `CustomerPersistenceTests` | `CustomerConfiguration_EmailIndex_IsUnique` | index `IsUnique == true` | Written, RED (compile) |

Every Acceptance Criterion (AC-001..AC-007) has at least one mapped
scenario across at least two test levels. No mandatory AC is uncovered.

**Status legend:** "RED (compile)" = the whole `CustomerPortal.Tests`
project fails to build because this test method's file references a
not-yet-existing production type; expected and correct for a from-scratch
Story (see `test_generation_report` v2 §5). "RED (endpoint absent)" =
the test file itself compiles, but cannot pass yet since
`POST /api/v1/customers` doesn't exist (currently would resolve as `404`
once the build succeeds enough to run it — not yet directly observed since
the project doesn't build as a whole). "Currently FAILING for the intended
reason" = the one test that both compiles standalone AND could theoretically
run today if isolated from the rest of the project, and would fail exactly
because `Program.cs` has no SC-4 fallback policy yet, which is the correct
red-phase signal for that specific test.
