# Package Map

Root namespace: `CustomerPortal`. Test namespaces mirror this tree under
`CustomerPortal.Tests`. Adding a namespace not listed here requires an
approved decision.

| Namespace | Contains | Depends on (allowed) | Notes |
|---|---|---|---|
| `Controllers` | ASP.NET Core `ControllerBase` classes (`[ApiController]`) | `Services`, `Models.Dtos`, `Models.Requests` | No business logic. No repository access. No entity in a signature. |
| `Services` | business logic, orchestration, entity↔DTO mapping, transaction boundaries | `Repositories`, `Models.Entities`, `Models.Dtos`, `Models.Requests`, `Exceptions`, `Validation`, `Security` (read-only helpers) | Owns transaction scopes. No MVC / `HttpContext` types. |
| `Repositories` | EF Core query/command classes over `AppDbContext` | `Models.Entities`, `Data` (for `AppDbContext`) | Queries only. No business logic. |
| `Models.Entities` | EF Core entity classes — persisted domain state | (none — leaf) | Never used as an API request/response type. |
| `Models.Dtos` | API **response** DTOs | (none — leaf) | No credential fields, ever. |
| `Models.Requests` | API **request** DTOs, validated by a FluentValidation validator | `Validation` | Bound and validated automatically on the Controller action parameter. |
| `Validation` | FluentValidation validators + reusable custom rules | `Models.Entities` (read-only, when a validator must query) | |
| `Security` | authentication/authorization setup, cookie auth handler config, password hasher, claims principal factory | `Repositories`, `Models.Entities`, `Config` | See `security-conventions.md`. |
| `Config` | `IServiceCollection` / `WebApplicationBuilder` extension methods (EF Core, Swagger, etc.) | framework only | No business logic. |
| `Data` | `AppDbContext`, `IEntityTypeConfiguration<T>` classes, EF Core Migrations | `Models.Entities` | Owns the model configuration and the migration history. |
| `Exceptions` | domain exception classes + the single `GlobalExceptionHandler` | `Models.Dtos` (error body) | Single place that maps exceptions → HTTP. |

## Dependency direction rules

- `Controllers` may depend on `Services`, `Models.Dtos`, `Models.Requests` —
  nothing else in the app.
- `Services` may depend on everything except `Controllers`.
- `Repositories` may depend only on `Models.Entities` and `Data`.
- `Models.Entities`, `Models.Dtos` are leaves (no intra-app dependencies).
- No cycles. `Repositories → Services`, `Repositories → Controllers`,
  `Services → Controllers`, `Controllers → Repositories` are all forbidden and
  are architecture violations (Major or Critical finding depending on
  impact).

## Test namespace rule

For a production class `CustomerPortal.<Namespace>.<Name>`, its tests live in
`CustomerPortal.Tests.<Namespace>`. Integration tests that span layers may sit
in a `CustomerPortal.Tests.<Feature>` namespace but still under the test root.
