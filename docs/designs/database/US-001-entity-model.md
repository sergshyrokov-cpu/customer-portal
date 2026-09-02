---
artifact_type: entity_model
story: US-001
version: 2
status: ARCHIVED
created_at: 2026-08-31T09:16:56Z
updated_at: 2026-09-02T13:11:31Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-001-spec.md
    version: 2
  - path: docs/designs/api/US-001-api-design.md
    version: 2
  - path: docs/designs/api/US-001-openapi.yaml
    version: 2
  - path: docs/decisions/US-001-open-decisions.md
    version: 1
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
supersedes: docs/designs/database/US-001-entity-model.md v1
---

# Entity Model — US-001 Customer Registration (v2)

Companion to `docs/designs/database/US-001-db-design.md` (physical schema). This
document is the entity/attribute model and its mapping to business concepts and
to the API DTOs.

> **v2 revision note:** full rework for EF Core (C# entity class + Fluent API
> configuration) in place of JPA annotations. See db-design v2's revision
> note for the stack-migration context.

## 1. Entities

One entity: **`Customer`** (`CustomerPortal.Models.Entities.Customer`) → table
`customer`. See db-design §3 for the `Customer` vs glossary `Account` naming
reconciliation.

No other entities, no owned types, no collection navigation. `Role` is a
plain `string` column (see db-design §6 for why it is not a C# enum), not a
separate entity.

## 2. `Customer`

| Field | CLR type | Column | Explicit mapping (`IEntityTypeConfiguration<Customer>`) | Business concept |
|---|---|---|---|---|
| `Id` | `long` | `id` | `.HasKey(c => c.Id).HasName("pk_customer")` — EF Core default value generation | Account identifier (surrogate, PC-3) |
| `Email` | `string` | `email` | `.Property(c => c.Email).HasMaxLength(254).IsRequired()`; uniqueness declared once, via `.HasIndex(...)` (§2.1) per PC-4 (either/or, not both) | Customer email (`business-glossary.md` Customer; BR-001, BR-002) |
| `PasswordHash` | `string` | `password_hash` | `.Property(c => c.PasswordHash).HasMaxLength(60).IsRequired()` | Hashed access credential (`business-glossary.md` Account; BR-005, PC-9) |
| `Role` | `string` | `role` | `.Property(c => c.Role).HasMaxLength(20).IsRequired().HasDefaultValue("CUSTOMER")` | Role / permission group (`business-glossary.md` Role; BR-006, SC-2) |
| `Enabled` | `bool` | `enabled` | `.Property(c => c.Enabled).IsRequired().HasDefaultValue(true)` | Account enabled state (BR-004) |
| `CreatedAt` | `DateTimeOffset` | `created_at` | `.Property(c => c.CreatedAt).IsRequired()`; set by `AppDbContext.SaveChangesAsync`'s `IAuditable` handling, never reassigned after insert | Audit — creation instant, UTC (BR-007, PC-6) |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` | `.Property(c => c.UpdatedAt).IsRequired()`; set by the same override on every save | Audit — last-modified instant, UTC (BR-007, PC-6) |

`Email` uniqueness is declared **once**, via `.HasIndex(c => c.Email)
.IsUnique()` in §2.1 (database name `uq_customer_email`, matching
db-design §4.3), not duplicated as a property-level constraint — PC-4 is
either/or.

### 2.1 Class shape and configuration

```csharp
namespace CustomerPortal.Models.Entities;

public class Customer : Data.IAuditable
{
    public long Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string Role { get; set; } = "CUSTOMER";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

- Implements `IAuditable` (`CustomerPortal/Data/IAuditable.cs`, already
  scaffolded) so `AppDbContext.SaveChangesAsync` sets `CreatedAt`/`UpdatedAt`
  automatically — no per-entity audit code needed (PC-6).
- Table/key/index/check-constraint mapping lives in the separate
  `CustomerConfiguration : IEntityTypeConfiguration<Customer>` class under
  `Data/Configurations/` (db-design §8.1), not as attributes on this class —
  per `architecture.md` AD-7/`package-map.md`, `Models.Entities` stays a leaf
  with no framework-configuration concerns attached.
- `EFCore.NamingConventions`'s snake_case convention (already wired in
  `Program.cs`) maps `Email`→`email`, `PasswordHash`→`password_hash`,
  `CreatedAt`→`created_at`, `UpdatedAt`→`updated_at` without restating column
  names in the configuration class (none of them diverge from the
  convention).
- No `RowVersion` / concurrency token (not required by US-001).
- Equality: rely on EF Core's default entity equality (reference equality);
  no custom `Equals`/`GetHashCode` override needed for US-001's single
  create-only flow.
- No custom `ToString()` override is added — the default `object.ToString()`
  (type name only) does not expose `PasswordHash`, so no explicit exclusion
  is needed (contrast with `RegistrationRequest`, a `record`, whose
  compiler-generated `ToString()` **does** render all properties including
  the plaintext password — see security-conventions.md SC-1 and
  `aspnet-implementor`'s implementation obligation to guard against logging
  it).

### 2.2 `Role` values

Stored as the literal claim string, not a C# enum (db-design §6):

- Valid values: `"CUSTOMER"`, `"ADMIN"` (`ck_customer_role` CHECK,
  db-design §4.3).
- US-001 only ever assigns `"CUSTOMER"` (SC-2, BR-006). `"ADMIN"` exists in
  the value set for completeness (`business-glossary.md` Role) but is unused
  by this Story.
- The ASP.NET Core role claim (`ClaimTypes.Role` = `"CUSTOMER"` /
  `"ADMIN"`, **no** `ROLE_` prefix — SC-2) is derived from this column at
  authentication time (US-002); the column already holds the exact claim
  value, so no translation step exists to get wrong.

## 3. Lifecycle / invariants

| Invariant | Enforced by |
|---|---|
| `Email` is unique case-insensitively | Service lowercases before check + insert (OD-006:A); `uq_customer_email` (db-design §5) |
| `Email` is stored lowercase | Service normalization (OD-006:A) |
| `PasswordHash` is always a BCrypt hash, never plaintext | Service hashes with the `BCrypt.Net-Next`-backed hasher (`Security` namespace) before constructing the entity (SC-1, FR-5) |
| `Role` is non-null, defaults to `"CUSTOMER"` | Service sets `"CUSTOMER"` explicitly on creation (BR-006); CLR default also `"CUSTOMER"` (§2.1) |
| `Enabled` is `true` on creation | Service sets `true` explicitly (FR-5, SEC-5); CLR default also `true` (§2.1) |
| `CreatedAt` never changes after insert | `AppDbContext.SaveChangesAsync`'s `IAuditable` override only sets it on `EntityState.Added` (PC-6) |
| timestamps are UTC | `IAuditable` override uses `DateTimeOffset.UtcNow` (BR-007, PC-6) |

US-001 performs only **create**. No update or delete path exists in this Story.

## 4. Mapping to API DTOs

DTOs are defined by `docs/designs/api/US-001-openapi.yaml`. The entity is
never serialized directly (`Models.Entities` never appears in a Controller
signature — `architecture.md` AD-4).

### 4.1 `RegistrationRequest` → `Customer` (inbound, create)

| DTO field | Entity field | Transformation |
|---|---|---|
| `email` | `Email` | `.Trim().ToLowerInvariant()` (OD-006:A) |
| `password` | `PasswordHash` | validated (12–72 bytes, char-class policy via FluentValidation), then the BCrypt hasher's `HashPassword(password)` (SC-1, FR-6). Plaintext discarded; never stored. |
| *(none)* | `Role` | set to `"CUSTOMER"` (BR-006) |
| *(none)* | `Enabled` | set to `true` (SEC-5) |
| *(none)* | `CreatedAt` / `UpdatedAt` | set by the `AppDbContext.SaveChangesAsync` audit override (PC-6) |
| *(none)* | `Id` | assigned by SQLite on insert (PC-3) |

Unknown JSON fields on `RegistrationRequest` are rejected with `400` at the
deserialization layer (`System.Text.Json`
`UnmappedMemberHandling.Disallow`, api-design §3) before this mapping runs.

### 4.2 `Customer` → `CustomerResponse` (outbound, `201`)

| Entity field | DTO field | Notes |
|---|---|---|
| `Id` | `id` | `long` → `int64` |
| `Email` | `email` | normalized (lowercase) value as stored |
| `Role` | `role` | literal string, always `"CUSTOMER"` |
| `CreatedAt` | `createdAt` | ISO-8601 UTC |
| `PasswordHash` | — | **never mapped** (SC-1, SEC-4, OD-004:A) |
| `Enabled` | — | not exposed (OD-004:A) |
| `UpdatedAt` | — | not exposed (OD-004:A) |

`Location: /api/v1/customers/{id}` header is built from `Id` (FR-8); the
target endpoint is not implemented by US-001.

## 5. Concept traceability

| Business concept (`business-glossary.md` / BR) | Model element |
|---|---|
| Customer (person who owns an account) | `Customer` entity (identity attributes: `Email`) |
| Account (technical representation of access credentials) | `Customer` entity (credential attributes: `PasswordHash`, `Role`, `Enabled`) — merged for US-001, see db-design §3 |
| Registration (process of creating a customer account) | `Customer` insert via `POST /api/v1/customers` (FR-1, FR-5) |
| Role (permission group: CUSTOMER, ADMIN) | `Role` string column, `ck_customer_role` |
| BR-001 unique email | `Email` unique index / `uq_customer_email` |
| BR-002 case-insensitive email | lowercase normalization + plain unique index (OD-006:A) |
| BR-003 one account per customer | `uq_customer_email` (email identifies the customer) |
| BR-004 disabled account cannot authenticate | `Enabled` column (consumed by US-002) |
| BR-005 no plaintext passwords | only `PasswordHash` exists; no plaintext field |
| BR-006 default role CUSTOMER | service sets `Role = "CUSTOMER"` |
| BR-007 UTC timestamps | `CreatedAt`/`UpdatedAt` `DateTimeOffset`, `AppDbContext` audit override |

## 6. Notes for downstream stages

- **IMPACT_ANALYSIS / IMPLEMENTATION_PLANNING:** new `Customer` entity
  (`Models/Entities/`), `CustomerConfiguration`
  (`Data/Configurations/`), repository (`Repositories/`), service
  (`Services/`), controller (`Controllers/`), request/response DTOs
  (`Models/Requests/`, `Models/Dtos/`), FluentValidation validator
  (`Validation/`); `Security` namespace additions (BCrypt hasher wrapper,
  cookie-auth wiring making `POST /api/v1/customers` public +
  antiforgery-exempt); a new EF Core migration under
  `Data/Migrations/`.
- **TEST_WRITING:** persistence tests should assert column constraints
  (`Email` max length 254, `PasswordHash` max length 60, both `NOT NULL`),
  the case-insensitive duplicate collision, that `PasswordHash` is a BCrypt
  string and not the plaintext, and that `CreatedAt`/`UpdatedAt` are
  populated and in UTC.
- **DESIGN_REVIEW:** see db-design §10 for the open confirmations (entity
  name, `Role` as a `string`+CHECK rather than an enum or lookup table,
  CHECK/DEFAULT clauses, 72-byte limit location, and the new v2 item on
  confirming the check-constraint migration syntax generates correctly).
