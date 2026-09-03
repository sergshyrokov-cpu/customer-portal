---
artifact_type: api_design
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T13:48:20Z
updated_at: 2026-09-02T13:48:20Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/reviews/specifications/US-002-spec-review.md
    version: 2
  - path: docs/decisions/US-002-open-decisions.md
    version: 1
  - path: docs/architecture/api-conventions.md
    version: null
  - path: docs/architecture/security-conventions.md
    version: null
  - path: docs/product/business-rules.md
    version: null
  - path: docs/product/non-functional-requirements.md
    version: null
supersedes: null
---

# API Design — US-002 Customer Login (v1)

Companion to `docs/designs/api/US-002-openapi.yaml` (the authoritative
contract). This document is the traceability anchor: rationale, operation
notes, Acceptance-Criterion map, auth model, error model, and open questions.

## 1. Scope

US-002 adds exactly one operation: `POST /api/v1/sessions` — public
authentication of a Customer, establishing a cookie-based session. No existing
contract changes; purely additive (api-conventions.md AC-1). Login is
read-only against `customer`; no entity/schema change is required (Spec
NFR-4), so `DB_DESIGN` is expected to be `NOT_APPLICABLE` for this Story.

## 2. Open Decisions applied

| OD | Resolution | Effect on this contract |
|---|---|---|
| OD-001 | A — `POST /api/v1/sessions` | Path chosen: plural noun "sessions", no verb, consistent with api-conventions.md AC-3 |
| OD-002 | A — `200 OK` with `{ id, email, role }` | `LoginResponse` schema; **see §3 for the AC-4 exception this requires** |
| OD-003 | A — disabled account joins the uniform `401` | No separate status/body for AC-004; single `401` response covers AC-002/AC-003/AC-004 |
| OD-004 | A — cookie-auth challenge overridden to `401`/`403`, no redirect | Not directly visible in this contract (no `[Authorize]` endpoint exists yet); recorded in §6 as the auth-scheme configuration this Story is responsible for wiring, exercised first by US-003 |
| OD-005 | A — anti-abuse out of scope | No rate-limit headers, no `429` in this contract |
| OD-006 | A — explicit lifetime, this design fixes concrete values | `Set-Cookie` header description states 30 min sliding idle / 8h absolute cap — see §6 |
| OD-007 | A — logout out of scope | No `DELETE /api/v1/sessions` or equivalent in this contract |

## 3. Resolving the OD-001/OD-002 tension (Spec §11, flagged at HUMAN_SPEC_APPROVAL)

`api-conventions.md` AC-4 expects `POST /collection` to return `201 Created` +
`Location` + created-resource body. OD-001:A frames login as
`POST /api/v1/sessions` (a collection-shaped path), which would suggest `201`.
However, OD-002:A explicitly resolves the success status to `200 OK` with no
`Location` header. **This is recorded here as the Story's approved,
human-decided exception to AC-4**, for the following reason: unlike a
registered Customer, the "session" this endpoint creates is not a persisted,
independently addressable resource — there is no `GET /api/v1/sessions/{id}`
and no plan to add one (no session-listing/session-management scope in this
Story). A cookie is not a URL-addressable resource, so `201` + `Location`
would point at nothing retrievable. `200 OK` with the authenticated Customer's
identity is the more accurate contract. This deviation is analogous to
US-001's OD-002 CSRF exception: an explicit, recorded, human-approved
departure from a default convention, not a silent inconsistency.

## 4. Operation: `login`

| Property | Value |
|---|---|
| Method / path | `POST /api/v1/sessions` |
| Auth | None — public (SC-4 lists login as a public endpoint) |
| Request media type | `application/json` required; else `415` (AC-2) |
| Request body | `LoginRequest { email, password }`, `additionalProperties: false` |
| Idempotency | Not idempotent in the strict sense (each call re-establishes/refreshes a session cookie), but repeatable without side effects on `customer` data — login never writes to `customer` |
| Rate limiting | Out of scope (OD-005:A) |

### 4.1 Request schema `LoginRequest`

| Field | Type | Constraints | Source |
|---|---|---|---|
| `email` | string | required, non-blank; **no format re-validation** — an invalid-shaped value simply fails to match any account (Spec §6.1) | Spec §6.1, FR-3 |
| `password` | string, `writeOnly` | required, non-blank; **no policy re-validation** — a policy-violating value simply fails to match any hash (Spec §6.2) | Spec §6.2, FR-3 |

- `password` is `writeOnly` — never appears in any response schema (SC-1, SEC-3).
- Unlike `RegistrationRequest` (US-001), no `maxLength`/`format` constraints
  are declared, because login does not re-validate shape or policy — the
  Specification is explicit that this is deliberate (falls through to the
  uniform `401`, not a `400`), to avoid a validation-based side channel that
  could distinguish "malformed input" from "wrong credentials."

### 4.2 Response schema `LoginResponse` (`200` only)

| Field | Type | Notes |
|---|---|---|
| `id` | integer(int64) | authenticated Customer's surrogate id |
| `email` | string | normalized (lowercase) stored value |
| `role` | string enum `[CUSTOMER, ADMIN]` | the account's actual role (unlike US-001's registration response, which is always `CUSTOMER`) |

No `password`, no `password_hash`, no `enabled` (OD-002:A, SEC-3, SEC-4/Spec
AC-005).

### 4.3 Responses

| Status | Body | When |
|---|---|---|
| `200 OK` | `LoginResponse` + `Set-Cookie` | valid credentials, account enabled (AC-001, AC-005) |
| `400 Bad Request` | `ErrorResponse` (+ `fieldErrors[]`) | `email`/`password` missing or blank, or malformed JSON (AC-007) |
| `401 Unauthorized` | `ErrorResponse`, generic message | unknown email, wrong password, or disabled account — all identical (AC-002, AC-003, AC-004, OD-003:A) |
| `415 Unsupported Media Type` | `ErrorResponse` | missing/non-JSON `Content-Type` (AC-006) |
| `500 Internal Server Error` | `ErrorResponse` | unmapped exception; no internal leak (SC-9) |

`403`/`409` are **not applicable** to this operation (Spec §8).

## 5. Acceptance Criterion → operation / response map

| Spec AC | Operation | Observable outcome in contract |
|---|---|---|
| AC-001 Successful Login | `login` | `200` + `Set-Cookie` + `LoginResponse` |
| AC-002 Invalid Password | `login` | `401`, generic message |
| AC-003 Unknown Account | `login` | `401`, byte-for-byte identical to AC-002's response |
| AC-004 Disabled Account | `login` | `401`, byte-for-byte identical to AC-002/AC-003's response (OD-003:A) |
| AC-005 Secure Authentication Response | `login` | `LoginResponse` schema excludes password and hash on every path |
| AC-006 Media type | `login` | `415` when `Content-Type` is not `application/json` |
| AC-007 Request-shape enforcement | `login` | `400` + `fieldErrors[]` for missing/blank `email`/`password` |

Every API-relevant AC is covered.

## 6. Auth model

- **Authentication:** none required to *call* `POST /api/v1/sessions` (it is
  the endpoint that establishes authentication). Every other endpoint remains
  deny-by-default (SC-4); this Story adds no other route.
- **Authorization:** none for this operation (`x-authorization: none`).
- **Session establishment:** on success, `ICustomerAuthService` calls
  `SignInAsync` with a `ClaimsPrincipal` built from the Customer's id, email,
  and role claim (`ClaimTypes.Role`, exact value `CUSTOMER`/`ADMIN`, no
  `ROLE_` prefix — SC-2). This is the cookie issued via the `Set-Cookie`
  header documented on the `200` response.
- **Cookie configuration** (this Story is the first to register
  `AddAuthentication().AddCookie(...)`, so these are new project-wide
  settings, not just this operation's concern):
  - Cookie name: `CustomerPortal.Auth` (explicit, not the ASP.NET Core
    default `.AspNetCore.Cookies`, for clarity in browser dev tools /
    logs — no security implication either way).
  - `HttpOnly: true`, `Secure: true`, `SameSite: Strict`
    (`security-conventions.md` SC-5).
  - Lifetime (OD-006:A, concretized here within its approved 30–60
    min / 8–12h range): `SlidingExpiration: true`,
    `ExpireTimeSpan: 30 minutes` (idle timeout). An **absolute** 8-hour cap is
    not a built-in ASP.NET Core cookie-auth option — `IMPLEMENTATION` must add
    an explicit check (e.g. an `IssuedUtc` claim compared in
    `CookieAuthenticationEvents.OnValidatePrincipal`, rejecting the principal
    once 8 hours have elapsed regardless of sliding renewal). Flagged as
    **Q-1** below.
  - **Challenge behavior (OD-004:A):** `CookieAuthenticationEvents
    .OnRedirectToLogin` / `OnRedirectToAccessDenied` overridden to
    short-circuit with `401` / `403` (`context.Response.StatusCode = ...;
    return Task.CompletedTask;`) instead of the framework default `302`
    redirect, unconditionally — this project has no server-rendered login
    page (Out of Scope). This is infrastructure this Story wires but cannot
    itself exercise with a test (no `[Authorize]` endpoint exists yet; first
    exercised by US-003). Flagged as **Q-2** below, consistent with Spec
    FR-11 and SPEC_REVIEW v2 M-1.
- **CSRF:** unaffected by this Story; `security-conventions.md` SC-5's
  default (enabled for browser/session endpoints) is not touched, since login
  itself is a public, pre-session endpoint like registration and requires no
  prior CSRF token (same posture question as US-001 OD-002, not reopened
  here — the Story's Open Decisions did not raise it, and `security-conventions.md`
  SC-5 already treats an unauthenticated public endpoint as not requiring a
  CSRF token to reach it).

## 7. Error model

- Single JSON error shape from api-conventions.md AC-6: `timestamp`, `status`,
  `error`, `message`, `path`, optional `fieldErrors[]` — identical shape to
  US-001, reused without modification.
- Produced only by the single `GlobalExceptionHandler` in the `Exceptions`
  namespace (AC-9, FR-15, architecture.md AD-6).
- `message` is client-safe; never contains stack traces, SQL, class/package
  names, file paths, DB URLs, the submitted password, or any signal
  distinguishing *why* authentication failed (SC-9, SEC-4).
- The `401` response for AC-002/AC-003/AC-004 is a deliberate, non-negotiable
  anti-enumeration default (SC-3) extended by a human-approved decision
  (OD-003:A) to cover the disabled-account case too — not a default assumed
  by this design.

## 8. Conventions compliance checklist

| Convention | Status |
|---|---|
| AC-1 URI-path versioning (`/api/v1`) | ✅ `servers: /api/v1`, path `/sessions` |
| AC-2 `application/json`, `415` otherwise | ✅ |
| AC-3 plural noun, no verbs | ✅ `/sessions` |
| AC-4 `POST /collection` → `201` + `Location` + body | ⚠️ Explicit, recorded exception — `200`, no `Location` (§3) |
| AC-5 error codes | ✅ `400/401/415/500` |
| AC-6 error body shape | ✅ `ErrorResponse` + `FieldError`, reused verbatim from US-001 |
| AC-7 session auth, no `Authorization` header | ✅ cookie-based, no header |
| AC-8 pagination | n/a — no collection returned |
| AC-9 single `GlobalExceptionHandler` | ✅ stated in §7 (implementation obligation) |
| SC-1 BCrypt verification, plaintext only inbound | ✅ `writeOnly` password, §4.1 |
| SC-2 role claim exact value, no `ROLE_` prefix | ✅ §6 |
| SC-3 anti-enumeration (401 uniform, unknown/wrong-password) | ✅ fixed rule, §7 — not an OD outcome |
| SC-4 deny-by-default, login public | ✅ §6 |
| SC-5 cookie `HttpOnly`/`Secure`/`SameSite=Strict` | ✅ §6 |
| SC-9 error/log hygiene | ✅ §7 |

## 9. Open questions for downstream stages

| # | For | Question |
|---|---|---|
| Q-1 | IMPLEMENTATION | Implement the 8-hour absolute cookie-lifetime cap via an explicit `IssuedUtc`-claim check in `CookieAuthenticationEvents.OnValidatePrincipal` — ASP.NET Core's built-in sliding expiration alone does not enforce an absolute maximum (OD-006:A). |
| Q-2 | IMPLEMENTATION, SECURITY_REVIEW | Confirm `OnRedirectToLogin`/`OnRedirectToAccessDenied` are overridden to return `401`/`403` unconditionally (OD-004:A) — this Story wires it but cannot test it (no `[Authorize]` endpoint exists yet); US-003 is where a live test first becomes possible. |
| Q-3 | DB_DESIGN | Confirm no schema change is needed (login is read-only against `customer`); expected `NOT_APPLICABLE` verdict, unless a later stage identifies a need (e.g. a failed-attempt counter, which would require reopening OD-005). |
| Q-4 | TEST_WRITING | Cover the byte-for-byte identical `401` response across AC-002/AC-003/AC-004 (Spec NFR-5) — response body, status, and headers (excluding `Set-Cookie`, which is absent on failure) must be asserted equal. |

None of these block the contract.

## 10. Result

```yaml
result:
  verdict: PASS
  stage: API_DESIGN
  story: US-002
  artifact_status: DRAFT
  artifacts:
    - docs/designs/api/US-002-openapi.yaml
    - docs/designs/api/US-002-api-design.md
  next_stage: DB_DESIGN
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "OD-001/OD-002 tension (flagged at HUMAN_SPEC_APPROVAL) resolved as an explicit, recorded AC-4 exception: 200 OK with no Location header, because a cookie session is not an independently addressable resource. See api-design.md section 3."
    - "OD-006:A concretized within its approved range: 30-minute sliding idle timeout, 8-hour absolute cap. The absolute cap is not a built-in ASP.NET Core cookie-auth feature -- IMPLEMENTATION must add an explicit OnValidatePrincipal check (Q-1)."
    - "OD-004:A (401/403 challenge override) is wired by this Story's Program.cs registration but has no Acceptance Criterion or test in US-002 itself -- first live-exercised by US-003 (Q-2), consistent with SPEC_REVIEW v2 M-1."
    - "Expect DB_DESIGN to return NOT_APPLICABLE for this Story: login is read-only against customer, no entity/column change required (Q-3)."
    - "ErrorResponse/FieldError schemas reused verbatim from US-001's contract (same shape), not redefined differently."
```
