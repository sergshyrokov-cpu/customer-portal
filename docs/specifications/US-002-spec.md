---
artifact_type: specification
story: US-002
version: 2
status: DRAFT
created_at: 2026-09-02T13:29:50Z
updated_at: 2026-09-02T13:35:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-002-customer-login.md
    version: null
  - path: docs/evidence/US-002-clarification-report.md
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 1
supersedes: docs/specifications/US-002-spec.md v1
---

# Specification — US-002 Customer Login

> **v2 revision note:** fixes SPEC_REVIEW v1 finding F-1 (Major). v1's FR-7
> conflated a fixed, non-negotiable existing convention
> (`security-conventions.md` SC-3 already mandates a uniform `401` for an
> unknown email vs. a wrong password — not an Open Decision) with the
> genuinely open question of whether a disabled account joins that same
> response (**OD-003**). FR-7 is now the fixed rule only; a new FR-16 states
> the OD-003-governed disabled-account rule separately. §8's error table and
> the OD-003 row in §11 are corrected to match. No Acceptance Criterion or
> business rule changed. See
> `docs/reviews/specifications/US-002-spec-review.md` v1 §7/§8.

## 1. Overview

Enable a previously-registered, enabled Customer (created via US-001) to
authenticate with email and password over a public REST endpoint. On success
the system establishes an ASP.NET Core cookie-authenticated session
(`security-conventions.md` SC-3) and returns a response that never contains
credential material. Login fails with a single uniform response — no
distinguishable status, message, or body — whether the email is unknown or
the password is wrong (fixed by SC-3, see FR-7); whether a **disabled**
account joins that same uniform response is the recommended (draft,
unresolved) direction of **OD-003** (see FR-16).

This Specification is the primary source of truth for API design, planning,
testing, and implementation of US-002. It depends on US-001's `Customer`
entity, `IPasswordHasher`, and repository being available unchanged.

## 2. Business Goal

Allow a registered customer to authenticate securely (`product-vision.md` —
customer authentication is an MVP success criterion; `personas.md` — the
Customer persona's goal "Login securely").

## 3. Business Flow

1. An unauthenticated client submits `email` and `password` to the login
   endpoint (**OD-001**).
2. The system validates the request shape (both fields present, non-blank,
   `Content-Type: application/json`).
3. The system looks up a `Customer` by email, compared case-insensitively
   (consistent with `business-rules.md` BR-002).
4. If no account exists for that email, the system treats this as an
   authentication failure (Story AC-003).
5. If an account exists, the system verifies the submitted password against
   the stored BCrypt hash (`security-conventions.md` SC-1). A mismatch is an
   authentication failure (Story AC-002).
6. If the account exists, the password matches, but `Enabled` is `false`
   (`business-rules.md` BR-004), the system treats this as an authentication
   failure (Story AC-004) — per **OD-003**, draft: indistinguishable from
   steps 4 and 5.
7. Steps 4 and 5 (unknown email, wrong password) SHALL produce the identical
   response (status, body, timing characteristics not specified) — this is
   **fixed** by `security-conventions.md` SC-3's existing anti-enumeration
   default (FR-7), not an Open Decision. Whether step 6 (disabled account)
   joins that same response is a genuinely open question, governed by
   **OD-003**'s draft recommendation (FR-16).
8. On success, the system establishes an authenticated session (`SignInAsync`
   via an `ICustomerAuthService`, SC-3) carrying the customer's id, email, and
   role, and returns a response per **OD-001** / **OD-002**.
9. No step in this flow ever creates, modifies, or deletes a `Customer` row;
   login is read-only against the `customer` table.

## 4. Functional Requirements

| id | Requirement |
|---|---|
| FR-1 | The system SHALL expose a public (no authentication required) login endpoint per **OD-001** (draft: `POST /api/v1/sessions`) that authenticates a Customer given `email` and `password`. |
| FR-2 | The request body SHALL be `application/json` with exactly two fields: `email` (string) and `password` (string). A request without `Content-Type: application/json` SHALL be rejected with `415`. |
| FR-3 | The system SHALL validate that `email` and `password` are present and non-blank before any persistence access; on failure it SHALL return `400` with the `api-conventions.md` AC-6 body including a `fieldErrors[]` entry per failed field, and SHALL NOT attempt authentication. No format re-validation of `email` is required here — format was already enforced at registration (US-001). |
| FR-4 | The system SHALL look up the `Customer` by `email`, compared case-insensitively. If none exists, this SHALL be treated as an authentication failure (Story AC-003), producing the response defined by FR-7. |
| FR-5 | If a `Customer` is found, the system SHALL verify the submitted password against the stored `password_hash` using the same `IPasswordHasher` abstraction introduced by US-001 (`security-conventions.md` SC-1). A mismatch SHALL be treated as an authentication failure (Story AC-002), producing the response defined by FR-7. |
| FR-6 | If the found `Customer`'s `Enabled` column (`business-rules.md` BR-004) is `false`, the system SHALL treat this as an authentication failure (Story AC-004). Whether this failure's response is distinguishable from FR-7's is governed by **OD-003** — see FR-16. |
| FR-7 | An authentication failure caused by an unknown email (FR-4) or a wrong password (FR-5) SHALL produce a single, identical response: `401 Unauthorized` with the `api-conventions.md` AC-6 body and a generic message such as "Invalid email or password". This uniformity and status code are **fixed by `security-conventions.md` SC-3's existing anti-enumeration convention — not subject to OD-003 or any decision this Story can revisit.** The system SHALL NOT reveal, via status code, message text, or response timing design, which of the two cases occurred. |
| FR-8 | On successful authentication, the system SHALL establish a cookie-authenticated session (`AddAuthentication().AddCookie(...)`, `SignInAsync`) carrying a `ClaimsPrincipal` built from the authenticated Customer's id, email, and role claim (`ClaimTypes.Role` = `CUSTOMER`/`ADMIN`, no `ROLE_` prefix — `security-conventions.md` SC-2), via the same `ICustomerAuthService` abstraction responsible for `SignInAsync`/`SignOutAsync` (SC-3). |
| FR-9 | On success, the system SHALL return a response per **OD-001** / **OD-002** (draft: `200 OK` with a JSON body `{ id, email, role }`). |
| FR-10 | The response body on any path (success or failure) SHALL NOT contain the plaintext password or the password hash, at any point (`security-conventions.md` SC-1, SC-9; Story AC-005). |
| FR-11 | Because this Story is the first to register the ASP.NET Core cookie-authentication scheme, the challenge behavior for any endpoint that later requires `[Authorize]` (starting with US-003) SHALL be configured per **OD-004** (draft: `CookieAuthenticationEvents.OnRedirectToLogin`/`OnRedirectToAccessDenied` overridden to return `401`/`403` with no redirect, since this project has no server-rendered login page). This requirement produces no independently testable behavior within US-002 itself (no `[Authorize]` endpoint exists yet), but the registration/configuration work happens in this Story. |
| FR-12 | The authentication cookie's lifetime (sliding/absolute expiration and duration) SHALL follow **OD-006**. |
| FR-13 | Whether a logout (session-termination) endpoint is delivered by this Story is governed by **OD-007** (draft: out of scope; a follow-up Story delivers logout). |
| FR-14 | Anti-abuse / brute-force protection (rate limiting, lockout) on the login endpoint is governed by **OD-005** (draft: out of scope for this Story). |
| FR-15 | Exception-to-HTTP mapping SHALL occur only in the single `GlobalExceptionHandler` in the `Exceptions` namespace (`api-conventions.md` AC-9; `architecture.md` AD-6). Controllers SHALL NOT build error responses. |
| FR-16 | Whether a disabled-account authentication failure (FR-6) produces the **exact same response as FR-7** or a **distinguishable** response is governed by **OD-003** (draft: identical to FR-7, per OD-003 option A). Unlike FR-7, this rule is genuinely open and is the actual subject of OD-003. |

## 5. Acceptance Criteria

Ids are stable and traceable to the Story.

| id | Story AC | Statement |
|---|---|---|
| AC-001 | AC-001 | Given a registered, enabled Customer with a known email/password, when login is submitted with matching credentials, then an authenticated session is established and the response follows OD-001/OD-002's resolution. |
| AC-002 | AC-002 | Given a registered Customer, when login is submitted with the correct email but a wrong password, then the response is the uniform authentication-failure response defined by FR-7, and no session is established. |
| AC-003 | AC-003 | Given no account exists for the submitted email, when login is submitted, then the response is **identical** to AC-002's response (same status, same body), and no session is established. |
| AC-004 | AC-004 | Given a registered Customer whose account is disabled (`Enabled = false`), when login is submitted with the correct password, then the response follows FR-16 / OD-003's resolution (draft: identical to AC-002/AC-003's FR-7 response), and no session is established. |
| AC-005 | AC-005 | Given any login attempt (success or failure), when the response is returned, then the body contains neither the submitted password nor the stored password hash. |
| AC-006 | derived (AC-2, `api-conventions.md`) | Given a request without `Content-Type: application/json`, when login is submitted, then the response is `415` and no session is established. |
| AC-007 | derived (§6, FR-3) | Given a request missing `email` or `password` (or either is blank), when login is submitted, then the response is `400` with a `fieldErrors[]` entry per missing/blank field, and no session is established. |

## 6. Validation Rules

Server-side only; no reliance on framework defaults (`non-functional-requirements.md`
NFR-002).

### 6.1 `email`

| Rule | Value | On failure |
|---|---|---|
| Required | must be present and non-blank | `400`, `fieldErrors[].field = "email"` |
| Format | **not re-validated here** — format was already enforced at registration time (US-001); a syntactically-invalid-but-present email simply will not match any stored account and falls through to the uniform authentication failure (FR-7), not a `400`. | n/a |

### 6.2 `password`

| Rule | Value | On failure |
|---|---|---|
| Required | must be present and non-blank | `400`, `fieldErrors[].field = "password"` |
| Policy (length/character classes) | **not re-validated here** — login only verifies the submitted value against the stored hash; a policy-violating submitted password simply will not match any real hash and falls through to the uniform authentication failure (FR-7), not a `400`. | n/a |

### 6.3 Request shape

- Unknown / extra JSON fields: follows the same decision as US-001's
  equivalent (left to API_DESIGN, consistent handling across endpoints).
- Malformed JSON: `400` with the AC-6 body.

## 7. Security Requirements

All cited from `security-conventions.md` (SC-*) or an Open Decision — none
invented.

| id | Requirement | Source |
|---|---|---|
| SEC-1 | The login endpoint is the only public endpoint added by this Story; every other endpoint remains deny-by-default. | SC-4 |
| SEC-2 | Password verification reuses the approved `IPasswordHasher` abstraction (`BCrypt.Net-Next`) introduced by US-001; no new hashing/verification mechanism is introduced. | SC-1 |
| SEC-3 | The plaintext password appears only on the inbound request DTO. It is never placed on a response DTO, never persisted, never logged, never included in an error message. | SC-1, SC-9 |
| SEC-4 | No account enumeration: an unknown email, a wrong password, and (per OD-003's draft) a disabled account all produce the identical response. Any narrowing of this (OD-003 option B) is an accepted, human-approved decision, not a default. | SC-3, OD-003 |
| SEC-5 | Successful authentication issues a `ClaimsPrincipal` with role claim value exactly `CUSTOMER`/`ADMIN` (no `ROLE_` prefix) via `SignInAsync`, called only from `ICustomerAuthService`. | SC-2, SC-3 |
| SEC-6 | The cookie-authentication scheme's unauthorized-access challenge behavior follows **OD-004** — relevant because this Story is the first to register `AddCookie(...)`. | OD-004 |
| SEC-7 | Anti-abuse / rate limiting on login follows **OD-005** (draft: out of scope). | OD-005 |
| SEC-8 | The authentication cookie's lifetime follows **OD-006**. | OD-006 |
| SEC-9 | Error responses never leak stack traces, SQL, entity/class names, file paths, or database URLs. | SC-9, AC-6 |
| SEC-10 | No database browser/admin UI is registered or exposed in any profile; this Story does not change it. | SC-6 |
| SEC-11 | No secrets are introduced or committed by this Story. | SC-7 |

## 8. Error Handling

All error responses use the `api-conventions.md` AC-6 JSON shape, produced by
the single `GlobalExceptionHandler` (AC-9).

| Condition | Status | `error` | Notes |
|---|---|---|---|
| Request-shape failure (missing/blank `email`/`password`), malformed JSON | `400` | `Bad Request` | includes `fieldErrors[]` for field failures |
| Unknown email / wrong password (uniform, FR-7 — fixed by SC-3) | `401` | `Unauthorized` | identical body for both cases; **not** subject to OD-003 |
| Disabled account (FR-6) | `401` (draft, per OD-003/FR-16) | `Unauthorized` | joins the row above under OD-003's draft option A; a distinguishable response (e.g. `403`) is OD-003 option B |
| Missing / wrong `Content-Type` | `415` | `Unsupported Media Type` | |
| Unmapped exception | `500` | `Internal Server Error` | no internal detail leaked (SC-9) |

`403`/`409` are not applicable to this Story's endpoint. `403` becomes
relevant only once a Story adds role-restricted resources (not this Story).

## 9. Non-Functional Requirements

| id | Requirement | Source |
|---|---|---|
| NFR-1 | Password verification uses the approved BCrypt-based hasher. | NFR-001, SC-1 |
| NFR-2 | All user input validated server-side. | NFR-002 |
| NFR-3 | REST conventions followed (`/api/v1`, JSON, status codes, AC-6 error body). | NFR-003, `api-conventions.md` |
| NFR-4 | No new entity/column is required unless a chosen Open Decision option demands one (e.g. OD-005 resolved as "in scope" would add a failed-attempt counter). | NFR-004 |
| NFR-5 | New functionality includes happy-path, validation, and security tests — including explicit assertions that AC-002/AC-003/AC-004's responses are byte-for-byte identical. | NFR-005 |
| NFR-6 | Implementation is traceable to this Story, this Specification, and the test artifacts. | NFR-006 |
| NFR-7 | The build succeeds before the change is considered complete. | NFR-007 |
| NFR-8 | Controller → Service → Repository layering. | NFR-008 |

## 10. Out of Scope

- Registration (delivered by US-001).
- Profile management (US-003).
- Multi-factor authentication.
- OAuth / social login.
- Password recovery / reset.
- Logout (per **OD-007** draft — may move in scope if resolved otherwise).
- Rate limiting / anti-abuse on login (per **OD-005** draft).
- Administrative enable/disable of a customer account (this Story only reads
  `Enabled`; it does not add a way to set it).

## 11. Open Decisions (with impact)

Recorded in `docs/decisions/US-002-open-decisions.md`. Unresolved; decided by
a human at `HUMAN_SPEC_APPROVAL`. Each dependent requirement above is marked
with its OD id and a "draft" assumption reflecting the recommended option.

| id | Question | Requirements it governs | Impact if the recommendation is not chosen |
|---|---|---|---|
| OD-001 | Login endpoint path/method/success-status shape | FR-1, FR-9, §5, §8 | Changes the route, and whether success is `200` or `201` with `Location`; API design and every test asserting the endpoint URL change. |
| OD-002 | Successful login response body/status | FR-9, AC-001, §5 | Changes the response DTO and the success-path test assertions. **Tension to confirm at HUMAN_SPEC_APPROVAL:** OD-001's recommended option A (`POST /api/v1/sessions`, a resource-creation framing) would suggest `201` per `api-conventions.md` AC-4, while OD-002's recommendation is `200`; if both recommended options are accepted together this inconsistency should be explicitly resolved, not left implicit. |
| OD-003 | Disabled-account response vs. enumeration | FR-6, FR-16, AC-004, §8, SEC-4 | If AC-004 is made distinguishable from AC-002/AC-003 (option B), FR-16 changes from "identical to FR-7" to a distinct rule, a `403` row is added to §8, and the "uniform response" tests in NFR-5 no longer apply to AC-004. FR-7 itself (AC-002/AC-003's uniformity) is unaffected either way — it is fixed by SC-3, not by this decision. |
| OD-004 | Cookie-auth challenge behavior (401 vs. redirect) | FR-11, SEC-6 | If the framework default (`302`) is kept, `security-conventions.md` SC-3/`api-conventions.md` AC-5's `401` commitment must be recorded as a documented exception instead; no observable change within US-002 itself, but directly affects US-003's design. |
| OD-005 | Anti-abuse controls on login | FR-14, SEC-7, §10 | If in scope, new functional + security requirements (lockout/delay) and tests are added. |
| OD-006 | Session/cookie lifetime | FR-12, SEC-8 | Changes the `AddCookie()` `ExpireTimeSpan`/`SlidingExpiration` configuration and any test asserting cookie attributes. |
| OD-007 | Logout endpoint inclusion | FR-13, §10 | If in scope, a new endpoint (e.g. `DELETE /api/v1/sessions`), its own functional/security requirements, and tests are added to this Story. |

None of these prevent stating the mandatory requirements — each is captured
as a documented gap with a draft assumption. Verdict is therefore not
`BLOCKED`.

## 12. Traceability

### 12.1 Acceptance Criterion → requirements

| AC | Functional requirement(s) | Validation / security rule(s) |
|---|---|---|
| AC-001 | FR-1, FR-4, FR-5, FR-6, FR-8, FR-9 | SEC-2, SEC-5; §6 (valid input path) |
| AC-002 | FR-5, FR-7 | SEC-4; §8 uniform-failure row |
| AC-003 | FR-4, FR-7 | SEC-4; §8 uniform-failure row |
| AC-004 | FR-6, FR-16 | SEC-4; §8 disabled-account row |
| AC-005 | FR-10 | SEC-3 |
| AC-006 | FR-2 | §8 `415` row |
| AC-007 | FR-3 | §6.1/§6.2 required-field rows |

### 12.2 Story AC → Specification AC

| Story | Specification |
|---|---|
| AC-001 Successful Login | AC-001 |
| AC-002 Invalid Password | AC-002 |
| AC-003 Unknown Account | AC-003 |
| AC-004 Disabled Account | AC-004 |
| AC-005 Secure Authentication Response | AC-005 |
| (derived) media type enforcement | AC-006 |
| (derived) request-shape enforcement | AC-007 |

### 12.3 Requirement → source

| Requirement | Source |
|---|---|
| Public login endpoint, no verbs in path | `api-conventions.md` AC-1, AC-3; SC-4 |
| BCrypt verification, no plaintext exposure | `security-conventions.md` SC-1, SC-9; NFR-001 |
| Case-insensitive email lookup | `business-rules.md` BR-002 |
| Disabled account cannot authenticate | `business-rules.md` BR-004 |
| Cookie-based session, role claims | `security-conventions.md` SC-2, SC-3; `api-conventions.md` AC-7 |
| AC-6 error body, single `GlobalExceptionHandler` | `api-conventions.md` AC-5, AC-6, AC-9 |
| Layering, build stability, test coverage, traceability | `non-functional-requirements.md` NFR-005–NFR-008 |
