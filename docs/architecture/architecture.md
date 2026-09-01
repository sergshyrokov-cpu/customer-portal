# Architecture

Explicit architecture decisions for the Customer Portal training project. These
are project decisions, not general framework advice. Skills
(`impact-analyzer`, `implementation-planner`, `aspnet-implementor`,
`design-reviewer`, `implementation-verifier`, `security-reviewer`,
`reconciliation-reviewer`) treat this file as authoritative.

## AD-1 Solution & project layout

- Single .NET solution (`CustomerPortal.sln`) with two projects: `CustomerPortal`
  (production code) and `CustomerPortal.Tests` (test code). No further project
  split in this training project.
- Production code under `CustomerPortal/` using root namespace
  `CustomerPortal`.
- Test code mirrors the production namespace tree under `CustomerPortal.Tests/`.
- No new projects or solution folders without an approved decision.

## AD-2 Layered architecture

```
Controller → Service → Repository → Database
```

| Layer | Namespace | Responsibility | Must not |
|---|---|---|---|
| Controller | `Controllers` | HTTP mapping, request/response DTO binding, delegate to a Service, map Service outcomes to HTTP status | contain business rules; call a Repository; return an entity |
| Service | `Services` | all business logic, orchestration, transaction boundaries, mapping between entities and DTOs | depend on `HttpContext` / MVC types (`ControllerBase`, `IActionResult`); call another Service's Controller |
| Repository | `Repositories` | EF Core queries against `AppDbContext` only | contain business logic; call a Service |
| Entity | `Models.Entities` | persisted domain state | be serialized as an API request/response |

Allowed dependency directions: `Controllers → Services → Repositories`.
Everything else in that set is forbidden (`Controllers → Repositories`,
`Controllers → Models.Entities` as an API type, `Repositories → Services`,
`Repositories → Controllers`, `Services → Controllers`).

## AD-3 Transaction boundary policy

- Transactions begin and end in the **Service** layer.
- A Service method that calls more than one Repository method, or that must
  see a consistent snapshot, wraps the calls in an explicit
  `IDbContextTransaction` (`await using var tx = await _db.Database
  .BeginTransactionAsync();` … `await tx.CommitAsync();`).
- A Service method that performs a single `SaveChangesAsync()` relies on EF
  Core's implicit per-`SaveChanges` transaction — no explicit transaction
  object needed.
- Controllers and Repositories must not open a transaction or call
  `SaveChangesAsync()`.
- `DbContext` is registered scoped (per-request) via
  `AddDbContext<AppDbContext>()`; no manual `DbContext` lifetime management in
  a Controller or Service.

## AD-4 DTO / entity boundary

- Every API request body binds to a `record` or `class` in `Models.Requests`.
- Every API response body is a `record` or `class` in `Models.Dtos`.
- Entities (`Models.Entities`) never appear in a Controller action signature, a
  request body, or a response body.
- Mapping entity ↔ DTO/request happens in the Service layer (a dedicated
  mapper class/extension method is allowed; a mapping library such as
  AutoMapper/Mapster is not added without an approved decision).
- A response DTO includes only fields the API contract lists. Credential
  fields (password, password hash) are never present on a response DTO, even
  as `null`.

## AD-5 Validation boundary

- Request-shape validation (required, length, format, allowed values):
  FluentValidation validators (`AbstractValidator<TRequest>`) in the
  `Validation` namespace, one validator class per request type, registered via
  `AddValidatorsFromAssemblyContaining<...>()` and run automatically through
  the FluentValidation ASP.NET Core auto-validation filter. `FluentValidation`
  and `FluentValidation.AspNetCore` are required dependencies for this.
- Business-rule validation (uniqueness, cross-field rules, state checks): in
  the Service layer, before persistence.
- Custom, reusable validation rules live as extension methods or custom
  `PropertyValidator` classes in the `Validation` namespace.
- Validation is server-side and independent of any client.

## AD-6 Exception handling architecture

- One `GlobalExceptionHandler` class in the `Exceptions` namespace,
  implementing `IExceptionHandler` (registered via
  `AddExceptionHandler<GlobalExceptionHandler>()` +
  `UseExceptionHandler(_ => { })`), is the single place that maps exceptions
  to HTTP responses.
- Domain/application exceptions are declared in the `Exceptions` namespace
  (e.g. `DuplicateEmailException`, `ResourceNotFoundException`). Services
  throw these; they carry no HTTP concepts.
- The handler maps: FluentValidation failure → 400; domain "not found" → 404;
  domain "conflict/duplicate" → 409; authn failure → 401; authz failure → 403;
  anything unmapped → 500.
- Every error response body uses the structure defined in
  `api-conventions.md` (§ Errors). Stack traces, SQL, entity/class names,
  database paths, and secrets are never in a response body.

## AD-7 Configuration boundaries

- Framework/infrastructure service registration (DI wiring, EF Core, MVC,
  Swagger/OpenAPI) lives in `Program.cs`, factored into `IServiceCollection`
  extension methods under `Config` (e.g. `AddPersistence()`,
  `AddApplicationServices()`) when `Program.cs` would otherwise grow past a
  handful of calls.
- Security/authentication/authorization registration lives in `Security` (see
  `security-conventions.md`).
- No business logic in a `Config` extension method.
- Application settings come from `appsettings.json` /
  `appsettings.{Environment}.json` / environment variables, never hard-coded;
  secrets never committed (see `security-conventions.md`).

## AD-8 Reuse over duplication

Before creating a component, check for an existing one that can be extended
within these rules. New namespaces beyond the map in `package-map.md` require
an approved decision (an Open Decision resolved by a human, not a silent
addition).
