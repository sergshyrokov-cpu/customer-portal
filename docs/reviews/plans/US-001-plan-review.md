---
artifact_type: plan_review
story: US-001
version: 2
status: ARCHIVED
created_at: 2026-08-31T11:05:00Z
updated_at: 2026-09-02T13:11:31Z
produced_by: plan-reviewer
inputs:
  - path: docs/plans/US-001-implementation-plan.md
    version: 2
  - path: docs/stories/US-001-register-customer.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 2
  - path: docs/reviews/specifications/US-001-spec-review.md
    version: 2
  - path: docs/designs/api/US-001-api-design.md
    version: 2
  - path: docs/designs/api/US-001-openapi.yaml
    version: 2
  - path: docs/designs/database/US-001-db-design.md
    version: 2
  - path: docs/designs/database/US-001-entity-model.md
    version: 2
  - path: docs/reviews/designs/US-001-design-review.md
    version: 2
  - path: docs/impact-analysis/US-001-impact-analysis.md
    version: 2
  - path: docs/decisions/US-001-open-decisions.md
    version: 1
supersedes: docs/reviews/plans/US-001-plan-review.md v1
critical_findings: 0
major_findings: 0
minor_findings: 6
---

# Plan Review — US-001 Customer Registration (v2)

## 1. Review Summary

**Verdict: PASS.** Implementation Plan v2 (`docs/plans/US-001-implementation-plan.md`)
is safe, complete, traceable, and executable against the ASP.NET Core/EF
Core/SQLite stack. It supersedes plan v1, which planned Spring Boot/Java
classes deleted this session. The plan implements exactly Specification v2,
API design v2, DB design v2, and the predicted impact surface in
`impact_analysis` v2; introduces no new business behavior; and correctly
sequences a from-scratch build against a verified-empty codebase. All seven
Acceptance Criteria map to at least one planned file and one planned
verification. Layering, DTO/entity separation, the single
`GlobalExceptionHandler`, validation split, and namespace ownership all
follow `architecture.md` / `package-map.md`.

Both judgment calls the plan itself raised were evaluated on their merits,
not rubber-stamped:

- **The SC-4 global fallback authorization policy** (plan Architectural
  Changes item 1, Open Question 2) is **confirmed appropriate** — see §7 and
  F-2 below. This is legitimate completion of an already-approved
  cross-cutting security convention that this Story's own endpoint depends
  on to be meaningfully deny-by-default-compliant, not a new architectural
  decision requiring a fresh Open Decision.
- **The deferred mocking-library decision** (Open Question 1) is
  **confirmed correctly deferred** — see §11 and F-3. Forcing a choice now
  would be premature; `test-writer` may not need one at all.
- **The transaction-boundary interpretation** (`CustomerRepository.AddAsync`
  performs the single `SaveChangesAsync`, not an explicit Service-layer
  `IDbContextTransaction`) is **sound for this Story's single-write scope**,
  but exposes a real, worth-fixing ambiguity between `architecture.md` AD-3's
  Service-centric wording and `package-map.md`'s Repository-only
  `AppDbContext` access rule — see §7 and F-1 (Minor, recommends a future
  `architecture.md` clarification, not a loop-back).

- **Plan readiness:** ready for `HUMAN_PLAN_APPROVAL`.
- **Principal risks:** all three carried from `impact_analysis` v2 (SQLite
  path/folder collision — already observed and cleaned up once;
  EF Core check-constraint syntax unverified against a real migration run;
  the new SC-4 fallback policy is untested wiring) have a named owner step
  and mitigation in the plan.
- **Recommended next action:** proceed to `HUMAN_PLAN_APPROVAL`. No new
  dependency requires confirmation at that gate for *this* plan (unlike v1,
  which needed `spring-boot-starter-validation` confirmed) — the six Minor
  findings below are all advisory.

## 2. Reviewed Artifacts

| Artifact | Path | Version | Status |
|---|---|---|---|
| Implementation Plan | `docs/plans/US-001-implementation-plan.md` | 2 | DRAFT |
| Story | `docs/stories/US-001-register-customer.md` | (unversioned) | IN_PROGRESS |
| Specification | `docs/specifications/US-001-spec.md` | 2 | DRAFT (post-`SPEC_REVIEW` re-approval pending re-record — see note) |
| Specification Review | `docs/reviews/specifications/US-001-spec-review.md` | 2 | DRAFT |
| API Design | `docs/designs/api/US-001-api-design.md` | 2 | DRAFT |
| OpenAPI Contract | `docs/designs/api/US-001-openapi.yaml` | 2 | DRAFT |
| DB Design | `docs/designs/database/US-001-db-design.md` | 2 | DRAFT |
| Entity Model | `docs/designs/database/US-001-entity-model.md` | 2 | DRAFT |
| Design Review | `docs/reviews/designs/US-001-design-review.md` | 2 | DRAFT |
| Impact Analysis | `docs/impact-analysis/US-001-impact-analysis.md` | 2 | DRAFT (PASS; no review stage follows `IMPACT_ANALYSIS`) |
| Open Decisions | `docs/decisions/US-001-open-decisions.md` | 1 | DRAFT (see F-4) |

Note on Specification status: `HUMAN_SPEC_APPROVAL` was recorded for
Specification v2 (`history.jsonl`, 2026-09-01T11:23:27Z) — the `APPROVED`
front-matter promotion is a documentation-lifecycle step separate from the
workflow gate itself, same pattern as v1 (which also stayed technically
`DRAFT`/`APPROVED` inconsistently at points). Not a blocker: the gate is
recorded, which is what `PLAN_REVIEW`'s precondition actually requires.

Architecture / product references consulted (all rewritten this session for
the current stack): `architecture.md` (AD-1..AD-8), `package-map.md`,
`api-conventions.md` (AC-1..AC-9), `persistence-conventions.md` (PC-1..PC-9),
`security-conventions.md` (SC-1..SC-9 + policy block), `business-rules.md`
(BR-001..BR-007), `non-functional-requirements.md` (NFR-001..NFR-008).
Repository state inspected directly: `CustomerPortal.csproj` (EF Core Sqlite
8.0.11, `.Design`, `EFCore.NamingConventions`, `FluentValidation.AspNetCore`,
`BCrypt.Net-Next`, `Swashbuckle.AspNetCore` — all present, matching the
plan's "no new NuGet package" claim), `Program.cs` (confirmed **no**
`AuthorizationOptions.FallbackPolicy` currently set — the plan's Architectural
Changes item 1 premise is accurate), `AppDbContext.cs` /`IAuditable.cs`
(confirmed the audit-override mechanism the plan reuses actually exists),
`GlobalExceptionHandler.cs` (confirmed it currently has only a fallback `_
=> 500` case, matching the plan's Step 6/Modify #13 description), and the
namespace folders (`Controllers/`, `Services/`, `Repositories/`,
`Models/{Entities,Requests,Dtos}/`, `Validation/`, `Security/`,
`Data/Configurations/`, `Exceptions/` — all confirmed empty except
`.gitkeep`, matching the plan's "every production file is a Create" premise).
`CustomerPortal.Tests.csproj` confirmed to reference no mocking library,
supporting the plan's Open Question 1 framing.

### Artifact chain / staleness

The plan's `inputs` front matter references spec v2, spec-review v2,
api-design v2, openapi v2, db-design v2, entity-model v2, design-review v2,
and impact-analysis v2. Every one is the current version; none is
`SUPERSEDED`. No downstream artifact records consuming an older upstream
version. The plan is not stale — review is not `BLOCKED` on that basis.

## 3. Strengths

- **Explicit reconciliation with `impact_analysis` v2** rather than
  re-deriving the file list — the plan states this directly and the file
  lists match line for line on inspection.
- **Two judgment calls made explicit rather than left implicit** (the SC-4
  fallback policy, the `Config/` non-extraction decision) — both argued with
  a stated rationale the reviewer could actually evaluate, rather than
  buried in code comments discovered later at `IMPLEMENTATION_VERIFICATION`.
- **Execution Order** — 14 steps, each with an explicit observable
  completion criterion, correctly sequencing the EF Core migration
  generation as its own gated step (Step 4) rather than an implicit side
  effect, and correctly coupling the exception type with its handler
  mapping (Step 6).
- **Validation and Testing Strategy tables** give deterministic pass
  criteria and map test levels to ACs, correctly deferring the definitive
  suite to `test-writer` at `TEST_WRITING` without pre-empting that stage.
- **Risks section** carries all of `impact_analysis` v2's residual risks
  forward with a named mitigation step, plus one new risk it correctly
  self-identifies (the untested SC-4 policy).
- **Honest new-dependency handling** — explicitly declines to add a mocking
  library itself and states why, rather than silently picking one.
- **Scope discipline** — no `.csproj` change, no `appsettings.json` change,
  no speculative abstraction beyond the two named architectural decisions,
  both traceable to an already-approved convention.

## 4. Scope Review

| Dimension | Assessment |
|---|---|
| Required scope | Fully covered. One endpoint `POST /api/v1/customers`; server-side email + password validation; case-insensitive duplicate → `409`; BCrypt hash; enabled `CUSTOMER` account; UTC audit timestamps; `201` + `Location` + credential-free body. |
| Missing scope | None identified. All FR-1..FR-11 and AC-001..AC-007 have a home in the file list and the execution order. |
| Scope expansion | The SC-4 fallback policy and the `Exceptions`/`GlobalExceptionHandler` extension touch files outside the narrowest possible "just this Story's happy path" — evaluated in §7 and judged in-scope (both are prerequisites for this Story's own security posture to be real, not speculative future-proofing). No other expansion: no mapper library, no extra endpoint, no dev-only profile, no runtime OpenAPI-serving change. |
| Out-of-Scope compliance | Compliant. Spec §10 items (login, password reset, email verification, MFA, activation, profile, rate limiting, admin account management) are all absent from the plan. `GET /api/v1/customers/{id}` and a `UserDetailsService`-equivalent are explicitly not built. |

## 5. Requirements Traceability

| AC | Spec section | Design artifact | Impact Analysis | Plan step | Planned verification |
|---|---|---|---|---|---|
| AC-001 Successful registration | FR-1/FR-5/FR-8/FR-9, §5 | api-design §4 + openapi `201`; db-design §4, entity-model §4.1 | §7, §8, §16 | Steps 1, 7, 10, 11 | Integration + Persistence |
| AC-002 Unique email (case-insensitive) | FR-4, §6.1, §8 | api-design §4.3 `409`; db-design §5 `uq_customer_email` | §7, §8 | Steps 6, 7, 10 | Unit (Service) + Integration + Persistence |
| AC-003 Email validation | FR-3, §6.1 | openapi `RegistrationRequest.email`; api-design §4.1 | §7 | Steps 8, 9 | Unit (Validator) + Integration |
| AC-004 Password storage | FR-5/FR-6/FR-7, §7 | db-design §4.2/§7; entity-model §4.1 | §8, §9 | Steps 5, 10 | Unit (Service) + Persistence |
| AC-005 Secure response | FR-7/FR-8, §7 | api-design §4.2 `CustomerResponse`; openapi schema | §7, §9 | Steps 8, 11 | Integration |
| AC-006 Password policy | FR-3/FR-6, §6.2 | api-design §4.1; design-review D-1/D-6 | §7, §10 | Step 9 | Unit (Validator) + Integration |
| AC-007 Media type | FR-2, §8 | openapi `415`; api-design §4.3 | §7 | Step 12 | Integration |

Every AC is covered by at least one file and one verification activity. No
orphan plan step. No unmet AC.

## 6. Impact Analysis Coverage

| Impact Analysis item (confidence) | Plan disposition |
|---|---|
| All namespaces are **Create** (HIGH) | Covered — Files-To-Create table matches exactly. |
| 3 Modify items against the scaffolded skeleton (HIGH) | Covered — Files-To-Modify table matches exactly, plus the SC-4 policy addition folded into the `Program.cs` row (see §7 for whether that fold is appropriate — judged yes). |
| No new NuGet package required (HIGH) | Covered — confirmed by direct `.csproj` read; plan states this explicitly. |
| Mocking-library open question (Minor risk) | Covered — plan Open Question 1, correctly deferred rather than decided (§11). |
| SQLite path/`Data` folder collision (Minor risk) | Covered — plan Risks section names Step 4/12 smoke checks as the mitigation. |
| EF Core check-constraint syntax unverified (Minor risk) | Covered — plan Step 4 is exactly the verification activity impact analysis asked for. |
| "First production code" informational note | Not separately restated in the plan, but has no actionable disposition to cover — informational only, no gap. |

No HIGH- or MEDIUM-confidence affected area from `impact_analysis` v2 is
ignored or contradicted.

## 7. Architecture Review

| Check | Result |
|---|---|
| Layering `Controllers → Services → Repositories → Models.Entities` (AD-2) | Pass — Step 11 Controller delegates to Step 10 Service; Service uses Step 7 Repository; Repository depends only on `Customer`. |
| Controller has no business logic, no repository access, no entity in a signature (AD-2, AD-4) | Pass — plan states the Controller only delegates and maps status codes. |
| Service owns the transaction boundary (AD-3) | **Evaluated, confirmed sound with a caveat.** `package-map.md` restricts `AppDbContext` access to `Repositories` (`Services` may depend on `Repositories`, `Models.*`, `Exceptions`, `Validation`, `Security` — not `Data`), while AD-3's literal wording ("a Service method that performs a single `SaveChangesAsync()`...") reads as if the Service calls it directly. The plan resolves this by having `CustomerRepository.AddAsync` perform the single `SaveChangesAsync()`, triggered by exactly one Service→Repository call, with no independent transactional decision-making inside the Repository. This preserves AD-3's substance (the Service, not the Repository or Controller, decides the unit of work) even though it can't literally satisfy AD-3's Service-calls-`SaveChangesAsync` phrasing given the stricter dependency rule. **Sound for this Story's single-write scope.** See F-1 — recommend a future `architecture.md` wording clarification so a later Story with a multi-repository-call use case (needing an explicit `IDbContextTransaction`) doesn't hit the same ambiguity unaddressed. |
| DTO / entity boundary (AD-4) | Pass — request binds to `Models.Requests.RegistrationRequest`; response is a `Models.Dtos` type; `Customer` never in a Controller signature or body; mapping in the Service. |
| Validation split (AD-5) | Pass — request-shape via FluentValidation (`Validation/RegistrationRequestValidator`); business-rule (uniqueness) + password re-check in the Service before persistence. |
| Single `GlobalExceptionHandler` (AD-6, AC-9) | Pass — Step 6 is the only handler-extension step; Controller builds no error bodies; `DuplicateEmailException` carries no HTTP concept. |
| Configuration boundaries (AD-7) | **Evaluated, confirmed appropriate.** The plan's decision to add the SC-4 fallback policy directly in `Program.cs` (not extracted to `Config/`) and to leave `Config/` unused this Story are both explicit, reasoned calls within AD-7's stated latitude ("`Config/` ... when `Program.cs` would otherwise grow past a handful of calls" — a threshold this Story's ~4 additions don't cross). See F-2 for the fallback-policy scope-authority question itself. |
| Namespace ownership (`package-map.md`) | Pass — every new class lands in a mapped namespace; no feature namespace introduced (AD-8). |
| Reuse over duplication (AD-8) | Pass — reuses the existing `AppDbContext`, `IAuditable`, `GlobalExceptionHandler`, `ErrorResponse`/`FieldError` as-is; no new dependency. |

No direct Controller-to-Repository access, no business logic in a
Controller, no persistence logic outside the Repository layer, no entity
used as an API type, no unjustified namespace.

## 8. API Review

| Check | Result |
|---|---|
| Contract alignment | Pass — plan targets the approved OpenAPI v2 operation `registerCustomer` verbatim; no invented behavior. |
| Status codes | Pass — `201` (Step 11), `400` (FluentValidation failure + unknown-field/malformed-JSON via `System.Text.Json`), `409` (`DuplicateEmailException`, Step 6), `415` (media type), `500` (fallback, no leak). |
| Request / response models | Pass — `RegistrationRequest { email, password }`; `CustomerResponse { id, email, role, createdAt }` matches OD-004:A and the OpenAPI schema exactly. |
| Validation behavior | Pass — email format/length + password 12–72-byte/character-class rules in the FluentValidation validator (Step 9); policy re-checked in the Service (FR-6). |
| Error mapping | Pass — all mapping in the single handler (Step 6); messages never echo the submitted value (SC-9). |
| Auth / authorization | Pass, and strengthened — `[AllowAnonymous]` on the one new action, now meaningful against a real global default (the new SC-4 fallback policy) rather than against an ambient "nothing is protected anyway" skeleton. |
| Compatibility | Pass — purely additive; no existing contract to break. |
| Planned contract tests | Pass — Testing Strategy's Integration row covers `201`+`Location`+body, both `400` cases, `409`, `415`. |

## 9. Persistence Review

| Check | Result |
|---|---|
| Entity changes | Pass — one entity `Customer` (Step 1) / table `customer`; matches entity-model v2 §2 field-by-field. |
| Explicit constraints / nullability / length | Pass — `CustomerConfiguration` (Step 2) sets `HasMaxLength`/`IsRequired` on every string property, per db-design v2 §4.2/§8.1. |
| Uniqueness | Pass — `uq_customer_email` unique index declared once via Fluent API (PC-4 either/or); case-insensitivity via Service lowercasing before both the check and the insert (OD-006:A). |
| Indexes | Pass — the unique index serves the `email` lookup (PC-7); no redundant index; no FKs. |
| Schema initialization | Pass — Step 4 generates a committed EF Core Migration, explicitly diffed against db-design v2 §8.2 before proceeding — no `EnsureCreated()`/`EnsureDeleted()` shortcut anywhere in the plan (PC-2, SC-8). |
| Migration implications | None — new table, no data, first migration in the project. |
| Auditing | Pass — `Customer` implements the already-scaffolded `IAuditable`, reusing `AppDbContext.SaveChangesAsync`'s override; no per-entity audit code needed (PC-6, BR-007). |
| Persistence tests | Pass — Testing Strategy's Persistence row asserts column constraints, the case-insensitive duplicate collision, and UTC audit timestamps against isolated in-memory SQLite. |

The two db-design-carried risks (EF Core check-constraint syntax; the
SQLite path collision) are correctly gated at Step 4/12 rather than
deferred silently — acceptable, execution-time integration details, not
plan defects.

## 10. Security Review

| Area | Result |
|---|---|
| Authentication boundary | Addressed, and — per §7 — **actually made real** by the new SC-4 fallback policy; without it, `[AllowAnonymous]` on the one endpoint would be indistinguishable from an unprotected-by-default skeleton. |
| Authorization boundary | Addressed — none required for the operation; no other route exists yet to misconfigure. |
| Password hashing | Addressed — `IPasswordHasher`/BCrypt (`BCrypt.Net-Next`) in `Security/` (Step 5); no-op/plaintext hasher forbidden (SC-1, SEC-2). |
| Password exposure | Addressed — `password` only on the inbound DTO; `Customer` is a plain class (not a `record`), so its default `ToString()` doesn't render `PasswordHash` (entity-model v2 §2.1 note); `CustomerResponse` has no credential field. |
| Dual-layer policy | Addressed — FluentValidation rule (Step 9) at the request layer **and** a re-check in the Service before hashing (Step 10, FR-6). |
| Input validation | Addressed — server-side only; unknown fields rejected via `System.Text.Json` `UnmappedMemberHandling.Disallow` (design-review D-7). |
| Sensitive-data logging | Addressed — validation messages are static/generic (SC-9); the fallback `500` case leaks nothing. |
| Database browser/admin UI | Addressed — none registered or exposed; this Story does not touch that posture. |
| Dev-only / insecure config | None — file SQLite for local, isolated in-memory for tests; committed migrations only. |
| Secret management | Addressed — no secret introduced; `./gitignore` already covers generated `.db*` files. |
| Security tests | Addressed — Testing Strategy's Security row asserts the response never contains credentials and that only the new endpoint is anonymous-accessible under the new fallback policy. |

The Story handles passwords, credentials, and account state, and the plan
contains explicit security steps (Steps 5, 9, 10, 12). No blocking security
finding. Final verification of the fallback-policy scope and the
antiforgery exemption is owned by `SECURITY_REVIEW`.

## 11. Testing and Validation Review

- **AC coverage:** every AC (§5 table) maps to at least one planned test
  level. The 72-**byte** (not character) password boundary is explicitly
  called out (spec-review F-5, carried).
- **Test categories:** unit (Service, Validator), integration (HTTP
  round-trip via `WebApplicationFactory<Program>`), persistence (isolated
  in-memory SQLite), security — matches NFR-5 and `impact_analysis` v2 §10.
- **Negative scenarios:** invalid email, password policy violations,
  duplicate email (same/different case), wrong `Content-Type`, unknown JSON
  field, malformed JSON, credential absent from the response — all listed
  in the Testing Strategy.
- **Deterministic validation:** Validation Strategy gives an observable
  pass criterion per concern; Step 13's manual smoke pass is explicitly
  stated as superseded once real tests exist — correctly not treated as the
  final gate.
- **Mocking-library deferral — evaluated, confirmed appropriate.**
  `IMPLEMENTATION_PLANNING`'s own Prohibited list forbids adding a
  dependency (only proposing one for human approval); `TEST_WRITING` hasn't
  yet decided whether `CustomerService` unit tests need a mock or can use a
  hand-written fake, so a decision now would be premature and could commit
  to an unnecessary dependency. Correctly flagged, correctly not decided
  here — see F-3.
- **Structure vs behavior:** planned tests target observable outcomes
  (status codes, headers, persisted values, hash verification), not
  implementation shape.
- **Ownership:** the plan correctly states `test-writer` owns the
  definitive AC→test matrix; the Testing Strategy table is indicative, not
  pre-emptive.
- **Missing evidence:** none blocking. See F-5 — the "step 13 manual smoke
  pass" language could be read as a permanent substitute for real tests if
  a reader skims; the plan does say it's superseded, but one clarifying
  word would help.

## 12. Execution Order Review

The 14-step order is dependency-safe: entity → configuration → `DbSet` →
**migration generation and diff** (explicitly its own gated step, not
folded into entity creation) → password hasher → exception type + handler
(coupled, as instructed) → repository → DTOs → validator → service →
controller → `Program.cs` wiring (including the new fallback policy) →
full validation → documentation reconciliation. This matches the canonical
contract-confirmation → persistence → service → API → security → validation
→ build → verification guidance closely, with the one deliberate
elaboration (splitting migration generation into its own step) explained
and justified by the residual risk it exists to catch.

## 13. Reviewability

The change is one reviewable Pull Request: a single project, ~11 new files
+ 3 small modifications, zero new dependencies, one new table, one endpoint.
No unrelated module, no broad refactor, no multiple independent
capabilities. No decomposition needed.

## 14. Findings

### F-1 — AD-3 / `package-map.md` transaction-boundary wording ambiguity

- **Severity:** Minor
- **Location:** `architecture.md` AD-3; `package-map.md` `Repositories`
  row; plan Architectural Changes section.
- **Problem:** AD-3 is written as if the Service layer calls
  `SaveChangesAsync()` directly, but `package-map.md` allows only
  `Repositories` to depend on `Data` (i.e. `AppDbContext`). The plan
  resolves this soundly for a single-write flow (§7), but the underlying
  architecture-document wording gap is real and will resurface for the
  first Story with a multi-repository-call transactional flow.
- **Why it matters:** a future planner without this Story's worked example
  could read AD-3 literally and either (a) try to give `Services` illegal
  `Data` access, or (b) not realize a Repository method can legitimately
  own its own single `SaveChangesAsync()`.
- **Required correction:** none by this Planner — recommend `architecture.md`
  AD-3 gets a documentation clarification (e.g. "a Service triggers the
  transaction boundary either by calling a single Repository method that
  itself performs `SaveChangesAsync()` for a one-operation flow, or by
  opening an explicit `IDbContextTransaction` around multiple Repository
  calls") in a future revision. Not this Story's scope to fix.
- **Loop-back target:** none — advisory, does not block this plan.

### F-2 — SC-4 global fallback policy: authority to add it in `IMPLEMENTATION_PLANNING`

- **Severity:** Minor (confirmation)
- **Location:** plan Architectural Changes item 1; Open Question 2.
- **Problem / evaluation:** the plan adds a project-wide `Program.cs`
  change (a default-deny authorization fallback policy) that no single
  upstream artifact for *this Story* explicitly instructs — it derives it
  from `security-conventions.md` SC-4, a already-approved, standing,
  cross-cutting convention, not from anything Story-specific.
- **Why it's acceptable:** SC-4 already mandates "every endpoint requires
  authentication unless the approved API design lists it as public." This
  Story's own API design *does* mark its one endpoint public — but that
  marking is only meaningfully "the exception" if a default-deny rule
  actually exists to be an exception *to*. Without the fallback policy, the
  Testing Strategy's own Security row ("only the new endpoint is
  anonymous-accessible") would be **unfalsifiable** — there would be no
  other endpoint's protection to compare against, and no mechanism
  enforcing it if one were later added carelessly. Implementing SC-4 now is
  therefore load-bearing for this Story's own claimed security posture to
  be real and testable, not an unrelated repository-wide fix smuggled in.
- **Required correction:** none. **Confirmed appropriate for this plan to
  include.** Recommend the human reviewer at `HUMAN_PLAN_APPROVAL` note
  this explicitly when approving (the plan already flags it as Open
  Question 2 for exactly that visibility).
- **Loop-back target:** none.

### F-3 — Mocking-library decision deferral to `TEST_WRITING`

- **Severity:** Minor (confirmation)
- **Location:** plan New Dependencies section; Open Question 1.
- **Problem / evaluation:** whether `CustomerService` unit tests need
  `Moq`/`NSubstitute` or can use hand-written fakes is unresolved.
- **Why it's acceptable:** `IMPLEMENTATION_PLANNING` is explicitly
  prohibited from adding dependencies (only proposing them for approval);
  the actual need depends on `test-writer`'s concrete test design, which
  doesn't exist yet. Deciding now would risk adding an unnecessary
  dependency or picking the wrong one before the real requirement is known.
- **Required correction:** none. **Confirmed appropriate to defer.**
  `TEST_WRITING` should raise it explicitly (with a proposed package) if it
  turns out to be needed, per the same new-dependency approval pattern.
- **Loop-back target:** none.

### F-4 — `open_decisions.md` v1 still marks OD-001..OD-006 as `status: OPEN`

- **Severity:** Minor (advisory; carried unchanged from v1's F-1)
- **Location:** `docs/decisions/US-001-open-decisions.md` v1.
- **Problem:** all six were resolved by the human at `HUMAN_SPEC_APPROVAL`
  (re-confirmed 2026-09-01T11:23:27Z after the Specification v2 correction)
  and are applied consistently across the Specification, both designs, the
  design review, the impact analysis, and this plan. The artifact body was
  never re-published.
- **Required correction:** none by the Planner. `us-clarifier` should
  publish v2 marking each `RESOLVED`.
- **Loop-back target:** none.

### F-5 — Step 13's "manual smoke pass" wording could read as a permanent substitute for tests

- **Severity:** Minor
- **Location:** plan Execution Order, Step 13.
- **Problem:** the step describes a manual `curl`/`.http`-file check of
  each status code, and states it is "pending `TEST_WRITING`'s real
  automated tests, which supersede this manual check once they exist" —
  correct, but a reader skimming just the step's evidence line could miss
  that qualifier.
- **Why it matters:** low — the qualifying sentence is present, just easy
  to skim past.
- **Required correction:** optional wording tweak (e.g. bold "temporary" in
  front of "manual check") on a future revision; not required for approval.
- **Loop-back target:** none.

### F-6 — New-dependency confirmation is a non-event for this plan (contrast with v1)

- **Severity:** Minor (informational, not a defect)
- **Location:** plan New Dependencies section.
- **Problem / note:** v1's plan review (F-5) required the human to confirm
  a new first-party dependency at `HUMAN_PLAN_APPROVAL`
  (`spring-boot-starter-validation`). This plan needs **no** new package —
  `FluentValidation.AspNetCore` and `BCrypt.Net-Next` were already added
  during Stage-3 scaffolding. Recorded so the human approving this gate
  knows there is nothing dependency-related to confirm here (only the
  mocking-library *question*, F-3, which is explicitly not a decision to
  make now).
- **Required correction:** none.
- **Loop-back target:** none.

No `Critical` findings. No `Major` findings.

## 15. Open Decisions

**No blocking Open Decisions were identified.** OD-001..OD-006 were all
resolved by the human at `HUMAN_SPEC_APPROVAL` and re-confirmed after the
Specification v2 correction, applied consistently across every upstream
artifact and this plan. The `open_decisions.md` file body still shows
`OPEN` — documentation lag owned by `us-clarifier` (F-4), not an unresolved
decision, and it does not block execution.

No unresolved `TODO`/`TBD`/`FIXME`/`???`/"to be decided" marker was found in
any input artifact that would affect implementation.

## 16. Required Plan Changes

None are required before `HUMAN_PLAN_APPROVAL`. Optional clarity
improvements the Planner may fold into a future revision (none blocks
execution): F-5's wording tweak. Separately (not a Planner action):
`us-clarifier` should publish `open_decisions.md` v2 (F-4); a future
`architecture.md` revision should clarify AD-3's transaction-boundary
wording (F-1).

## 17. Verdict Rationale

The plan has zero `Critical` and zero `Major` findings. It covers every
Acceptance Criterion with a planned file and a planned verification; it is
consistent with the approved Specification v2, the approved API and
database designs, the design review, and the predicted impact surface; it
adds no business behavior beyond what implementing already-approved
conventions (SC-4) requires; it complies with AD-2..AD-8 and
`package-map.md`, with one worth-noting-but-not-blocking wording ambiguity
in AD-3 (F-1); its security handling is explicit and sufficient, and is
strengthened rather than merely satisfied by the new fallback policy; its
execution order is dependency-safe with observable evidence per step; and
the change is reviewable as a single Pull Request. The two judgment calls
the plan itself raised (SC-4 policy authority, mocking-library deferral)
were independently evaluated and both confirmed sound. The six Minor
findings are advisory and are carried as `non_blocking_findings`.

Per `stage-map.yaml`, `PLAN_REVIEW` with verdict `PASS` advances to
`HUMAN_PLAN_APPROVAL`. A `PASS` here is not human approval — a human
records the decision with `/so:approve` (or `/so:reject`).

```yaml
result:
  verdict: PASS
  stage: PLAN_REVIEW
  story: US-001
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/plans/US-001-plan-review.md
  next_stage: HUMAN_PLAN_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "F-1: architecture.md AD-3's Service-centric SaveChangesAsync wording is in tension with package-map.md's Repository-only Data access rule. The plan's resolution (Repository owns the single SaveChangesAsync for a one-write flow) is sound here; recommend AD-3 gets a clarifying revision before a multi-repository-call Story hits the same ambiguity unaided."
    - "F-2 (confirmed): the new SC-4 global fallback authorization policy is legitimate completion of an already-approved convention, load-bearing for this Story's own claimed security posture to be testable -- not scope creep, not a new Open Decision needed."
    - "F-3 (confirmed): deferring the mocking-library choice to TEST_WRITING is correct -- IMPLEMENTATION_PLANNING may only propose dependencies, and the real need isn't known until test design happens."
    - "F-4 (carried): docs/decisions/US-001-open-decisions.md v1 still shows OD-001..OD-006 as OPEN; resolutions are authoritative in history.jsonl. us-clarifier should publish v2 marking them RESOLVED."
    - "F-5: plan Step 13's manual-smoke-check wording could be misread as a permanent test substitute; the superseding clause is present but easy to skim past. Optional wording tweak."
    - "F-6 (informational): unlike v1, this plan needs zero new NuGet dependencies for production code -- nothing dependency-related to confirm at HUMAN_PLAN_APPROVAL."
```
