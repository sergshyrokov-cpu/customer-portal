---
artifact_type: traceability
story: US-001
version: 3
status: ARCHIVED
created_at: 2026-09-01T13:17:46Z
updated_at: 2026-09-02T13:11:31Z
produced_by: reconciliation-reviewer
inputs:
  - path: docs/stories/US-001-register-customer.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 2
  - path: docs/designs/api/US-001-api-design.md
    version: 2
  - path: docs/designs/database/US-001-db-design.md
    version: 2
  - path: docs/impact-analysis/US-001-impact-analysis.md
    version: 2
  - path: docs/plans/US-001-implementation-plan.md
    version: 2
  - path: docs/tests/US-001-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-001-implementation-report.md
    version: 4
  - path: docs/verification/US-001-implementation-verification.md
    version: 2
  - path: docs/reviews/security/US-001-security-review.md
    version: 1
supersedes: docs/reconciliation/US-001-traceability.md v2
---

# Acceptance Criteria Traceability Matrix — US-001 (v3, RECONCILED)

Authoritative end-to-end matrix. All 7 Acceptance Criteria are
**RECONCILED** — approved requirement, implementation location, executable
test, independent functional verification, and independent security
evidence all agree with no gap.

## AC-001 — Successful Registration

- **Story:** AC-001. **Spec:** FR-1, FR-5, FR-8, FR-9, §5.
- **API design:** v2 §4 (`registerCustomer`, `201` + `Location` +
  `CustomerResponse`). **DB design:** v2 §4 (`customer` table).
- **Plan step:** 1, 2, 3, 5, 7, 10, 11 (entity → configuration → `DbSet` →
  hasher → repository → service → controller).
- **Implementation:** `Controllers/CustomersController.Register`,
  `Services/CustomerService.RegisterAsync`.
- **Test:** `CustomerRegistrationApiTests.PostCustomers_ValidRequest_Returns201WithLocationAndSafeBody`,
  `CustomerServiceTests.RegisterAsync_ValidRequest_CreatesEnabledCustomerWithCustomerRole`.
- **Implementation Verification:** VERIFIED (v2 §7); live re-check
  reproduced `201` + `Location: /api/v1/customers/1` + correct body.
- **Security Review:** positive control confirmed (v1 §20).
- **Status: RECONCILED.**

## AC-002 — Unique Email

- **Story:** AC-002. **Spec:** FR-4, §6.1, §8.
- **API design:** v2 §4.3 (`409`). **DB design:** v2 §4.3
  (`uq_customer_email`), §5 (case-insensitive normalization mechanism).
- **Plan step:** 6, 7, 10.
- **Implementation:** `CustomerService.RegisterAsync` (uniqueness check +
  `DuplicateEmailException`), `Data/Configurations/CustomerConfiguration`
  (unique index).
- **Test:** `CustomerServiceTests.RegisterAsync_DuplicateEmailCaseInsensitive_ThrowsDuplicateEmailException`,
  `CustomerRegistrationApiTests.PostCustomers_DuplicateEmail_Returns409`,
  `CustomerPersistenceTests.SaveChanges_ExactDuplicateEmail_ThrowsOnUniqueConstraint`.
- **Implementation Verification:** VERIFIED (v2 §7).
- **Security Review:** abuse-case reviewed (v1 §16, Q5) — concurrent-race
  edge case identified and accepted per OD-005:A (F-2 there), not a
  correctness or confidentiality defect; sequential duplicate path fully
  sound.
- **Status: RECONCILED.**

## AC-003 — Email Validation

- **Story:** AC-003. **Spec:** FR-3, §6.1.
- **API design:** v2 `RegistrationRequest.email` schema.
- **Plan step:** 9.
- **Implementation:** `Validation/RegistrationRequestValidator`.
- **Test:** `RegistrationRequestValidatorTests.Validate_InvalidEmailFormat_IsInvalid`,
  `..._MissingEmail_...`, `..._EmailExceeding254Characters_...`,
  `CustomerRegistrationApiTests.PostCustomers_InvalidEmailFormat_Returns400WithEmailFieldError`.
- **Implementation Verification:** VERIFIED (v2 §7).
- **Security Review:** input validation confirmed genuinely active at
  runtime, not merely annotated (v1 §9).
- **Status: RECONCILED.**

## AC-004 — Password Storage

- **Story:** AC-004. **Spec:** FR-5, FR-6, FR-7, §7.
- **DB design:** v2 §4.2, §7 (`password_hash`, sensitive-data handling).
- **Plan step:** 5, 10.
- **Implementation:** `Security/BCryptPasswordHasher`,
  `CustomerService.RegisterAsync`.
- **Test:** `CustomerServiceTests.RegisterAsync_ValidRequest_PersistsHashedPasswordNotPlaintext`,
  `CustomerPersistenceTests.SaveChanges_NewCustomer_SetsCreatedAtAndUpdatedAtInUtc`.
- **Implementation Verification:** VERIFIED (v2 §7).
- **Security Review:** BCrypt confirmed real, default (non-trivial) work
  factor, matches SC-1/SC-2 (v1 §7, Q3). Plaintext confirmed unreachable
  outside the inbound DTO.
- **Status: RECONCILED.**

## AC-005 — Secure Response

- **Story:** AC-005. **Spec:** FR-7, FR-8, §7.
- **API design:** v2 §4.2 (`CustomerResponse` field list, OD-004:A).
- **Plan step:** 8, 11.
- **Implementation:** `Models/Dtos/CustomerResponse` (no credential
  property exists on the type at all).
- **Test:** `CustomerRegistrationApiTests` (body assertions),
  `RegistrationSecurityPostureTests.PostCustomers_SuccessfulRegistration_ResponseBodyContainsNoPasswordOrHash`.
- **Implementation Verification:** VERIFIED (v2 §7); live re-check
  confirmed no `password`/`passwordHash` property in the response body.
- **Security Review:** confirmed no credential exposure across responses,
  logs, or exceptions (v1 §8).
- **Status: RECONCILED.**

## AC-006 — Password Policy

- **Story:** derived from AC-001/AC-003. **Spec:** FR-3, FR-6, §6.2.
- **API design:** v2 §4.1; design-review v2 D-1/D-6.
- **Plan step:** 9, 10.
- **Implementation:** `Validation/RegistrationRequestValidator`,
  `CustomerService.IsPolicyCompliant` (Service-layer re-check, FR-6).
- **Test:** `RegistrationRequestValidatorTests.Validate_PasswordViolatesPolicy_IsInvalidWithPasswordFieldError`
  (`[Theory]`, 5 cases), `..._PasswordExactly72AsciiBytes_IsValid`,
  `..._PasswordMultiByteCharacterPushingByteLengthOver72_IsInvalid`,
  `..._PasswordFailureMessage_NeverEchoesSubmittedValue`,
  `CustomerRegistrationApiTests.PostCustomers_WeakPassword_Returns400WithPasswordFieldError`.
- **Implementation Verification:** VERIFIED (v2 §7); F-3 there notes the
  Service-layer re-check branch has no *direct* unit test — the
  request-layer control (which fully implements this AC) is thoroughly
  tested; non-blocking.
- **Security Review:** independently confirmed the Service-layer branch is
  provably unreachable given the primary control's confirmed-active
  wiring — not a security gap (v1 §7, Q4).
- **Status: RECONCILED.**

## AC-007 — Media Type

- **Story:** AC-007. **Spec:** FR-2, §8.
- **API design:** v2 §3, §4.3 (`415`, `ErrorResponse` shape).
- **Plan step:** 12 (Program.cs wiring), plus the F-1 correction pass
  (content-type middleware + `UnsupportedMediaTypeException`).
- **Implementation:** `Program.cs` pipeline middleware,
  `Exceptions/UnsupportedMediaTypeException`, `GlobalExceptionHandler`.
- **Test:** `CustomerRegistrationApiTests.PostCustomers_MissingContentType_Returns415`.
- **Implementation Verification:** VERIFIED (v2 §7/§8) — both the status
  code (original) and the body shape (after the F-1 correction, live
  re-checked independently).
- **Security Review:** the new middleware independently confirmed to
  introduce no DoS or information-disclosure risk (v1 §10, Q2).
- **Status: RECONCILED.**

---

**7 / 7 RECONCILED.** No `PARTIALLY_RECONCILED`, `NOT_RECONCILED`, or
`BLOCKED` status anywhere in this matrix.
