# Persistence Conventions

Explicit persistence decisions for this project. `db-designer` and
`design-reviewer` enforce these; `aspnet-implementor` implements to them;
`implementation-verifier` and `security-reviewer` check against them.

## PC-1 Database & runtime mode

- SQLite, **file-based**, for local training runs. Connection string form:
  `Data Source=./App_Data/customer-portal.db` (project-relative
  `./App_Data/` directory), registered via `AddDbContext<AppDbContext>(o =>
  o.UseSqlite(connectionString))`. `App_Data` (not `data`) is deliberate:
  `dotnet run`'s working directory is the project directory, and on a
  case-insensitive filesystem a lowercase `data/` collides with the
  `Data/` namespace folder (`CustomerPortal/Data/`) — confirmed during
  US-001 implementation, where this collision briefly deleted real source
  files via an unrelated cleanup command. `App_Data` cannot collide with
  any C# namespace.
- The generated database file (`./App_Data/*.db`, `*.db-shm`, `*.db-wal`)
  is **not** committed — it is covered by `.gitignore` (by extension, not
  by folder name).
- Automated tests use an **isolated in-memory** SQLite connection per test
  class (`Data Source=:memory:`, keeping one `SqliteConnection` open for the
  `DbContext`'s lifetime), never the file database.
- No database browser/admin UI (e.g. a SQLite viewer) is ever wired into the
  application or exposed over HTTP, in any environment.

## PC-2 Schema initialization

- Schema is **explicit and version-controlled** as EF Core Migrations
  (`dotnet ef migrations add <Name>`), committed under
  `CustomerPortal/Data/Migrations/`. Each migration is reviewed like any other
  code change.
- `Database.EnsureCreated()` / `EnsureDeleted()` are **forbidden** outside the
  isolated per-test in-memory database — they bypass the migration history and
  can silently diverge from it (equivalent risk to Hibernate `ddl-auto:
  create/create-drop/update`).
- Local/dev startup applies pending migrations explicitly via
  `db.Database.Migrate()`, gated behind a config flag
  (`Persistence:AutoMigrate`), never unconditionally on every boot in a shared
  environment.
- Tests apply the **same committed migrations** to the isolated in-memory
  database (`db.Database.Migrate()`) rather than generating the schema from
  the current model — this is the equivalent of Hibernate `ddl-auto: validate`:
  it fails fast if an entity change was made without a matching migration.
- Every entity change is accompanied by the matching migration in the same
  Story. `db-designer` specifies both; `design-reviewer` checks they agree.

## PC-3 Identifiers

- Surrogate primary key named `Id` (column `id`), type `long`, EF Core
  default value generation (`INTEGER PRIMARY KEY AUTOINCREMENT` under
  SQLite) — no explicit `[Key]`/`[DatabaseGenerated]` override needed unless
  deviating from this.
- No business/natural key as a primary key. Natural keys (e.g. email) get a
  `UNIQUE` constraint instead.
- IDs are not exposed as sequential where enumeration is a concern the Story
  raises — otherwise a `long` id in the API is acceptable for this project.

## PC-4 Explicit column mapping (no EF Core convention defaults)

Every persistent property is configured explicitly in a matching
`IEntityTypeConfiguration<TEntity>` class under `Data/Configurations/`
(applied via `ApplyConfigurationsFromAssembly()`), never left to bare EF Core
conventions and never scattered as data-annotation attributes on the entity:

- `.IsRequired()` / `.HasMaxLength(...)` — max length required for every
  `string` property;
- `.HasIndex(...).IsUnique()` for uniqueness;
- `.ToTable(...)` / `.HasColumnName(...)` when the table/column name would
  otherwise differ from the naming convention in PC-5.

`db-designer` states the exact constraints; the entity configuration class and
the generated migration must both match them.

## PC-5 Naming

- Tables: `snake_case`, singular (`customer`, `customer_role`).
- Columns: `snake_case`.
- Constraints: `uq_<table>_<col>` (unique), `fk_<table>_<ref>` (foreign key),
  `ix_<table>_<col>` (index), `pk_<table>` (primary key) — set explicitly via
  `.HasConstraintName(...)` / `.HasDatabaseName(...)` in the entity
  configuration.
- The `EFCore.NamingConventions` package is registered
  (`.UseSnakeCaseNamingConvention()` on the `DbContextOptionsBuilder`) so a
  C# property `EmailAddress` maps to column `email_address` by default; PC-4's
  explicit configuration overrides it only where a name must diverge.

## PC-6 Audit timestamps

- Every entity has `CreatedAt` and `UpdatedAt` properties (columns
  `created_at`, `updated_at`), type `DateTimeOffset`, stored in **UTC**
  (see `business-rules.md` BR-007).
- Populated by overriding `SaveChangesAsync` in `AppDbContext`: on every
  tracked entity in `Added` state set `CreatedAt` and `UpdatedAt` to
  `DateTimeOffset.UtcNow`; on every entity in `Modified` state set only
  `UpdatedAt`. Not set by hand in a Service.
- `CreatedAt` is non-null and never reassigned after insert (enforced by the
  `SaveChangesAsync` override, not by a database trigger); `UpdatedAt` is
  non-null.

## PC-7 Indexes

- Index every foreign key column (EF Core creates this automatically for a
  configured relationship; verify it is present in the generated migration).
- Index every column used as a lookup key by a repository query (e.g. `email`
  for "find by email"). A `UNIQUE` index already covers lookup.
- `db-designer` lists the required indexes; the entity configuration class and
  migration create them.

## PC-8 Relationships

- Declare cardinality explicitly (`.HasOne(...).WithMany(...)`,
  `.HasMany(...).WithOne(...)`, etc.) in the entity configuration class.
- Navigation properties are loaded explicitly via `.Include(...)` /
  `.ThenInclude(...)` in the Repository. Lazy-loading proxies
  (`UseLazyLoadingProxies`) are **not** used in this project — EF Core's
  default (explicit loading) is the project convention, not an opt-out.
- `DeleteBehavior` is explicit and minimal on every relationship
  (`.OnDelete(DeleteBehavior.Restrict)` unless a cascade is the stated,
  reasoned requirement) — no relying on the EF Core cascade default without a
  stated reason.

## PC-9 Sensitive data

- Passwords are stored only as a BCrypt hash (via the `BCrypt.Net-Next`
  package) in a column named `password_hash` (`TEXT`, non-null, 60-character
  BCrypt output). Plaintext is never persisted or logged.
- Other sensitive columns are identified by `db-designer` with their handling
  rules.
