---
artifact_type: design_review
story: US-001
version: 2
status: ARCHIVED
created_at: 2026-08-31T09:44:50Z
updated_at: 2026-09-02T13:11:31Z
produced_by: design-reviewer
inputs:
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
  - path: docs/decisions/US-001-open-decisions.md
    version: 1
  - path: docs/architecture/architecture.md
    version: null
  - path: docs/architecture/api-conventions.md
    version: null
  - path: docs/architecture/persistence-conventions.md
    version: null
  - path: docs/architecture/security-conventions.md
    version: null
  - path: docs/architecture/package-map.md
    version: null
  - path: docs/product/business-rules.md
    version: null
  - path: docs/product/business-glossary.md
    version: null
  - path: docs/product/non-functional-requirements.md
    version: null
supersedes: docs/reviews/designs/US-001-design-review.md v1
---

# Design Review — US-001 Customer Registration (v2)

## Summary

**Verdict: PASS.** Re-review of API design v2 and database design v2, both
produced this session as a full re-run for ASP.NET Core/EF Core/SQLite
(loop-back from `RECONCILIATION` v2, `specification_gap`), superseding the
v1 designs authored for the retired Spring Boot/JPA/H2 stack. Both designs
are complete, internally consistent, cross-consistent with each other, and
compliant with the approved Specification v2 and the rewritten architecture/
security/persistence conventions. The six Open Decisions (OD-001:A,
OD-002:B, OD-003:A, OD-004:A, OD-005:A, OD-006:A) are correctly and
consistently applied across both designs, unchanged from the original
resolution — none of them are stack-sensitive.

No `Critical` and no `Major` findings. Ten `Minor`/`Informational` findings
are recorded: five carried unchanged from v1, two revised for the new stack,
one resolved (no longer applicable), and two new to this review. None block
progression to `IMPACT_ANALYSIS`.

## Reviewed Artifacts

| Artifact | Path | Version | Status |
|---|---|---|---|
| Specification | `docs/specifications/US-001-spec.md` | 2 | APPROVED |
| Specification Review | `docs/reviews/specifications/US-001-spec-review.md` | 2 | APPROVED |
| API Design | `docs/designs/api/US-001-api-design.md` | 2 | DRAFT |
| OpenAPI Contract | `docs/designs/api/US-001-openapi.yaml` | 2 | DRAFT |
| DB Design | `docs/designs/database/US-001-db-design.md` | 2 | DRAFT |
| Entity Model | `docs/designs/database/US-001-entity-model.md` | 2 | DRAFT |
| Open Decisions | `docs/decisions/US-001-open-decisions.md` | 1 | DRAFT |

Precondition check: `specification_review` v2 verdict is `PASS`;
`HUMAN_SPEC_APPROVAL` was recorded 2026-09-01T11:23:27Z (`history.jsonl`,
re-confirming the same OD resolutions as the original 2026-08-31T07:48:48Z
approval). Both design areas are applicable — the Specification changes
both public API behavior and persistence behavior, and neither `API_DESIGN`
nor `DB_DESIGN` recorded `NOT_APPLICABLE` (both returned `PASS`). All
design artifacts consumed the current spec v2, spec-review v2, and
open_decisions v1; none is `SUPERSEDED`. Architecture convention documents
(rewritten this session for the new stack) contain real guidance. Not
stale, not `BLOCKED`.

## API Design Review

| Check | Result |
|---|---|
| Every externally observable AC maps to an operation / status code | Pass — AC-001→`201`+`Location`+`CustomerResponse`; AC-002→`409`; AC-003→`400`+`fieldErrors[email]`; AC-005→`CustomerResponse` schema (no credential fields); AC-006→`400`+`fieldErrors[password]`; AC-007→`415`. AC-004 is a persistence guarantee, supported negatively by the response schema. |
| Path, method, media type, versioning, error model vs `api-conventions.md` | Pass — `POST /customers` under `servers: /api/v1` (AC-1); `application/json` required, `415` otherwise (AC-2); plural noun, `camelCase` fields (AC-3); `POST /collection` → `201` + `Location` + body (AC-4); `400/409/415/500` (AC-5); `ErrorResponse` matches the AC-6 shape with optional `fieldErrors[]`; single `GlobalExceptionHandler` obligation stated, citing `architecture.md` AD-6 (AC-9). |
| Request/response schemas use DTOs, never entities | Pass — `RegistrationRequest`, `CustomerResponse`, `ErrorResponse`, `FieldError`. Consistent with AD-4 / `package-map.md` (`Models.Requests`, `Models.Dtos`). |
| No response field exposes a credential or internal-only value | Pass — `CustomerResponse` = `id, email, role, createdAt` (OD-004:A). `password` is `writeOnly`; no `password_hash`, no `enabled`, no `updatedAt`. |
| Specification validation constraints reflected in the contract | Pass with note D-1 — `email`: `minLength 1`, `maxLength 254`, `format: email` (§6.1, OD-001:A), now correctly attributed to a FluentValidation rule rather than Jakarta `@Email`. `password`: `minLength 12`, `maxLength 72` (§6.2). The four character-class rules are stated in the schema `description` only, not as a `pattern` (see D-1, unchanged). |
| Error responses cover the documented failures | Pass — `400` (FluentValidation failure, malformed JSON, unknown field — correctly no longer called "bean-validation"), `409` (duplicate email, OD-003:A message), `415` (media type), `500` (unmapped, no leak). `401`/`403` correctly marked not applicable (public endpoint). |
| Authentication / authorization per operation stated, matches `security-conventions.md` | Pass — `security: []`, `x-public: true`, `x-csrf-exempt: true`, `x-authorization: none`. Registration is the one public endpoint (SC-4); all other routes stay deny-by-default; no other route added. |
| CSRF/antiforgery exemption recorded as an architecture decision | Pass — API design §6 is the recorded decision required by SC-5 for OD-002:B, now correctly described as ASP.NET Core antiforgery-token validation rather than a Spring Security CSRF filter; CSRF/antiforgery stays enabled for every other endpoint. |
| Backward compatibility | Pass — purely additive; no existing contract changes (AC-1). |
| Request-shape policy (spec-review F-2 / Spec §6.3) | Pass, revised (D-7) — `additionalProperties: false`; unknown/extra fields → `400` (API design §3), now correctly attributed to `System.Text.Json` `UnmappedMemberHandling.Disallow` rather than Jackson. Spec §6.3 explicitly delegated this to `API_DESIGN`; recording it here is in scope. |

## Database Design Review

| Check | Result |
|---|---|
| Entities trace to business concepts | Pass — single `Customer` entity / `customer` table embodies glossary **Customer** (identity: `Email`) and **Account** (credentials: `PasswordHash`, `Role`, `Enabled`). Reconciliation in DB design §3, carried unchanged from v1. See D-2. |
| Explicit column length, nullability, uniqueness, indexes — no EF Core convention defaults | Pass — every column declares its CLR type, SQLite affinity, nullability, and constraints via `IEntityTypeConfiguration<Customer>`: `Email` (`TEXT`, `HasMaxLength(254)`, required), `PasswordHash` (`TEXT`, `HasMaxLength(60)`, required — see D-11, revised from v1's `VARCHAR(60)`), `Role` (`TEXT`, `HasMaxLength(20)`, required), `Enabled` (`INTEGER`/`bool`, required), `CreatedAt`/`UpdatedAt` (`TEXT`/`DateTimeOffset`, required). `uq_customer_email` unique index. Matches PC-4 / NFR-4. |
| Identifier type and generation vs `persistence-conventions.md` | Pass — `Id` (`long`), EF Core default value generation for the sole integer key (`pk_customer`, PC-3). Surrogate key; `Email` is a unique index, not a natural PK. |
| Sensitive fields identified with storage rules | Pass — plaintext password never persisted (no column); `PasswordHash` BCrypt-only (`BCrypt.Net-Next`), never logged/returned, no `ToString()` override needed since `Customer` isn't a `record` (db-design §7, entity-model §2.1 — see D-12). |
| Schema-initialization strategy vs `persistence-conventions.md` | Pass, revised (D-5 retired, see below) — a committed, reviewed EF Core Migration (`Data/Migrations/`) generated from `CustomerConfiguration`; `Database.EnsureCreated()`/`EnsureDeleted()` forbidden outside the isolated test database; `Persistence:AutoMigrate` gates `Database.Migrate()` at local/dev startup. Matches the rewritten PC-1, PC-2, SC-8. No shortcut used in place of an explicit, reviewed migration. |
| Relationships and cardinality explicit | Pass — none for US-001; `customer` is standalone. `Role` modeled as a `string` column + CHECK constraint, not a C# enum with a converter and not a `customer_role` table (see D-3, revised and strengthened for this stack). |
| Indexes | Pass — `uq_customer_email` provides the `Email` lookup index (PC-7); no separate `ix_customer_email` (correct — a unique index already covers lookup). No foreign keys, so no FK indexes. |
| Audit timestamps | Pass — `CreatedAt` (never reassigned after insert) / `UpdatedAt`, `DateTimeOffset`, UTC, set via `AppDbContext.SaveChangesAsync`'s `IAuditable` override (PC-6, BR-007) — a mechanism that already exists in the scaffolded skeleton (`CustomerPortal/Data/AppDbContext.cs`), confirmed by direct read. This replaces v1's `@CreatedDate`/`@LastModifiedDate` JPA auditing; the old D-5 strict-type-match risk (`OffsetDateTime` ↔ `TIMESTAMP WITH TIME ZONE` under Hibernate `ddl-auto=validate`) no longer applies — see D-5 below. |

## Cross-Model Consistency

| Concern | Result |
|---|---|
| Every API resource maps to a coherent persistence model | Pass — the `customers` resource ↔ `customer` table; the `registerCustomer` create operation ↔ a single `INSERT`. |
| Field names / types / constraints agree between DTO schemas and entities | Pass — `RegistrationRequest.email` `maxLength 254` ↔ `Email` `HasMaxLength(254)`; `CustomerResponse.id` `int64` ↔ `Id` `long`; `CustomerResponse.email` ↔ stored normalized `Email`; `CustomerResponse.role` enum `[CUSTOMER]` ↔ `Role` `string` column (contract narrows to the only value US-001 emits; the persisted value set stays `{CUSTOMER, ADMIN}` via the CHECK — not a conflict); `CustomerResponse.createdAt` `date-time` ↔ `CreatedAt` `DateTimeOffset`. |
| `password` handling agrees end to end | Pass — `password` `writeOnly` on the inbound DTO only; hashed to `PasswordHash` (`TEXT`, `HasMaxLength(60)` — BCrypt output is exactly 60 chars, D-11); absent from every response schema and from the entity model's `Customer → CustomerResponse` map. `password_hash` never appears anywhere in the OpenAPI contract, which is correct (SEC-4) and required no cross-check beyond confirming its absence. The `maxLength: 72` characters vs 72 bytes point is carried as D-6. |
| Uniqueness / validation enforced consistently | Pass — email format/length at the request layer (FluentValidation, correctly no longer Jakarta/Bean Validation), email **uniqueness** as a business rule in the Service (`ExistsByEmailAsync` on the lowercased value) **and** as `uq_customer_email` at the DB. Case-insensitivity (BR-002) is met by service lowercasing before both the check and the insert, with a plain unique index (OD-006:A), stated identically in both designs. Matches AD-5. |
| No design introduces a business decision absent from the Specification or an approved decision | Pass — the three DB choices (entity name, `Role` modeling, CHECK/DEFAULT clauses) are within `db-designer`'s entity/schema authority and are explicitly referred here for confirmation (D-2, D-3, D-4). The unknown-field rejection (D-7) is the design-stage choice Spec §6.3 delegated to `API_DESIGN`. Nothing else new. |

## Security Review of Designs

| Area | Result |
|---|---|
| Authentication posture | Pass — one new public endpoint; everything else deny-by-default (SC-4). |
| CSRF / antiforgery | Pass — exemption for `POST /api/v1/customers` only, recorded as the Story architecture decision in API design §6 (SC-5, OD-002:B). |
| Password hashing & exposure | Pass — BCrypt via `BCrypt.Net-Next` (`Security` namespace, per SC-1 and SC-2's role-claim clarification); plaintext only on the inbound DTO; dual-layer policy enforcement (FluentValidation request rule + service re-check); hash stored only in `PasswordHash`, never returned (SC-1, PC-9, SEC-2..SEC-4). |
| Error / log hygiene | Pass — single error shape (AC-6); `message` client-safe; examples carry no stack trace, SQL, class/namespace name, path, or DB connection string; `fieldErrors[].message` never echoes the submitted value (SC-9, SEC-6). |
| Account enumeration | Accepted — the explicit `409` duplicate-email response is a human-approved exposure (OD-003:A / SEC-8), not a default. Documented in API design §7. |
| Database browser/admin UI / secrets / schema safety | Pass — designs do not register or expose any database browser/admin UI in any profile (SC-6, correctly no longer phrased as `spring.h2.console.enabled=false`), introduce no secrets (SC-7/SEC-11), and keep schema changes to committed EF Core Migrations only, no `EnsureCreated`/`EnsureDeleted` shortcut (SC-8/SEC-10). |
| Role claim consistency (new v2 check) | Pass — `security-conventions.md` SC-2 fixes the role claim value at exactly `CUSTOMER`/`ADMIN` with **no** `ROLE_` prefix. `db-design` v2 §6 models `Role` as a plain `string` column storing that literal value, explicitly to avoid a C# enum-member-name-to-claim-value translation step that could silently drift from SC-2 (e.g. a naive `enum Role { Customer, Admin }` would default-`ToString()` to `"Customer"`, not `"CUSTOMER"`, unless a converter is added and kept correct). This is a sound, deliberate design choice — see D-3. |

## Findings

| id | Severity | Area | Evidence | Required correction |
|---|---|---|---|---|
| D-1 | Minor (carried) | API | `RegistrationRequest.password` states the four character-class rules only in the schema `description`; the contract has no `pattern`. OpenAPI 3.0.3 cannot cleanly express "≥1 of each class". | None required. The authoritative enforcement is the FluentValidation custom rule plus the service re-check (FR-6, SC-1). `TEST_WRITING` must derive password-policy cases from Spec §6.2, not from the OpenAPI schema. |
| D-2 | Minor (confirmation, carried) | Database | DB design §3 / entity-model §1: single entity `Customer` / table `customer` covers glossary **Customer** + **Account**. | **Confirmed acceptable, unchanged from v1.** US-001 persists no profile data; merging identity and credentials into one `Customer` matches Spec NFR-4, `persistence-conventions.md` PC-5 (`customer` example table), and the API naming. A future Story may split credential vs profile data; nothing here forecloses that. |
| D-3 | Minor (confirmation, revised & strengthened) | Database | DB design §6 / entity-model §2.2: `Role` is a `string` column with `ck_customer_role` CHECK, not a `customer_role` lookup table and (new for v2) not a C# enum + value converter. | **Confirmed acceptable — and the stronger choice.** PC-5 cites `customer_role` only as a naming example, not a mandated table. Storing the literal `string` value (rather than an enum requiring a converter) is the design that best guarantees the persisted value matches the SC-2 role-claim string with zero translation risk (see Security Review of Designs, new row). No correction needed. |
| D-4 | Minor (confirmation, carried) | Database | DB design §4.3 / §10: `ck_customer_role CHECK (role IN ('CUSTOMER','ADMIN'))` and the `Role`/`Enabled` column `DEFAULT` values are defensive. | **Confirmed acceptable to keep**, unchanged rationale from v1 — harmless hardening for a manual write path; the entity/service always sets both fields explicitly. Equally no objection if `IMPLEMENTATION` prefers minimal DDL. |
| D-5 | **Resolved / retired for this stack** | Database | v1's D-5 flagged `OffsetDateTime` ↔ `TIMESTAMP WITH TIME ZONE` under Hibernate `ddl-auto=validate`, and H2 2.x `GENERATED BY DEFAULT AS IDENTITY` clause-ordering, as known strict-match trip points. | No longer applicable: EF Core Migrations *generate* the schema from the model (db-design §8.1/§8.2) rather than validating the model against a separately hand-written `schema.sql` — there is no second, independently-authored DDL source to drift out of sync with the entity. This entire risk category is removed by the stack change, not just renamed. |
| D-6 | Minor (carried) | API / cross-model | `password` `maxLength: 72` in the contract is characters; the real bound is 72 **bytes** of BCrypt input (spec-review F-5, API Q-4, DB design §4.2). | No design change. `TEST_WRITING` / `IMPLEMENTATION` own the 72-byte boundary vector and enforce it in the FluentValidation rule + service, not the column. |
| D-7 | Minor (revised) | API | API design §3 / OpenAPI `additionalProperties: false`: unknown JSON fields → `400`. Implementation note (revised from v1's Jackson reference): `System.Text.Json` `JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow`, resulting `JsonException` mapped to `400` by `GlobalExceptionHandler`. | In scope (Spec §6.3 delegated the choice to `API_DESIGN`) and acceptable. `IMPLEMENTATION` must set the option and confirm the `400` mapping; `IMPLEMENTATION_VERIFICATION` confirms the path. |
| D-8 | Minor (informational, carried) | API | `Location: /api/v1/customers/{id}` targets `GET /api/v1/customers/{id}`, which US-001 does not implement. | Acceptable. The header value is correct and stable; the read endpoint is a future Story. AC-4 requires the header, not a live target within this Story. |
| D-9 | **Resolved for this stack** | Database (downstream note) | v1's D-9 flagged entity-model §6 mentioning a non-existent "customer" package instead of the `package-map.md` layout. | Resolved in v2: entity-model §6 now correctly names the current `package-map.md` namespaces (`Models/Entities`, `Data/Configurations`, `Repositories`, `Services`, `Controllers`, `Models/Requests`, `Models/Dtos`, `Validation`, `Security`) — confirmed by direct read of entity-model v2 §6. No further action. |
| D-10 | Minor (new, risk note per db-designer's own flag) | Database | DB design v2 §8.1: the EF Core check-constraint Fluent API syntax (`ToTable(t => t.HasCheckConstraint(...))`) is designed against documented EF Core 8 behavior but has not yet been exercised by an actual `dotnet ef migrations add` run against this project's exact package versions. | Not a design defect — the syntax is correct EF Core 8 API usage as documented. `IMPLEMENTATION` must confirm the generated migration actually matches DB design §8.2's illustrative snippet (e.g. `Sqlite:Autoincrement` annotation, check-constraint DDL) before relying on it; `IMPLEMENTATION_VERIFICATION` should treat a mismatch as a design-vs-implementation deviation to record, not silently "fix." |
| D-11 | Minor (new, revision confirmation) | Database | `password_hash`/`PasswordHash` column revised from v1's `VARCHAR(60)` to `TEXT`, `HasMaxLength(60)`, matching the rewritten `persistence-conventions.md` PC-9. | **Confirmed consistent.** DB design v2 §4.2 and entity-model v2 §2 agree exactly; the OpenAPI contract correctly never exposes this field at all (SEC-4), so there was nothing in the API design to reconcile it against — its absence there is itself the correct state, not a gap. |

No `Critical` findings. No `Major` findings.

## Open Decisions

All six are resolved (human, originally 2026-08-31T07:48:48Z, re-confirmed
2026-09-01T11:23:27Z after the Specification v2 correction) and applied
consistently in both designs. The `open_decisions.md` file still shows
entries as `OPEN`; the recorded resolutions are authoritative (both designs
note this explicitly).

| OD | Resolution | API design | DB design | Consistent |
|---|---|---|---|---|
| OD-001 | A — standard email shape + max length 254 | `email` `format: email`, `maxLength 254`, `minLength 1` | `Email` `HasMaxLength(254)` | Yes |
| OD-002 | B — antiforgery-exempt registration path | `security: []`, `x-csrf-exempt`, decision recorded §6 | no persistence impact | Yes |
| OD-003 | A — explicit `409` | `409` + `"An account with this email already exists."` | `uq_customer_email`; duplicate detected before insert | Yes |
| OD-004 | A — `id, email, role, createdAt` | `CustomerResponse` field list | `Enabled` / `UpdatedAt` persisted, never exposed | Yes |
| OD-005 | A — anti-abuse out of scope | no rate-limit headers, no `429` | no persistence impact | Yes |
| OD-006 | A — lowercase in service + plain unique index | no contract impact; response `email` is the normalized value | `Email` stored lowercased; plain unique index, no functional/`LOWER()` index | Yes |

No unresolved Open Decision affects API or persistence design.

## Limitations

- Review is document-level: it does not execute `dotnet ef migrations add`
  or apply a migration to a live SQLite database. The generated-migration
  match to DB design §8.2 (D-10) is verified at `IMPLEMENTATION` /
  `IMPLEMENTATION_VERIFICATION`.
- OpenAPI was reviewed as a document; it was not run through a schema
  linter.
- Package/namespace placement (previously D-9, now resolved) and the
  `System.Text.Json` unknown-member wiring (D-7) are
  planning/implementation concerns flagged here, not resolved here.
- No IDE MCP server is configured for this .NET track; review evidence
  comes from direct file reads and `Grep`, not semantic/symbol analysis —
  adequate for a document-level design review with no compiled code yet to
  analyze semantically.

## Verdict

**PASS** — both designs are sound, mutually consistent, and compliant with
the approved Specification v2 and the rewritten architecture/security
conventions. The ten `Minor`/`Informational`/`Resolved` findings are
advisory and carried as `non_blocking_findings`; D-3 confirms (and
strengthens the case for) the DB design's `Role`-as-`string` choice, and
D-5/D-9 record that two v1-era risks are structurally retired by the stack
change rather than merely renamed. The Story may proceed to
`IMPACT_ANALYSIS`.

```yaml
result:
  verdict: PASS
  stage: DESIGN_REVIEW
  story: US-001
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/designs/US-001-design-review.md
  next_stage: IMPACT_ANALYSIS
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "D-1: password character-class policy is only in the OpenAPI description, not a pattern; TEST_WRITING must source password cases from Spec §6.2, not the schema."
    - "D-2 (confirmed, carried): single Customer entity/table covers glossary Customer + Account for US-001 (no profile data)."
    - "D-3 (confirmed, revised): role modeled as a string column + CHECK constraint, not an enum+converter and not a customer_role table -- deliberately avoids an enum-to-SC-2-claim-value translation risk."
    - "D-4 (confirmed, carried): ck_customer_role CHECK and role/enabled column defaults are defensive-only; acceptable to keep."
    - "D-5 (resolved/retired): the old Hibernate ddl-auto=validate strict-type-match risk no longer exists -- EF Core Migrations generate the schema from the model, removing the separate-DDL-source drift category entirely."
    - "D-6: password maxLength 72 in the contract is characters; the real limit is 72 bytes (BCrypt input). TEST_WRITING/IMPLEMENTATION own the byte-boundary vector."
    - "D-7 (revised): unknown-JSON-field rejection now correctly attributed to System.Text.Json UnmappedMemberHandling.Disallow, not Jackson; IMPLEMENTATION must wire the option + confirm the 400 mapping."
    - "D-8: Location header targets GET /api/v1/customers/{id}, not implemented by US-001; header value is stable and correct."
    - "D-9 (resolved): entity-model v2 section 6 already names the current package-map.md namespaces; v1's stale 'customer package' note is gone."
    - "D-10 (new): the EF Core check-constraint Fluent API syntax (db-design section 8.1) hasn't been exercised by an actual migrations-add run; IMPLEMENTATION must confirm the generated migration matches section 8.2."
    - "D-11 (new): password_hash/PasswordHash revised from v1's VARCHAR(60) to TEXT/HasMaxLength(60), matching the rewritten PC-9; confirmed consistent, and correctly absent from the OpenAPI contract entirely."
```
