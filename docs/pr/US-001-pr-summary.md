---
artifact_type: pr_summary
story: US-001
version: 1
status: ARCHIVED
created_at: 2026-09-01T13:22:25Z
updated_at: 2026-09-02T13:11:31Z
produced_by: pr-preparer
inputs:
  - path: docs/stories/US-001-register-customer.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 2
  - path: docs/impact-analysis/US-001-impact-analysis.md
    version: 2
  - path: docs/plans/US-001-implementation-plan.md
    version: 2
  - path: docs/evidence/US-001-implementation-report.md
    version: 4
  - path: docs/verification/US-001-implementation-verification.md
    version: 2
  - path: docs/reviews/security/US-001-security-review.md
    version: 1
  - path: docs/reviews/reconciliation/US-001-reconciliation.md
    version: 3
  - path: docs/reconciliation/US-001-traceability.md
    version: 3
supersedes: null
---

# Pull Request Summary — US-001 Customer Registration

## Story & Business Goal

**As a** Customer, **I want to** register a new account using my email
address and password, **so that** I can access the Customer Portal.
Enables self-service registration without administrator involvement — an
MVP success criterion (`product-vision.md`).

**Stack note:** this is the merge-ready delivery of US-001 on
**ASP.NET Core / EF Core / SQLite**. An earlier Spring Boot implementation
of this same Story was completed, then superseded this session by an
explicit, human-directed technology re-platform of the whole project; that
Spring Boot code was deleted and is not part of this PR. This PR delivers
the Story fresh on the new stack, through a full second delivery cycle
(Specification → design → planning → tests → implementation → independent
verification → security review → reconciliation).

## Scope / Implemented Features

- `POST /api/v1/customers` — public, self-service Customer registration.
- Server-side email and password validation (FluentValidation), including
  the 72-**byte** BCrypt input bound (not 72 characters).
- Case-insensitive email uniqueness (normalized to lowercase in the
  Service layer before both the check and the insert).
- BCrypt password hashing (`BCrypt.Net-Next`, default work factor); no
  plaintext password ever persisted, logged, or returned.
- Project-wide SC-4 deny-by-default authorization fallback policy
  (this Story adds the project's first endpoint, so this was the correct
  point to wire it), with the registration endpoint explicitly
  `[AllowAnonymous]`.
- Uniform `ErrorResponse` shape across every status this Story produces
  (`400`/`409`/`415`/`500`).

**Out of scope** (unchanged from the Specification): login/authentication
(US-002), password reset, email verification, MFA, account activation
workflow, profile management, rate limiting/anti-abuse, administrative
account management.

## API Changes

New operation only — purely additive, no existing contract touched (none
existed before this Story on this stack).

| Method | Path | Auth | Success | Errors |
|---|---|---|---|---|
| `POST` | `/api/v1/customers` | Public (`[AllowAnonymous]`) | `201 Created` + `Location: /api/v1/customers/{id}` + `CustomerResponse` | `400` (validation / malformed / unknown field), `409` (duplicate email), `415` (wrong/missing `Content-Type`), `500` (unmapped) |

`CustomerResponse`: `id`, `email`, `role` (always `"CUSTOMER"`),
`createdAt`. No credential field on the type at all. Full contract:
`docs/designs/api/US-001-openapi.yaml` v2.

## Database Changes

One new table, `customer` (surrogate `id`, `email` unique/≤254 chars,
`password_hash` 60 chars, `role`, `enabled`, `created_at`/`updated_at`
UTC), via a single, committed, reviewed EF Core Migration
(`Data/Migrations/20260901122138_AddCustomer.cs`) that was independently
confirmed to match the approved database design exactly. SQLite,
file-based for local/dev (`./App_Data/customer-portal.db`, not committed),
isolated in-memory for tests. No relationships, no foreign keys.

## Security Changes

- BCrypt hashing confirmed real (default, non-trivial work factor), not a
  stub.
- Dual-layer password-policy enforcement: FluentValidation at the request
  layer (primary, fully tested) plus a Service-layer re-check (FR-6,
  defense in depth — independently confirmed provably unreachable via the
  application's only entry point, so not itself a live control, but not a
  gap either).
- Project's first deny-by-default authorization fallback policy (SC-4),
  independently confirmed to correctly exempt only the one public
  endpoint.
- New content-type-enforcement middleware (added mid-cycle to fix a `415`
  contract-shape defect implementation verification found) independently
  confirmed to introduce no DoS or information-disclosure risk.
- Full adversarial security review performed and passed — see
  `docs/reviews/security/US-001-security-review.md` for the complete
  analysis, including three forward-looking (non-blocking) observations
  in **Risks & Known Limitations** below.

## Tests Executed (evidence references)

Authoritative evidence owned by `implementation-verifier`
(`docs/verification/US-001-implementation-verification.md` v2, PASS,
independently re-reproduced, not merely re-stated here):

- `dotnet build` — clean, 0 warnings, 0 errors.
- `dotnet test` — **32/32 pass** (21 test methods; one `[Theory]` expands
  to 5 cases), covering unit (Service, Validator), integration (real HTTP
  pipeline via `WebApplicationFactory<Program>`), persistence (isolated
  in-memory SQLite, EF Core model-metadata inspection), and security
  (response never contains credentials; the SC-4 fallback policy is
  actually configured).
- `dotnet format --verify-no-changes` — clean.
- Live smoke checks (this session, twice — before and after the `415`
  fix): happy path, duplicate email, weak password, wrong `Content-Type`,
  Swagger reachability — all reproduced with real HTTP evidence, not
  simulated.

## Acceptance Criteria Coverage

From `docs/reconciliation/US-001-traceability.md` v3 (authoritative,
consumed here, not re-derived) — **7 / 7 RECONCILED**:

| AC | Description | Status |
|---|---|---|
| AC-001 | Successful registration → `201` + `Location` + safe body | RECONCILED |
| AC-002 | Duplicate email (case-insensitive) rejected | RECONCILED |
| AC-003 | Invalid email format rejected | RECONCILED |
| AC-004 | Password stored only as a BCrypt hash | RECONCILED |
| AC-005 | Response excludes password and hash | RECONCILED |
| AC-006 | Password policy enforced | RECONCILED |
| AC-007 | Wrong/missing media type → `415` | RECONCILED |

Every row has an approved requirement, an implementation location, an
executable test, independent functional verification, and independent
security evidence — see the traceability artifact for the full per-AC
detail.

## PR Candidate Files — Include

From `docs/reviews/reconciliation/US-001-reconciliation.md` v3 §15
(authoritative, consumed here) — **61 files**, grouped:

- **Story business logic** (25 new + 5 modified files under
  `CustomerPortal/` and `CustomerPortal.Tests/`, + 8 `.gitkeep` deletions):
  the entity, EF Core configuration and migration, repository, service,
  validator, DTOs, controller, security/hashing, exception types, and the
  full test suite.
- **Build tooling** (1 file): `.config/dotnet-tools.json` (local
  `dotnet-ef` tool manifest, dev-only, required to author the migration —
  no runtime footprint).
- **Repository hygiene** (1 file): `.gitignore` (`App_Data` pattern
  correction).
- **Story delivery-process documentation** (24 files): Specification,
  API/DB designs, all reviews, plan, tests, evidence, verification,
  security review, and this Story's own reconciliation/traceability
  artifacts — plus one architecture-convention correction
  (`docs/architecture/persistence-conventions.md`, the `App_Data` path
  fix).
- **Workflow bookkeeping** (5 files):
  `docs/workflow/workflow-state.yaml`, `history.jsonl` (this Story's
  transition log), `artifact-paths.yaml`, `stage-map.yaml`, `stages.md`.
  **Reviewer note:** the latter three are a harness-routing fix (renaming
  a retired skill reference so the workflow orchestrator could route this
  Story's `IMPLEMENTATION` stage at all), not Story business logic —
  `reconciliation` v3 recommends including them as-is but flags that a
  reviewer may prefer to split them into a separate commit for cleaner
  history. Not a blocker either way.

Full per-file list with classification rationale: see
`reconciliation` v3 §15 directly.

## PR Candidate Files — Exclude

**None.** `reconciliation` v3 independently confirmed zero generated
runtime artifact (no `.db*` file), zero local-only configuration, zero
secret, and zero unrelated change anywhere in the diff.

## Files Needing Human Decision

None beyond the single reviewer note already surfaced above (workflow
bookkeeping files) — not a blocking decision, a stylistic one.

## Risks & Known Limitations

Five Minor findings, all non-blocking, carried from
`implementation_verification` v2 and `security_review` v1 and
cross-checked consistent by `reconciliation` v3 — none requires action
before merge, all are recommendations for a **future** Story:

1. **`Location` header test coverage** — the `201` integration test
   asserts the header is present but not its exact value. Current
   behavior is independently confirmed correct (twice); a real bug in
   this exact area was caught by manual testing during implementation,
   not by this test. Recommend strengthening the assertion later.
2. **FR-6 Service-layer re-check has no direct unit test** — the code is
   correct by inspection and provably unreachable via the application's
   only entry point (the primary, fully-tested request-layer validator
   blocks non-compliant passwords first). Recommend a direct unit test
   later for completeness.
3. **`architecture.md` AD-3 wording** — the "Service triggers
   `SaveChangesAsync`" phrasing is in tension with `package-map.md`'s
   Repository-only database-context-access rule. This Story's resolution
   (Repository owns the single save for its one-write flow) was
   independently judged sound at every review; recommend a documentation
   clarification for a future Story with a multi-step transaction.
4. **Cookie-authentication default challenge behavior** — configured with
   framework defaults, which redirect (`302`) unauthenticated requests
   rather than returning `401` as `security-conventions.md` SC-3
   requires. **Not observable today** — no protected endpoint exists yet
   in the application (registration is correctly public). Must be
   addressed before or alongside the next Story that adds an authenticated
   endpoint (e.g. login).
5. **Concurrent duplicate registration** — a narrow race between the
   uniqueness check and the insert can produce `500` instead of `409`
   under simultaneous identical requests; the unique database constraint
   still fully prevents the duplicate account (no data-integrity or
   confidentiality impact). Explicitly accepted per the approved,
   unchanged Open Decision OD-005:A (anti-abuse/concurrency out of scope
   for this Story) — same disposition the retired Spring Boot
   implementation received for the identical issue. Also: `AddAntiforgery()`
   is registered but has no enforcement wired anywhere yet — harmless
   today (no non-exempt endpoint exists), but must be wired explicitly
   when a future Story adds one.

No Critical or Major finding exists anywhere in this Story's delivery
chain.

## Release Notes

**User-visible:** customers can now self-register an account via
`POST /api/v1/customers`.

**Technical:** first production code on the ASP.NET Core/EF Core/SQLite
stack (project's first entity, first migration, first controller/service/
repository, first project-wide authorization policy). Establishes the
pattern later Stories are expected to follow.

**Security:** BCrypt password hashing; deny-by-default authorization
posture established project-wide; no credential ever exposed in a
response, log, or error.

## Notes For Reviewers

- This Story's own artifact chain includes two full delivery cycles — the
  first (Spring Boot) is entirely superseded and not part of this diff;
  only the second (ASP.NET Core) cycle's output is proposed here.
- The one workflow-scope note above (bundling harness-routing fixes with
  this Story's commit) is worth a quick look but is not blocking.
- Five Minor, non-blocking findings are summarized above with full detail
  in `implementation_verification` v2 and `security_review` v1 — none
  requires a change before merge.

## Readiness Result

```yaml
result:
  verdict: PASS
  stage: PR_PREPARATION
  story: US-001
  artifact_status: APPROVED
  artifacts:
    - docs/pr/US-001-pr-summary.md
  next_stage: READY_FOR_PR
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "Consumed reconciliation v3's PR candidate scope (61 include / 0 exclude) and traceability v3 (7/7 AC RECONCILED) as authoritative, not re-derived. Repository state independently re-confirmed unchanged since reconciliation v3 ran (only workflow-state.yaml/history.jsonl advanced further, as expected for HUMAN_PR_APPROVAL/PR_PREPARATION's own transitions)."
    - "Surfaced (not decided) the one reviewer note: docs/workflow/{artifact-paths.yaml,stage-map.yaml,stages.md} are a harness-routing fix bundled with this Story's delivery; reconciliation v3 recommends including as-is, human may split into a separate commit if preferred."
    - "5 Minor findings summarized accurately from implementation_verification v2 / security_review v1, all confirmed non-blocking by reconciliation v3."
```
