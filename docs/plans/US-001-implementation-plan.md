---
artifact_type: implementation_plan
story: US-001
version: 2
status: ARCHIVED
created_at: 2026-08-31T10:24:32Z
updated_at: 2026-09-02T13:11:31Z
produced_by: implementation-planner
inputs:
  - path: docs/stories/US-001-register-customer.md
    version: null
  - path: docs/decisions/US-001-open-decisions.md
    version: 1
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
supersedes: docs/plans/US-001-implementation-plan.md v1
---

# Implementation Plan — US-001 Customer Registration (v2)

## Goal

Implement `POST /api/v1/customers` self-registration end to end on the
ASP.NET Core/EF Core/SQLite stack, exactly as specified in Specification v2
and designed in API design v2 / DB design v2, against the currently
logic-free `CustomerPortal` skeleton. Supersedes v1, which planned
Spring Boot/Java classes under `org.example.customerportal` that were
deleted this session as part of the project's technology re-platform.

## Source Artifacts

All consumed at the versions listed in the front matter `inputs` above; none
`SUPERSEDED`. `impact_analysis` v2 is the required predicted-change-surface
input — this plan sequences its Create/Modify list rather than re-deriving
it.

## Architectural Changes

Two decisions this plan makes explicit, both required to correctly implement
already-approved conventions rather than new scope:

1. **Global deny-by-default authorization fallback (SC-4).** The current
   `Program.cs` (Stage-3 skeleton) calls `AddAuthorization()` with no
   fallback policy, so a future Controller action with no `[Authorize]`
   attribute would be public by accident — the opposite of SC-4's
   deny-by-default posture. Because this Story adds the project's **first**
   endpoint, this plan includes configuring
   `options.FallbackPolicy = new AuthorizationPolicyBuilder()
   .RequireAuthenticatedUser().Build()` and marking the new registration
   action `[AllowAnonymous]` explicitly (Step 12). This is implementing an
   already-approved convention (SC-4), not introducing new behavior.
2. **`Config/` namespace stays unused for this Story.** `impact_analysis`
   v2 §6 left open whether this Story's ~5 new DI registrations warrant
   extracting `Config/` extension methods per AD-7 ("once `Program.cs`
   would otherwise grow past a handful of calls"). Decision: **no** — three
   new single-line `AddScoped<...>()` registrations plus the one
   authorization-policy change do not cross that threshold on their own.
   Revisit at the next Story that adds DI registrations if `Program.cs`
   keeps growing.

No other architecture-level decision is introduced. Repository/Service
transaction boundary note: this Story's single write (`INSERT` into
`customer`) has exactly one `SaveChangesAsync()` call, made inside
`CustomerRepository.AddAsync` (the only class with `AppDbContext` access
per `package-map.md`) — consistent with AD-3's simpler branch ("a Service
method that performs a single `SaveChangesAsync()` relies on EF Core's
implicit... transaction — no explicit transaction object needed"); the
Service triggers exactly one such call by invoking the repository once, so
no explicit `IDbContextTransaction` is needed. A future Story whose Service
method must span **multiple** repository calls in one unit of work would
need the Service to open an explicit transaction per AD-3's other branch —
not required here.

## Impact-Analysis Reconciliation

This plan's Files To Create / Files To Modify below reproduce
`impact_analysis` v2 §6 exactly, with no material difference. The only
addition beyond §6 is the Architectural Changes item 1 above (the global
fallback policy), which impact analysis did not call out as a separate file
change because it's a one-line addition inside the already-listed
`Program.cs` Modify entry — folded into Step 12 below, not a new file.

## Files To Create

| # | Path | Responsibility |
|---|---|---|
| 1 | `CustomerPortal/Models/Entities/Customer.cs` | persisted entity, implements `IAuditable` |
| 2 | `CustomerPortal/Data/Configurations/CustomerConfiguration.cs` | `IEntityTypeConfiguration<Customer>` Fluent API (db-design v2 §8.1) |
| 3 | `CustomerPortal/Models/Requests/RegistrationRequest.cs` | inbound DTO |
| 4 | `CustomerPortal/Models/Dtos/CustomerResponse.cs` | outbound DTO |
| 5 | `CustomerPortal/Validation/RegistrationRequestValidator.cs` | FluentValidation: email format, password policy |
| 6 | `CustomerPortal/Security/IPasswordHasher.cs` + `BCryptPasswordHasher.cs` | BCrypt-backed hasher (SC-1, SC-2) |
| 7 | `CustomerPortal/Exceptions/DuplicateEmailException.cs` | domain exception for FR-4 / OD-003:A |
| 8 | `CustomerPortal/Repositories/ICustomerRepository.cs` + `CustomerRepository.cs` | `FindByEmailAsync`, `ExistsByEmailAsync`, `AddAsync` (owns the single `SaveChangesAsync`, see Architectural Changes) |
| 9 | `CustomerPortal/Services/ICustomerService.cs` + `CustomerService.cs` | registration orchestration (FR-3..FR-9) |
| 10 | `CustomerPortal/Controllers/CustomersController.cs` | `POST /api/v1/customers` action |
| 11 | `CustomerPortal/Data/Migrations/<generated>_AddCustomer.cs` (+ designer + snapshot) | EF Core migration — **generated by tooling in Step 4, not hand-authored** |

## Files To Modify

| # | Path | Change |
|---|---|---|
| 12 | `CustomerPortal/Data/AppDbContext.cs` | add `public DbSet<Customer> Customers => Set<Customer>();` |
| 13 | `CustomerPortal/Exceptions/GlobalExceptionHandler.cs` | extend the exception-type switch: `DuplicateEmailException` → `409`; the request-shape/validation failure path → `400` |
| 14 | `CustomerPortal/Program.cs` | register `ICustomerRepository`/`CustomerRepository`, `ICustomerService`/`CustomerService`, `IPasswordHasher`/`BCryptPasswordHasher` as scoped services; add the SC-4 fallback authorization policy (Architectural Changes item 1) |

No `.csproj` change (impact analysis confirmed no new NuGet package is
required for production code). No `appsettings.json` change (connection
string and `Persistence:AutoMigrate` already correct).

## Execution Order

Numbered, dependency-ordered. Each step names its observable completion
evidence — `IMPLEMENTATION` records the actual command/output in the
Implementation Report, this plan only states what "done" looks like.

1. **Create `Customer` entity** (`Models/Entities/Customer.cs`) per
   entity-model v2 §2.1's exact class shape, implementing `IAuditable`.
   *Evidence:* file exists; `dotnet build` succeeds (entity alone has no
   dependents yet to break).
2. **Create `CustomerConfiguration`**
   (`Data/Configurations/CustomerConfiguration.cs`) per db-design v2 §8.1.
   *Evidence:* `dotnet build` succeeds; `ApplyConfigurationsFromAssembly()`
   in `AppDbContext.OnModelCreating` (already present) picks it up
   automatically — no `AppDbContext` change needed for this step alone.
3. **Add `DbSet<Customer>` to `AppDbContext`** (Modify #12).
   *Evidence:* `dotnet build` succeeds.
4. **Generate and review the EF Core migration**:
   `dotnet ef migrations add AddCustomer --project CustomerPortal`. Compare
   the generated `Up()` against db-design v2 §8.2's illustrative shape
   (table name, column types/affinities, `pk_customer`, `uq_customer_email`,
   `ck_customer_role`, the `Sqlite:Autoincrement` annotation on `id`).
   Record any deviation from §8.2 in the Implementation Report as a
   documented design-vs-generated difference — do not silently accept a
   mismatch (db-design v2 D-10 / impact-analysis v2 §13 risk). *Evidence:*
   migration files exist under `Data/Migrations/`; the diff against §8.2 is
   recorded; local `Database.Migrate()` (via `Persistence:AutoMigrate` in
   Development) applies it cleanly against a fresh SQLite file.
5. **Create `IPasswordHasher` + `BCryptPasswordHasher`**
   (`Security/`), wrapping `BCrypt.Net.BCrypt.HashPassword`/`Verify` (SC-1).
   *Evidence:* `dotnet build` succeeds; no dependents yet.
6. **Create `DuplicateEmailException`** (`Exceptions/`) **and extend
   `GlobalExceptionHandler`'s switch** (Modify #13) in the same step, since
   the exception type and its mapping are coupled and neither is useful
   without the other. *Evidence:* `dotnet build` succeeds; a throwaway unit
   test (or manual check, superseded by `TEST_WRITING`'s real tests) confirms
   `DuplicateEmailException` maps to `409` with the AC-6 body.
7. **Create `ICustomerRepository`/`CustomerRepository`**
   (`Repositories/`) — `FindByEmailAsync`, `ExistsByEmailAsync`, `AddAsync`
   (the latter performs the single `SaveChangesAsync`, per Architectural
   Changes). *Evidence:* `dotnet build` succeeds.
8. **Create `RegistrationRequest`** (`Models/Requests/`) and
   **`CustomerResponse`** (`Models/Dtos/`) per api-design v2 §4.1/§4.2.
   *Evidence:* `dotnet build` succeeds; shapes match `openapi.yaml` v2
   exactly (field names, nullability, `writeOnly` on `password`).
9. **Create `RegistrationRequestValidator`** (`Validation/`) — email
   format + length (OD-001:A), password 12–72 length + character-class
   rules (Spec §6.2), enforced as bytes for the 72 bound per spec-review
   F-5. *Evidence:* `dotnet build` succeeds; registered automatically via
   `AddValidatorsFromAssembly` (already wired in `Program.cs`).
10. **Create `ICustomerService`/`CustomerService`** (`Services/`) —
    normalize email to lowercase (OD-006:A), check uniqueness via the
    repository, hash the password via `IPasswordHasher`, construct the
    entity (`Role = "CUSTOMER"`, `Enabled = true`), persist via the
    repository, map to `CustomerResponse`. Throws `DuplicateEmailException`
    on collision. *Evidence:* `dotnet build` succeeds.
11. **Create `CustomersController`** (`Controllers/`) — `POST
    /api/v1/customers`, `[AllowAnonymous]`, delegates to `ICustomerService`,
    returns `201` + `Location` header + `CustomerResponse` on success.
    *Evidence:* `dotnet build` succeeds.
12. **Update `Program.cs`** (Modify #14) — register the three new services;
    add the SC-4 fallback authorization policy (Architectural Changes item
    1); confirm `System.Text.Json`
    `UnmappedMemberHandling.Disallow` is configured for unknown-field
    rejection (api-design v2 §3 / D-7). *Evidence:* `dotnet build` succeeds;
    application starts (`dotnet run`) without a hosting error, same smoke
    check performed during Stage-3 scaffolding.
13. **Full incremental validation**: `dotnet build`, then a manual
    end-to-end smoke check of the happy path and each documented error case
    (`201`, `400`×2, `409`, `415`) via `CustomerPortal.http` or `curl`,
    pending `TEST_WRITING`'s real automated tests, which supersede this
    manual check once they exist. *Evidence:* recorded request/response
    pairs for each status code in the Implementation Report.
14. **Reconcile documentation**: no `docs/` changes expected beyond the
    Implementation Report itself (impact analysis §12: no doc-update
    requirement identified). Confirm this remains true at implementation
    time; if not, record the deviation.

## Validation Strategy

- `dotnet build` after every step group (1–4, 5–6, 7–9, 10–12) — do not
  defer all validation to the end (per `aspnet-implementor`'s own
  Step 16 discipline).
- `dotnet ef migrations add` output (Step 4) is diffed against db-design v2
  §8.2 by hand before proceeding — this is the one step in this plan with a
  named upstream artifact to check against, since it's tooling-generated.
- Final full validation before the Implementation Report: `dotnet build`,
  `dotnet test` (once `TEST_WRITING` has produced tests), and the manual
  smoke pass in Step 13.

## Testing Strategy

Executable tests are `test-writer`'s output, not this plan's. Expected
levels (mirrors `impact_analysis` v2 §10):

| Level | Target | Acceptance Criteria |
|---|---|---|
| Unit | `CustomerService` (mocked/faked `ICustomerRepository`, `IPasswordHasher`) | AC-001, AC-002, AC-004 |
| Unit | `RegistrationRequestValidator` (`FluentValidation.TestHelper`) | AC-003, AC-006 |
| Integration | `WebApplicationFactory<Program>` HTTP round-trip through the real pipeline (incl. `GlobalExceptionHandler`, the SC-4 fallback policy, and the antiforgery exemption) | AC-001, AC-002, AC-003, AC-005, AC-006, AC-007 |
| Persistence | `AppDbContext` against an isolated in-memory SQLite connection, migrations applied | AC-002, AC-004; column constraints; case-insensitive duplicate collision (db-design v2 §5); UTC audit timestamps |
| Security | response never contains `password`/`password_hash`; only the new endpoint is anonymous-accessible under the new SC-4 fallback policy | AC-005; SEC-1..SEC-11 |

## Risks

Carried from `impact_analysis` v2 §13, unchanged unless noted:

- Minor: SQLite path/`Data` folder-name collision on Windows — already
  observed and cleaned up once this session; Step 4/12 smoke checks should
  catch a recurrence.
- Minor: EF Core check-constraint Fluent API syntax unverified against a
  real `dotnet ef migrations add` run — addressed directly by Step 4.
- Minor (new, this plan): the SC-4 global fallback policy (Architectural
  Changes item 1) is new-to-this-Story wiring, not yet exercised — Step 12's
  smoke check and `TEST_WRITING`'s security-level tests are the mitigation.

## New Dependencies

**None required for production code.** `TEST_WRITING` may determine that a
mocking library (e.g. `Moq` or `NSubstitute`) is needed to isolate
`ICustomerRepository`/`IPasswordHasher` in `CustomerService` unit tests
(`CustomerPortal.Tests.csproj` currently has no such package, confirmed by
impact analysis v2 §13). This plan does **not** add one — if `TEST_WRITING`
needs it, that stage proposes it and it requires explicit human approval
per `AGENTS.md`, the same as any new dependency. Flagged here for
`PLAN_REVIEW`/human visibility, not decided here.

## Configuration Changes

None. `ConnectionStrings:Default` and `Persistence:AutoMigrate` are already
correctly set in `appsettings.json`/`appsettings.Development.json` from
Stage-3 scaffolding.

## Open Questions

1. **Mocking library for `TEST_WRITING`** (see New Dependencies) — a human
   or `plan-reviewer` should weigh in before `TEST_WRITING` runs, so that
   stage doesn't have to make a new-dependency call unilaterally.
2. **SC-4 fallback policy scope** — this plan adds a global
   `RequireAuthenticatedUser()` fallback. Confirm at `PLAN_REVIEW` that this
   is the correct scope (vs., say, an explicit per-controller default) —
   it's a one-Story-early architectural completion, not itself an Open
   Decision from the Specification, so flagged for reviewer sign-off rather
   than a formal OD.

## Result

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION_PLANNING
  story: US-001
  artifact_status: DRAFT
  artifacts:
    - docs/plans/US-001-implementation-plan.md
  next_stage: PLAN_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "v2: sequences impact_analysis v2's Create/Modify list (all Creates except 3 Modifies against the scaffolded skeleton) into 14 dependency-ordered steps; no re-derivation of the file list."
    - "New architectural decision made explicit: SC-4 global deny-by-default fallback authorization policy added to Program.cs, since this Story adds the project's first endpoint and the skeleton had no fallback policy configured. Flagged for PLAN_REVIEW confirmation (Open Question 2)."
    - "Config/ namespace stays unused this Story (AD-7 threshold not crossed by 3 new registrations) -- explicit decision, not left ad hoc."
    - "Mocking-library-for-tests question flagged for human/PLAN_REVIEW visibility (Open Question 1); not resolved here, no dependency added."
    - "EF Core migration generation (Step 4) is sequenced as an explicit early step with a named diff-against-design check, not an implicit side effect."
```
