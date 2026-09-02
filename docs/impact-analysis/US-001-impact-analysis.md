---
artifact_type: impact_analysis
story: US-001
version: 2
status: ARCHIVED
created_at: 2026-08-31T10:00:04Z
updated_at: 2026-09-02T13:11:31Z
produced_by: impact-analyzer
inputs:
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
  - path: docs/decisions/US-001-open-decisions.md
    version: 1
supersedes: docs/impact-analysis/US-001-impact-analysis.md v1
semantic_analysis: TEXT_FALLBACK
---

# 1. Executive Summary

US-001 (Customer Registration) is being implemented from scratch on a new
technology stack. The prior Spring Boot/JPA/H2 implementation under `src/`
was deleted this session as part of an explicit, human-directed technology
re-platform to ASP.NET Core/EF Core/SQLite; a fresh, logic-free solution
skeleton (`CustomerPortal/`, `CustomerPortal.Tests/`) was scaffolded and
committed (`53ef257`). This analysis supersedes v1, which predicted a
Gradle/Java package surface that no longer exists.

**This is not a delta against existing US-001 code — it is a from-scratch
build against a real, verified-empty codebase.** The repository currently
contains zero Controllers, zero Services, zero Repositories, zero entities,
zero DTOs, zero validators, zero tests, and zero EF Core migrations. Every
production file this Story needs is a **Create**, not a **Modify**.

Overall risk: **Low-to-Moderate**. The change surface is small (one entity,
one endpoint, no relationships) and every architectural convention it must
follow was rewritten and verified buildable this session. The residual risk
is technical unfamiliarity risk, not requirements risk: two mechanisms named
in the approved DB design (the EF Core check-constraint Fluent API syntax,
and the SQLite connection-string/folder-name collision noted in §13) have
not yet been exercised against real tooling.

# 2. Source Artifacts

| Artifact | Path | Version | Status |
|---|---|---|---|
| Story | `docs/stories/US-001-register-customer.md` | — | current |
| Specification | `docs/specifications/US-001-spec.md` | 2 | APPROVED |
| Specification Review | `docs/reviews/specifications/US-001-spec-review.md` | 2 | APPROVED |
| API Design | `docs/designs/api/US-001-api-design.md` | 2 | DRAFT |
| OpenAPI | `docs/designs/api/US-001-openapi.yaml` | 2 | DRAFT |
| DB Design | `docs/designs/database/US-001-db-design.md` | 2 | DRAFT |
| Entity Model | `docs/designs/database/US-001-entity-model.md` | 2 | DRAFT |
| Design Review | `docs/reviews/designs/US-001-design-review.md` | 2 | APPROVED |
| Open Decisions | `docs/decisions/US-001-open-decisions.md` | 1 | DRAFT (resolutions authoritative in `history.jsonl`) |

Precondition check: `specification_review` v2 verdict `PASS`,
`HUMAN_SPEC_APPROVAL` recorded 2026-09-01T11:23:27Z. `design_review` v2
verdict `PASS`. `api_design`/`openapi`/`database_design`/`entity_model` all
exist (neither area is `NOT_APPLICABLE`). Architecture documents
(`architecture.md`, `package-map.md`, `api-conventions.md`,
`persistence-conventions.md`, `security-conventions.md`) exist, were
rewritten this session for the current stack, and contain real guidance —
confirmed by direct read, not stale/placeholder. Not `BLOCKED`.

# 3. Business Capability Impact

**Introduced:** self-service Customer registration — a new capability. No
existing capability is modified or removed (the project has no prior
capability at all in the current codebase; this Story's capability is
identical to what v1 intended, only the implementation stack differs).

# 4. Module Impact

| Module | Impact | Rationale | Confidence |
|---|---|---|---|
| `CustomerPortal` (production) | Create (content) | Every production component this Story needs is new. | HIGH |
| `CustomerPortal.Tests` | Create (content) | Currently zero test files (template placeholder was deliberately removed during scaffolding). | HIGH |

No other project/module exists in `CustomerPortal.sln`.

# 5. Namespace Impact

| Namespace | Responsibility | Impact | Rationale | Architecture constraint |
|---|---|---|---|---|
| `CustomerPortal.Controllers` | HTTP mapping | Create | `RegisterController`/`CustomersController` for `POST /api/v1/customers` | `package-map.md`: may depend only on `Services`, `Models.Dtos`, `Models.Requests` |
| `CustomerPortal.Services` | business logic, orchestration | Create | Registration orchestration, transaction boundary, entity↔DTO mapping | may depend on everything except `Controllers`; owns transaction scope |
| `CustomerPortal.Repositories` | EF Core queries | Create | `FindByEmailAsync`/`ExistsByEmailAsync`, `Add` | may depend only on `Models.Entities`, `Data` |
| `CustomerPortal.Models.Entities` | persisted domain state | Create | `Customer` entity (db-design v2 §4, entity-model v2 §2) | leaf namespace; never an API type |
| `CustomerPortal.Models.Requests` | inbound DTOs | Create | `RegistrationRequest` | validated via FluentValidation |
| `CustomerPortal.Models.Dtos` | outbound DTOs | Create | `CustomerResponse`; `ErrorResponse`/`FieldError` **already exist** (Reuse) | leaf namespace; no credential fields ever |
| `CustomerPortal.Validation` | FluentValidation rules | Create | Email-format rule, password-policy rule (Spec §6.1/§6.2) | one validator class per request type |
| `CustomerPortal.Security` | auth/hashing | Create | `IPasswordHasher`/BCrypt wrapper; cookie-auth wiring adjustments for a public+antiforgery-exempt endpoint | reads `Repositories`, `Models.Entities`, `Config` |
| `CustomerPortal.Data` | `AppDbContext`, migrations | Modify | Register `DbSet<Customer>`; **already exists** (`AppDbContext.cs`, `IAuditable.cs`) — Reuse the audit mechanism, Modify the context to add the `DbSet` | owns model config + migration history |
| `CustomerPortal.Data.Configurations` | `IEntityTypeConfiguration<T>` | Create | `CustomerConfiguration` (db-design v2 §8.1) | applied via `ApplyConfigurationsFromAssembly()` (already wired in `AppDbContext.OnModelCreating`) |
| `CustomerPortal.Exceptions` | domain exceptions + handler | Modify | Add `DuplicateEmailException`; extend `GlobalExceptionHandler`'s exception→status switch (currently a single fallback case — see §6) | single handler stays the only exception→HTTP mapping site |
| `CustomerPortal.Config` | DI wiring extensions | Reuse / possibly Create | `Program.cs` currently registers everything inline; may stay that way (AD-7 only requires extracting to `Config` "when `Program.cs` would otherwise grow past a handful of calls") | no business logic in `Config` |

# 6. Expected File Changes

## Files To Create

| Path | Responsibility | Source requirement | Confidence |
|---|---|---|---|
| `CustomerPortal/Controllers/CustomersController.cs` | `POST /api/v1/customers` action | FR-1, api-design v2 §4 | HIGH |
| `CustomerPortal/Services/CustomerService.cs` (+ `ICustomerService`) | registration orchestration, transaction boundary | FR-3..FR-9, architecture.md AD-2/AD-3 | HIGH |
| `CustomerPortal/Repositories/CustomerRepository.cs` (+ `ICustomerRepository`) | `FindByEmailAsync`, `ExistsByEmailAsync`, `AddAsync` | FR-4, db-design v2 §5 | HIGH |
| `CustomerPortal/Models/Entities/Customer.cs` | persisted entity | entity-model v2 §2.1 (exact class shape given) | HIGH |
| `CustomerPortal/Models/Requests/RegistrationRequest.cs` | inbound DTO | api-design v2 §4.1, openapi.yaml `RegistrationRequest` | HIGH |
| `CustomerPortal/Models/Dtos/CustomerResponse.cs` | outbound DTO | api-design v2 §4.2, openapi.yaml `CustomerResponse` | HIGH |
| `CustomerPortal/Validation/RegistrationRequestValidator.cs` | email format + password policy FluentValidation rules | Spec §6.1, §6.2 | HIGH |
| `CustomerPortal/Security/PasswordHasher.cs` (or similar; `IPasswordHasher` + `BCrypt.Net-Next`-backed impl) | hashing, per SC-1/SC-2 (security-conventions.md, already rewritten) | SEC-2, FR-6 | HIGH |
| `CustomerPortal/Exceptions/DuplicateEmailException.cs` | domain exception for FR-4/OD-003:A | FR-4, api-conventions.md AC-9 | HIGH |
| `CustomerPortal/Data/Configurations/CustomerConfiguration.cs` | `IEntityTypeConfiguration<Customer>` | db-design v2 §8.1 (full class body given) | HIGH |
| `CustomerPortal/Data/Migrations/<timestamp>_AddCustomer.cs` (+ `.Designer.cs`, snapshot) | EF Core migration | db-design v2 §8.2 (illustrative `Up()` given); generated by `dotnet ef migrations add`, not hand-authored | MEDIUM — exact filename/timestamp unknown until generated |
| `CustomerPortal.Tests/...` (namespace mirrors production, per `package-map.md` Test namespace rule) | unit/integration/persistence/security tests | NFR-5, test-writer's forthcoming `test_strategy`/`ac_test_matrix` | MEDIUM — exact file list is `TEST_WRITING`'s to determine |

## Files To Modify

| Path | Change | Source requirement | Confidence |
|---|---|---|---|
| `CustomerPortal/Data/AppDbContext.cs` | add `DbSet<Customer> Customers` | db-design v2 §8 | HIGH |
| `CustomerPortal/Exceptions/GlobalExceptionHandler.cs` | extend the exception-type switch (currently only a fallback `_ => 500` case, confirmed by direct read) to map `DuplicateEmailException` → 409, FluentValidation `ValidationException`/`JsonException` → 400 | AD-6, api-design v2 §7, D-7 (design_review v2) | HIGH |
| `CustomerPortal/Program.cs` | register `ICustomerService`/`ICustomerRepository`/`IPasswordHasher`/validators via DI; confirm `[Authorize]`-by-default posture with `[AllowAnonymous]` on the new public action (SC-4) | architecture.md AD-2, security-conventions.md SC-3/SC-4 | HIGH |
| `CustomerPortal/appsettings.json` | none expected — `ConnectionStrings:Default` and `Persistence:AutoMigrate` already present and correct | — | HIGH (no change) |

## Files To Reuse

| Path | Why reusable as-is |
|---|---|
| `CustomerPortal/Models/Dtos/ErrorResponse.cs`, `FieldError` (same file) | Already matches api-conventions.md AC-6 exactly — created during Stage 3 scaffolding, confirmed by direct read. |
| `CustomerPortal/Data/IAuditable.cs` | `Customer` implements it as-is; no change needed (entity-model v2 §2.1). |
| `CustomerPortal/Exceptions/GlobalExceptionHandler.cs` (the class itself, its registration in `Program.cs`) | Structure is correct per AD-6; only its internal switch needs an added case (see Modify list). |
| `CustomerPortal.csproj` package set | `Microsoft.EntityFrameworkCore.Sqlite`, `.Design`, `EFCore.NamingConventions`, `FluentValidation.AspNetCore`, `BCrypt.Net-Next` are all already referenced and sufficient — **no new NuGet dependency is required** for this Story. |

## Files Potentially Affected

| Path | Why uncertain |
|---|---|
| `CustomerPortal/Config/*` | AD-7 permits (does not require) extracting DI registration out of `Program.cs` into `Config` extension methods once it grows past "a handful of calls." Whether this Story's ~5 new registrations cross that threshold is a judgment call for `IMPLEMENTATION_PLANNING`, not fixed here. |

# 7. API Impact

New operation: `POST /api/v1/customers` (public, no existing operation
touched — purely additive, api-design v2 §1). Request: `RegistrationRequest`
(`email`, `password`, `additionalProperties: false`). Responses: `201` +
`Location` + `CustomerResponse`; `400` (FluentValidation / malformed JSON /
unknown field); `409` (duplicate email); `415` (wrong/missing
`Content-Type`); `500` (unmapped). No breaking change to any existing
contract (none exists yet). Full detail: `docs/designs/api/US-001-openapi.yaml`
v2, `docs/designs/api/US-001-api-design.md` v2.

# 8. Persistence Impact

One new entity `Customer` → table `customer` (`Id`, `Email`, `PasswordHash`,
`Role`, `Enabled`, `CreatedAt`, `UpdatedAt`), one unique index
(`uq_customer_email`), one CHECK constraint (`ck_customer_role`), no
foreign keys, no relationships. One new EF Core migration required — **the
migration must be generated via `dotnet ef migrations add`, reviewed, and
committed; it is not a substitute for the explicit persistence design already
recorded in `db-design` v2 / `entity-model` v2**, and `IMPLEMENTATION` must
not treat auto-generation as sufficient without comparing the generated
migration against db-design v2 §8.2's illustrative shape (this is exactly
db-designer's own D-10 flag, carried into planning). Full detail:
`docs/designs/database/US-001-db-design.md` v2,
`docs/designs/database/US-001-entity-model.md` v2.

# 9. Security Impact

New public, unauthenticated endpoint (the only one in the project so far);
every other route stays deny-by-default per SC-4 (there are no other routes
yet, so this is trivially satisfied — worth re-checking once US-002 exists).
Antiforgery validation exempted for this one path only (OD-002:B, SC-5).
Passwords hashed with BCrypt (`BCrypt.Net-Next`) via a new `IPasswordHasher`
in `Security`; plaintext appears only on `RegistrationRequest`, never
persisted/logged/returned. Password hash never exposed in `CustomerResponse`.
Role claim value stored and later read as the literal string `"CUSTOMER"`
(no `ROLE_` prefix, SC-2). No secret introduced. No database browser/admin UI
exposed (SC-6, unchanged — none exists in the skeleton). See design_review v2
"Security Review of Designs" for the full independent check already
performed at the design level.

# 10. Testing Impact

`CustomerPortal.Tests` currently has **zero** test files. Expected categories
(final scope is `TEST_WRITING`'s to fix, per `ac_test_matrix`):

| Category | Maps to AC | Notes |
|---|---|---|
| Unit — `CustomerService` | AC-001, AC-002, AC-004 | mock `ICustomerRepository`/`IPasswordHasher` |
| Unit — `RegistrationRequestValidator` | AC-003, AC-006 | FluentValidation `TestValidate` |
| Integration — `WebApplicationFactory<Program>` HTTP round-trip | AC-001, AC-002, AC-003, AC-005, AC-006, AC-007 | exercises the real pipeline incl. `GlobalExceptionHandler` |
| Persistence — `AppDbContext` against isolated in-memory SQLite | AC-002, AC-004 | column constraints, case-insensitive duplicate collision (db-design v2 §5), BCrypt-not-plaintext, UTC audit timestamps |
| Security posture | AC-005, SEC-1..SEC-11 | antiforgery-exemption scope, no unintended public route, response never contains password/hash |

# 11. Configuration and Dependency Impact

**No new NuGet package required** (§6, Files To Reuse). No new
`appsettings.json` key required — `ConnectionStrings:Default` and
`Persistence:AutoMigrate` are already present and correctly scoped
(base `false`, Development `true`). One new EF Core migration is a
configuration-adjacent change (schema), covered in §8. No external service
dependency introduced.

# 12. Documentation Impact

None beyond the artifacts this workflow already produces (Specification,
API/DB designs, this analysis, the forthcoming plan/tests/implementation
report). No README or operational-doc update identified as required by this
Story's scope.

# 13. Risks

| Severity | Description | Affected area | Mitigation | Human decision required |
|---|---|---|---|---|
| Minor | The configured SQLite connection string (`Data Source=./data/customer-portal.db`, relative to the process working directory) collides case-insensitively with the existing `CustomerPortal/Data/` C# source folder on Windows — confirmed empirically this session: running `dotnet run` from inside `CustomerPortal/` created the database files *inside* `Data/`, mixed with `AppDbContext.cs`/`IAuditable.cs`, instead of a separate `./data/` runtime directory. On a case-sensitive filesystem (Linux, most CI/production hosts) the same path would correctly create a sibling `data/` directory, so behavior differs by OS. | Configuration / persistence | `IMPLEMENTATION` should confirm the actual runtime working directory (normally the project root when run via `dotnet run --project CustomerPortal` or the built app, not from inside the subfolder) and verify the generated `.db*` files land in `CustomerPortal/data/`, not `CustomerPortal/Data/`, in the environment(s) that matter. Both are already `.gitignore`d either way, so this is a developer-experience/clarity risk, not a data-loss or security risk. | No — a `dotnet run` working-directory convention question, not a scope/requirement question. |
| Minor | The EF Core check-constraint Fluent API syntax specified in db-design v2 §8.1 (`ToTable(t => t.HasCheckConstraint(...))`) has not yet been exercised by an actual `dotnet ef migrations add` run against this project's exact package versions (db-design v2 D-10, carried from design_review v2). | Persistence | `IMPLEMENTATION` generates the migration early and compares it against db-design v2 §8.2 before proceeding; if the syntax doesn't produce the expected DDL, that is a design-vs-implementation deviation to record, not something to silently work around. | No — a tooling-verification step, not a scope question. |
| Minor | `CustomerPortal.Tests` currently has zero tests and zero package references beyond the xUnit/`Mvc.Testing` template defaults (confirmed by direct read of `CustomerPortal.Tests.csproj`) — no mocking library (e.g. `Moq`/`NSubstitute`) is referenced yet, and `CustomerService` unit tests will need one to isolate `ICustomerRepository`/`IPasswordHasher`. | Testing / dependencies | `TEST_WRITING` should either add a mocking library (a new dependency, needs the "new dependency" human-approval pattern noted in §11) or design tests around hand-written fakes to avoid the extra dependency — a genuine open choice for that stage, not resolved here. | Possibly — only if `TEST_WRITING` chooses to add a new NuGet package. |
| Informational | This is the project's first production code and first EF Core migration. There is no existing pattern in the repository to reuse for controller/service/repository/validator shape — every file created by this Story effectively also establishes the project's first concrete example of each layer. | Architecture / consistency | Not a defect; noted so `IMPLEMENTATION_PLANNING`/`IMPLEMENTATION` treat these first files as setting precedent that later Stories (US-002, US-003) will likely follow, and hold them to the full letter of `architecture.md`/`package-map.md` rather than "good enough for one Story." | No. |

No Critical or Major risk identified.

# 14. Open Decisions

All six Open Decisions (OD-001..OD-006) were resolved by a human at
`HUMAN_SPEC_APPROVAL`, re-confirmed 2026-09-01T11:23:27Z after the
Specification v2 correction, and are consistently reflected in both designs
(design_review v2 "Open Decisions" table). None remain open in a way that
affects this analysis or planning.

No blocking Open Decisions were identified.

# 15. Planning Inputs

- Every production file for this Story is a **Create**, except three
  **Modify**s (`AppDbContext.cs` — add `DbSet`; `GlobalExceptionHandler.cs`
  — extend the switch; `Program.cs` — register new services) against the
  already-scaffolded skeleton.
- No new NuGet package is required for production code; `TEST_WRITING` may
  need to decide on a mocking library (§13).
- The EF Core migration is generated tooling output, not hand-authored;
  `IMPLEMENTATION_PLANNING` should sequence "generate + review migration"
  as an explicit step, not assume it happens implicitly alongside entity
  creation.
- `Config/` namespace use is optional per AD-7; `IMPLEMENTATION_PLANNING`
  decides whether this Story's DI registrations warrant extracting an
  extension method or staying inline in `Program.cs`.
- The SQLite path/folder-name collision (§13) is worth a one-line
  verification step in the plan, not a design change.

# 16. Traceability

| AC | Spec §ref | Design ref | Affected area | Expected test category |
|---|---|---|---|---|
| AC-001 | §5, §6 | api-design §4, db-design §4 | Controller, Service, Repository, Entity | Integration |
| AC-002 | §5, §6.1 | db-design §5, §4.3 | Service, Repository, `uq_customer_email` | Integration, Persistence |
| AC-003 | §5, §6.1 | api-design §4.1 | Validation | Unit (validator), Integration |
| AC-004 | §5, §6.2 | db-design §7 | Service, Security (hasher) | Unit, Persistence |
| AC-005 | §5 | api-design §4.2 | Controller (response mapping) | Integration |
| AC-006 | §5, §6.2 | api-design §4.1 | Validation | Unit (validator), Integration |
| AC-007 | §5, §4 (FR-2) | api-design §3 | Controller / `System.Text.Json` config | Integration |

# 17. Analysis Limitations

- No IDE MCP server is configured for this .NET track (unlike the retired
  Spring Boot track's IntelliJ IDEA MCP integration referenced in this
  skill's own Tooling Strategy section, which is itself now stale
  boilerplate — not corrected here, out of this analysis's scope). All
  repository-state evidence in this report comes from direct file reads and
  shell listing (`find`), not semantic/symbol analysis — appropriate given
  the codebase currently contains no compiled logic to analyze
  semantically.
- The exact EF Core migration filename/timestamp cannot be predicted before
  `dotnet ef migrations add` actually runs (§6, MEDIUM confidence item).
- The final test file list is `TEST_WRITING`'s to determine; §10 states
  expected categories, not exact filenames.
- Whether `Config/` extension methods are warranted (AD-7's threshold is
  qualitative) is left to `IMPLEMENTATION_PLANNING`'s judgment, not fixed
  here.

# 18. Readiness Result

**PASS.** The change surface is identified with high confidence for
production code (every file is a clean Create against a verified-empty
codebase) and medium confidence for the exact migration/test file list
(inherent to those being generated/designed by later stages). No Critical
or Major risk. Three Minor risks and one Informational note are carried to
`IMPLEMENTATION_PLANNING` as residual, non-blocking findings. Ready for
`IMPLEMENTATION_PLANNING`.

```yaml
result:
  verdict: PASS
  stage: IMPACT_ANALYSIS
  story: US-001
  artifact_status: DRAFT
  artifacts:
    - docs/impact-analysis/US-001-impact-analysis.md
  next_stage: IMPLEMENTATION_PLANNING
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "v2: from-scratch analysis against a verified-empty ASP.NET Core skeleton (confirmed via direct `find` of CustomerPortal/), not a delta -- every production file is a Create except 3 Modifies against already-scaffolded files (AppDbContext, GlobalExceptionHandler, Program.cs)."
    - "No new NuGet dependency required for production code; TEST_WRITING may need a mocking library for CustomerService unit tests -- a new-dependency decision for that stage."
    - "Minor: SQLite connection string './data/customer-portal.db' collides case-insensitively with the existing Data/ C# folder on Windows when run from inside CustomerPortal/ -- confirmed empirically this session (files landed in Data/, cleaned up). IMPLEMENTATION should verify the actual runtime working directory keeps generated db files out of the source folder. Not a data-loss or security risk; both names are already gitignored."
    - "Minor (carried from db-design v2 D-10): the EF Core check-constraint Fluent API syntax hasn't been exercised by a real migrations-add run; IMPLEMENTATION should generate it early and diff against db-design v2 section 8.2."
    - "Informational: this Story's files are the project's first production code of every kind (first controller, service, repository, entity, migration) -- they set the pattern later Stories will likely copy."
```
