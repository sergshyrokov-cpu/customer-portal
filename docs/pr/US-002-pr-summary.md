---
artifact_type: pr_summary
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T14:53:54Z
updated_at: 2026-09-02T14:53:54Z
produced_by: pr-preparer
inputs:
  - path: docs/stories/US-002-customer-login.md
    version: null
  - path: docs/specifications/US-002-spec.md
    version: 2
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
  - path: docs/reviews/reconciliation/US-002-reconciliation.md
    version: 1
  - path: docs/reconciliation/US-002-traceability.md
    version: 1
supersedes: null
---

# Pull Request Summary — US-002 Customer Login

## Story & Business Goal

Allow a previously-registered, enabled Customer to authenticate with email
and password (`product-vision.md` — customer authentication is an MVP
success criterion). Builds directly on US-001's registration.

## Scope / Implemented Features

- New public endpoint `POST /api/v1/sessions`: authenticates a Customer and
  establishes an ASP.NET Core cookie session on success.
- Uniform, anti-enumeration `401` response for unknown email, wrong
  password, and disabled account — indistinguishable to the caller.
- Cookie-authentication scheme (registered but unconfigured since US-001)
  now fully configured: `HttpOnly`, `Secure`, `SameSite=Strict`, 30-minute
  sliding idle timeout, 8-hour absolute cap, and a `401`/`403` challenge
  override (no server-rendered login page exists in this API).
- No persistence change — login is read-only against the existing
  `customer` table.

## API Changes

`POST /api/v1/sessions` (see `docs/designs/api/US-002-openapi.yaml` v1):

| Status | When |
|---|---|
| `200 OK` | valid credentials, account enabled — body `{ id, email, role }`, `Set-Cookie` issued |
| `400 Bad Request` | missing/blank `email` or `password` |
| `401 Unauthorized` | unknown email, wrong password, or disabled account — byte-for-byte identical (excl. `timestamp`) across all three causes |
| `415 Unsupported Media Type` | missing/non-JSON `Content-Type` |
| `500 Internal Server Error` | unmapped exception |

No `Location` header on success — an approved, explicit exception to
`api-conventions.md` AC-4 (a cookie session is not an independently
addressable resource; recorded in `docs/designs/api/US-002-api-design.md`
§3, OD-001:A/OD-002:A).

## Database Changes

None. `DB_DESIGN` verdict `NOT_APPLICABLE` (re-confirmed at every
downstream stage through Reconciliation) — no entity, migration, or
`AppDbContext` change.

## Security Changes

- `CustomerAuthService` (new, `Security` namespace) is the sole caller of
  `SignInAsync` (SC-3), reusing the existing `IPasswordHasher`/
  `ICustomerRepository` — no new hashing mechanism.
- `AuthenticationFailedException` is a sealed type with **no public
  message-accepting constructor** — the anti-enumeration response
  uniformity is a compile-time guarantee, not a convention.
- A dummy `BCrypt.HashPassword`-derived hash is verified on the
  unknown-email path with the **same algorithmic cost** as a real password
  check, closing the timing side-channel that would otherwise let a caller
  distinguish "unknown email" from "wrong password" by response latency.
- Full adversarial review: `docs/reviews/security/US-002-security-review.md`
  v1, PASS, 0 Critical/Major. Independently analyzed and found already
  mitigated (not by anything new in this PR): login CSRF, via the
  pre-existing `Content-Type: application/json` enforcement + absence of
  any CORS policy.

## Tests Executed

Evidence owned by `implementation_verification` v1 (independently
reproduced a second time by `reconciliation` v1, both PASS):

```
dotnet build   → Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.
dotnet test    → Пройден!: не пройдено 0, пройдено 56, пропущено 0, всего 56.
dotnet format --verify-no-changes → exit 0, no output.
```

56/56 tests pass: 24 new (US-002) + 32 pre-existing (US-001, unchanged). A
live manual smoke check on a running instance independently confirmed the
exact `Set-Cookie` attributes and the byte-for-byte-identical `401` bodies
(`implementation_report` v1 §5).

## Acceptance Criteria Coverage

From `docs/reconciliation/US-002-traceability.md` v1 — **7/7 RECONCILED**:

| AC | Status |
|---|---|
| AC-001 Successful Login | RECONCILED |
| AC-002 Invalid Password | RECONCILED |
| AC-003 Unknown Account | RECONCILED |
| AC-004 Disabled Account | RECONCILED |
| AC-005 Secure Authentication Response | RECONCILED |
| AC-006 (media type) | RECONCILED |
| AC-007 (request-shape enforcement) | RECONCILED |

## PR Candidate Files — Include

Per `docs/reviews/reconciliation/US-002-reconciliation.md` v1 §15
(authoritative; consumed, not re-derived). **38 files** — see the finding
in "Notes For Reviewers" regarding that document's own internal count
labels.

**Production code (12):**
`CustomerPortal/Controllers/SessionsController.cs`,
`CustomerPortal/Exceptions/AuthenticationFailedException.cs`,
`CustomerPortal/Exceptions/GlobalExceptionHandler.cs` (modified),
`CustomerPortal/Models/Dtos/LoginResponse.cs`,
`CustomerPortal/Models/Requests/LoginRequest.cs`,
`CustomerPortal/Security/CustomerAuthService.cs`,
`CustomerPortal/Security/ICustomerAuthService.cs`,
`CustomerPortal/Services/CustomerService.cs` (modified),
`CustomerPortal/Services/ICustomerService.cs` (modified),
`CustomerPortal/Validation/LoginRequestValidator.cs`,
`CustomerPortal/Program.cs` (modified),
`CustomerPortal/appsettings.json` (modified).

**Test code (6):**
`CustomerPortal.Tests/Login/LoginApiTests.cs`,
`CustomerPortal.Tests/Security/CustomerAuthServiceTests.cs`,
`CustomerPortal.Tests/Security/LoginSecurityPostureTests.cs`,
`CustomerPortal.Tests/Services/CustomerServiceLoginTests.cs`,
`CustomerPortal.Tests/Services/CustomerServiceTests.cs` (modified — disclosed
non-behavioral constructor-fixture update only, no assertion changed),
`CustomerPortal.Tests/Validation/LoginRequestValidatorTests.cs`.

**Story documentation (18):**
`docs/decisions/US-002-open-decisions.md`,
`docs/designs/api/US-002-api-design.md`,
`docs/designs/api/US-002-openapi.yaml`,
`docs/evidence/US-002-clarification-report.md`,
`docs/evidence/US-002-implementation-report.md`,
`docs/evidence/US-002-test-generation-report.md`,
`docs/impact-analysis/US-002-impact-analysis.md`,
`docs/plans/US-002-implementation-plan.md`,
`docs/reviews/designs/US-002-design-review.md`,
`docs/reviews/plans/US-002-plan-review.md`,
`docs/reviews/security/US-002-security-review.md`,
`docs/reviews/specifications/US-002-spec-review.md`,
`docs/specifications/US-002-spec.md`,
`docs/tests/US-002-ac-test-matrix.md`,
`docs/tests/US-002-test-strategy.md`,
`docs/verification/US-002-implementation-verification.md`,
`docs/reviews/reconciliation/US-002-reconciliation.md`,
`docs/reconciliation/US-002-traceability.md`.

**Workflow bookkeeping (2):** `docs/workflow/history.jsonl`,
`docs/workflow/workflow-state.yaml` — harness state advancement, same
bundling treatment as US-001's precedent.

## PR Candidate Files — Exclude

None. No runtime artifact (no generated `.db`/`.db-shm`/`.db-wal` file
present), no local configuration, no secret, no unrelated change
(`reconciliation` v1 §15/§17, independently re-confirmed here against
current `git status`).

## Files Needing Human Decision

None.

## Risks & Known Limitations

- **Untestable in this Story, code-reviewed only:** the cookie
  challenge-behavior override (`401`/`403`, OD-004:A) and the absolute
  8-hour cookie-lifetime cap (`OnValidatePrincipal`) have no
  `[Authorize]`-protected endpoint to exercise them against yet.
  Disclosed consistently across `test_strategy`, `implementation_verification`,
  and `security_review`. **Recommend US-003 (Profile View) explicitly
  exercise both live**, since that Story is what finally makes them
  observable.
- **Pre-existing, not introduced or worsened by this PR:** `LoginRequestValidator`'s
  `NotEmpty()` field-error messages render in the server's OS locale — identical
  behavior already shipped in US-001's `RegistrationRequestValidator`.
- **Documentation-only, non-blocking:** `docs/decisions/US-002-open-decisions.md`
  v1 still shows all 7 Open Decisions as `OPEN` despite being resolved at
  `HUMAN_SPEC_APPROVAL` — the resolutions are authoritative via
  `workflow-state.yaml`/`history.jsonl`; a `us-clarifier` v2 publish would
  close this cosmetic gap (same class of carried gap as US-001's delivery).
- **Recommended for a future pass, not blocking:** record the login-CSRF
  mitigation rationale (JSON `Content-Type` enforcement + no CORS) and the
  full cookie-attribute baseline (`HttpOnly`/`Secure`) explicitly in
  `security-conventions.md` SC-5, so future Stories don't re-derive them by
  analogy (`security_review` v1 M-2/I-1).

## Release Notes

**User-visible:** Customers can now log in with email and password;
authenticated sessions are cookie-based.

**Technical:** New `Security.ICustomerAuthService`/`CustomerAuthService`;
`Services.ICustomerService` gains `LoginAsync`; cookie-authentication
scheme (registered since US-001) is now fully configured with explicit
lifetime and challenge-behavior settings.

**Security:** Login responses are anti-enumeration by construction
(compile-time-fixed exception message) and timing-safe (matched-cost dummy
hash-verify on the unknown-email path).

## Notes For Reviewers

- **Finding (Minor, discovered during PR preparation, not blocking):**
  `docs/reviews/reconciliation/US-002-reconciliation.md` v1 §15's own
  section-count labels and grand total are arithmetically inconsistent
  with its own itemized file list (e.g. labeled "Production code (9)" for
  a list of 12 items; stated total "37" where the itemized list — and
  actual `git status` — total 38; §12 also references a stale "33-file"
  figure). **The itemized file-by-file list itself is complete and
  accurate** — independently cross-checked against `git status --short`
  (38 lines, 38 files) with no omission or extra entry found; only the
  summary arithmetic/labels are wrong. This PR summary uses the correct,
  verified total (38) and the verified itemized list. Not treated as a
  blocking `stale_reconciliation` condition (no file is missing or
  misclassified) — recommend a future `reconciliation-reviewer` pass
  correct the labels for hygiene, not urgency.
- Two correction loops occurred earlier in this delivery
  (`SPEC_REVIEW` F-1, `PLAN_REVIEW` F-1), both independently re-verified
  resolved before advancing — see `docs/specifications/US-002-spec.md` v2's
  revision note and `docs/plans/US-002-implementation-plan.md` v2's
  revision note for the exact fixes.
- Reviewer attention especially welcome on: the `Services` → `Security`
  layering introduced by the plan-review fix (`CustomerService.LoginAsync`
  as the sole path to `CustomerAuthService`), and the `OnValidatePrincipal`/
  `TimeProvider` wiring in `Program.cs` (untestable in this Story, per
  Risks above).

## Readiness Result

```yaml
result:
  verdict: PASS
  stage: PR_PREPARATION
  story: US-002
  artifact_status: APPROVED
  artifacts:
    - docs/pr/US-002-pr-summary.md
  next_stage: READY_FOR_PR
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "Consumed reconciliation v1's PR scope and traceability v1 (7/7 RECONCILED) as authoritative, not re-derived. Chain confirmed current: specification_review, plan_review, implementation_verification, security_review, reconciliation all PASS; HUMAN_PR_APPROVAL recorded 2026-09-02T14:52:11Z."
    - "Discovered (Minor, non-blocking): reconciliation v1 section 15's subtotal/total labels (\"Production code (9)\", total \"37\") and section 12's \"33-file\" reference don't match its own itemized list or actual git status (both = 38 files) -- the itemized list itself is complete and correct, independently re-verified against git status --short with no omission. Used the verified total (38) and the verified itemized list in this summary; recommend a future reconciliation-reviewer pass correct the arithmetic labels for hygiene."
    - "Summarized all 3 carried Informational findings (open_decisions.md staleness; SC-5 HttpOnly/Secure wording; login-CSRF rationale) as Risks & Known Limitations for human visibility, not decided here."
    - "Repository state re-confirmed unchanged since reconciliation v1 beyond the expected workflow-state.yaml/history.jsonl advancement (HUMAN_PR_APPROVAL recording) and this stage's own new pr_summary file."
```
