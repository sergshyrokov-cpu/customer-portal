---
artifact_type: design_review
story: US-002
version: 1
status: APPROVED
created_at: 2026-09-02T13:52:22Z
updated_at: 2026-09-02T13:52:22Z
produced_by: design-reviewer
inputs:
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/reviews/specifications/US-002-spec-review.md
    version: 2
  - path: docs/decisions/US-002-open-decisions.md
    version: 1
supersedes: null
---

# Design Review — US-002 Customer Login (v1)

## 1. Summary

Reviewed `docs/designs/api/US-002-openapi.yaml` v1 and
`docs/designs/api/US-002-api-design.md` v1 against Specification v2 (and its
review v2), `docs/decisions/US-002-open-decisions.md` v1, `architecture.md`,
`api-conventions.md`, `security-conventions.md`, `package-map.md`, and
`business-rules.md`. `DB_DESIGN` correctly returned `NOT_APPLICABLE`
(independently re-confirmed, §3). **0 Critical, 0 Major, 3 Minor findings.
Verdict: PASS.**

## 2. Reviewed Artifacts

| Artifact | Version |
|---|---|
| docs/designs/api/US-002-openapi.yaml | 1 |
| docs/designs/api/US-002-api-design.md | 1 |
| docs/specifications/US-002-spec.md | 2 |
| docs/reviews/specifications/US-002-spec-review.md | 2 |
| docs/decisions/US-002-open-decisions.md | 1 |
| docs/designs/database/US-001-db-design.md, US-001-entity-model.md | 2 (unchanged, reused) |

## 3. Database design: NOT_APPLICABLE re-confirmed

Independently re-checked against Specification v2 NFR-4 ("No new
entity/column is required unless a chosen Open Decision option demands one")
and the recorded `HUMAN_SPEC_APPROVAL` resolution OD-005:A (anti-abuse /
failed-attempt counter out of scope — the only OD that could have triggered a
schema change). No other approved decision implies persistence change. Login
is read-only against `customer`. `DB_DESIGN`'s `NOT_APPLICABLE` verdict is
correct.

## 4. API Design Review

- Every API-relevant Acceptance Criterion (AC-001, AC-002, AC-003, AC-004,
  AC-005, AC-006, AC-007) maps to the `login` operation and a specific status
  code (api-design.md §5) — independently re-verified against the OpenAPI
  file's `responses` block; matches exactly.
- Path/method/versioning: `POST /api/v1/sessions` — plural noun, no verb
  (`api-conventions.md` AC-3), under `/api/v1` (AC-1). Correct per OD-001:A.
- Media type: `application/json` required, `415` otherwise (AC-2) — present.
- Request schema `LoginRequest`: `email`/`password` required, non-blank;
  **no** format/`maxLength`/policy constraints — correctly matches
  Specification §6.1/§6.2's deliberate choice not to re-validate shape/policy
  at login (avoids a validation-based side channel). Confirmed this is a
  documented deliberate choice, not an oversight.
- Response schema `LoginResponse`: `id`, `email`, `role` only — no credential
  field, no `enabled`. Matches OD-002:A and Spec AC-005/FR-10. `role` enum
  correctly includes both `CUSTOMER` and `ADMIN` (unlike US-001's
  registration response, which is always `CUSTOMER`) — this is a case the
  original US-001 design didn't need to handle and is handled correctly here.
- Error model: `400`/`401`/`415`/`500` only, matching Specification §8
  exactly (no `403`/`409`, correctly justified — this endpoint has neither a
  role-restricted resource nor a uniqueness conflict). Reuses `ErrorResponse`/
  `FieldError` verbatim from US-001 (byte-for-byte compared) — no
  unauthorized divergence in the shared error contract.
- Auth: `security: []`, `x-authorization: none` — correct, login itself is
  public (SC-4).
- Backward compatibility: purely additive (`api-design.md` §1); no existing
  contract changed.

## 5. Database Design Review

Not applicable (§3). No entities, columns, or migrations introduced or
changed by this Story.

## 6. Cross-Model Consistency

- `LoginResponse.id`/`email`/`role` map to the existing `Customer` entity's
  `Id` (`long`), `Email` (`string`), `Role` (`string`, `CUSTOMER`/`ADMIN`)
  from `US-001-entity-model.md` v2 — types and semantics agree; no new field
  invented.
- No new namespace is implied: `SessionsController` fits the existing
  `Controllers` namespace, `LoginRequest`/`LoginResponse` fit
  `Models.Requests`/`Models.Dtos`, and the cookie-auth wiring
  (`ICustomerAuthService`, `AddCookie(...)` events) fits the existing
  `Security` namespace exactly as `package-map.md` defines it ("cookie auth
  handler config" is explicitly listed there). No `AD-7`/`AD-8` violation, no
  approved-decision gap.
- Uniqueness/validation: not applicable — login introduces no new uniqueness
  or persistence-level validation rule; it only reads the rule US-001 already
  enforces.

## 7. Security Review of Designs

- Anti-enumeration (SC-3, extended by OD-003:A): the design's single `401`
  response for all three failure cases is correctly the only failure
  response defined — no separate schema, status, or field exists anywhere in
  the contract that could leak which case occurred. Confirmed by inspection,
  not just by the accompanying prose.
- Role-claim handling (SC-2): `api-design.md` §6 states the exact claim value
  requirement (no `ROLE_` prefix) consistently with US-001's precedent.
- **Finding M-1 (Minor):** `api-design.md` §6 states the authentication
  cookie is `HttpOnly: true` and `Secure: true`. `security-conventions.md`
  SC-5 explicitly mandates only `SameSite=Strict` (or `Lax`); it does not
  independently state `HttpOnly`/`Secure`. This is a security-*strengthening*
  addition (not a weakening), and matches ASP.NET Core's own
  `AddCookie()` defaults for `HttpOnly` — but it is technically a design
  decision not textually backed by an approved convention or Open Decision.
  Non-blocking (no risk introduced), but recommend `security-conventions.md`
  SC-5 be updated in a future pass to state `HttpOnly`/`Secure` explicitly, so
  this is a stated convention rather than an inferred default the next
  Story's designer must re-derive.
- **Finding M-2 (Minor, informational):** The specific cookie name
  (`CustomerPortal.Auth`) is a new, non-binding naming choice with no
  security or business impact (it does not appear in any Acceptance
  Criterion or Open Decision). Acceptable as a design-level default;
  `IMPLEMENTATION` may rename it without a Specification/design change if a
  better convention emerges.
- OD-006:A's concrete values (30-minute sliding / 8-hour absolute cap) are a
  legitimate concretization within the *example* range OD-006's own approved
  option text gave ("e.g. 30–60 minutes" / "e.g. 8–12 hours") — this is normal
  API_DESIGN-stage responsibility, not a re-opening of the human's decision.
  Confirmed acceptable, not a finding.
- OD-004:A's challenge-behavior override is correctly scoped as
  infrastructure this Story wires but cannot itself test (no `[Authorize]`
  endpoint exists yet) — consistent with SPEC_REVIEW v2's M-1 and correctly
  carried forward as Q-2 for `IMPLEMENTATION`/`SECURITY_REVIEW` and US-003.

## 8. Open Decisions

All 7 Open Decisions (OD-001 through OD-007) are correctly applied and
recorded in `api-design.md` §2, with the OD-001/OD-002 tension explicitly
resolved and justified in §3 (not left as a silent inconsistency) — confirmed
sound: a cookie session is not an independently retrievable resource, so the
`200`/no-`Location` exception to `api-conventions.md` AC-4 is well-reasoned,
not an oversight.

## 9. Findings

| id | Severity | Area | Finding | Required correction |
|---|---|---|---|---|
| M-1 | Minor | API / Security | `HttpOnly`/`Secure` cookie attributes stated in the design are not independently mandated by `security-conventions.md` SC-5 (only `SameSite` is). Strengthening, not weakening; non-blocking. | None required now; recommend a future `security-conventions.md` update to state `HttpOnly`/`Secure` explicitly. |
| M-2 | Minor (informational) | API | Cookie name `CustomerPortal.Auth` is a new, unbacked-but-harmless naming choice. | None; may be renamed freely by `IMPLEMENTATION`. |
| M-3 | Minor (carried, unresolved from SPEC_REVIEW v2) | API / Security | FR-11/OD-004 (cookie challenge behavior) has no Acceptance Criterion or test possible within US-002 itself. Correctly self-disclosed (Q-2). | `IMPLEMENTATION`/`SECURITY_REVIEW` to verify by code inspection, not a Story test; US-003 to add the first live test. |

## 10. Limitations

Design review does not execute code, run a build, or invoke MCP tooling; it is
a documentation-consistency and convention-compliance review. `runtime_checks:
NONE`, `semantic_analysis: TEXT_FALLBACK` (read-only comparison against
source `.md`/`.yaml` files).

## 11. Verdict

`PASS` — 0 Critical, 0 Major, 3 Minor (all non-blocking; M-3 carried,
M-1/M-2 new but informational). Advancing to `IMPACT_ANALYSIS`.
