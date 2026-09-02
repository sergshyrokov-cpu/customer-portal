---
artifact_type: clarification_report
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-02T13:26:41Z
updated_at: 2026-09-02T13:26:41Z
produced_by: us-clarifier
inputs:
  - path: docs/stories/US-002-customer-login.md
    version: null
supersedes: null
---

# Clarification Report — US-002 Customer Login

## 1. Scope understanding

US-002 delivers customer authentication: a registered, enabled Customer
(created by US-001) can log in with email + password and receive an
authenticated session; login fails cleanly for wrong password, unknown
account, and disabled account, without ever returning credential material.
This is the first Story that gives the project a working authentication
mechanism — `security-conventions.md` SC-3 (cookie authentication) has been an
approved-but-unexercised convention since project inception; US-002 is where
it is actually wired into `Program.cs` and exercised end to end for the first
time.

**Actor:** Customer (existing account, created via US-001's registration
endpoint).

**Business value:** Lets a previously-registered customer securely access
protected functionality — the stated precondition for every future Story that
requires an authenticated caller (starting with US-003 Profile View).

**Out of scope** (per the Story and `product-vision.md`): MFA, OAuth, social
login, password recovery — consistent with the project's MVP scope. Logout is
not listed either way; see OD-007.

## 2. Acceptance Criteria coverage check

| AC | Covered by an unambiguous rule today? | Gap |
|---|---|---|
| AC-001 Successful Login | Partially — "valid credentials" defined by BR-001/BR-002/BR-005, but response shape is undecided | OD-001, OD-002 |
| AC-002 Invalid Password | Yes for the failure itself; response shape undecided | OD-001 (path), consistency with AC-003 must be made explicit (§3) |
| AC-003 Unknown Account | Yes for the failure itself; response shape undecided | Same as AC-002 |
| AC-004 Disabled Account | BR-004 fixes *that* it must fail; *how* (message/status vs. AC-002/003) is undecided | OD-003 |
| AC-005 Secure Authentication Response | Directionally clear (no credential leakage), but the positive field list is undefined | OD-002 |

## 3. Ambiguities and contradictions found

1. **AC-002/AC-003 response uniformity is implied, not stated.**
   `security-conventions.md` SC-3 already commits to "does not reveal whether
   the email exists ... unless a Story's approved design explicitly allows
   it," which means AC-002 (wrong password) and AC-003 (unknown account) must
   produce an *identical* response by the existing architectural default. The
   Story text does not say this explicitly — `spec-writer` must state it as an
   explicit requirement (single shared error message/status for both cases),
   not leave it as an inference from a different document. This is **not** a
   new Open Decision — SC-3 already resolves it — but it is a specification
   gap that must be closed in prose.
2. **Whether AC-004 (disabled account) joins that same uniform response is
   genuinely undecided** — see OD-003.
3. **"Authentication succeeds" (AC-001) does not say what a caller receives**
   — no response shape, no status code, no indication of whether a resource
   (a "session") is modeled at all. See OD-001, OD-002.
4. **The endpoint's path/method is unconstrained by the Story** and directly
   collides with `api-conventions.md` AC-3's "no verbs in paths, non-CRUD
   actions need an approved design decision" — login is exactly such a
   non-CRUD action. See OD-001.
5. **No cookie lifetime is specified anywhere in the project's docs.** This
   Story is where a lifetime must first be chosen, since it is the first to
   actually issue the authentication cookie. See OD-006.
6. **Logout is silent** — neither an Acceptance Criterion nor an explicit
   Out-of-Scope entry. See OD-007.
7. **Carried forward from US-001's delivery** (not a new ambiguity in this
   Story's text, but directly relevant to it): the ASP.NET Core cookie scheme
   that this Story registers for the first time has a default challenge
   behavior (`302` redirect) that does not match `security-conventions.md`
   SC-3 / `api-conventions.md` AC-5's `401` commitment. See OD-004.

## 4. Missing validation / security expectations already resolved by existing convention (no new Open Decision needed)

- Password hashing/verification: `security-conventions.md` SC-1 (BCrypt,
  `IPasswordHasher`) — reuse the same abstraction US-001 introduced; no new
  hasher.
- Deny-by-default authorization (SC-4): the login endpoint itself is public
  (`[AllowAnonymous]`), same pattern as US-001's registration endpoint.
- Error body shape: `api-conventions.md` AC-6 — reuse verbatim, including for
  the `401` login-failure cases.
- Media type / content-type handling: AC-2 — same `415` handling as US-001.

## 5. Specification checklist (what `spec-writer` must cover)

- [ ] Resolve and state the login endpoint's path, method, and success status
  code (OD-001).
- [ ] Define the exact successful-login response body fields (OD-002).
- [ ] State explicitly that AC-002 and AC-003 return an identical response
  (§3.1) and record the AC-004 decision once made (OD-003).
- [ ] Define the cookie-authentication challenge behavior for any future
  protected endpoint that this Story's `AddCookie()` registration will serve
  (OD-004) — architecturally relevant now even though no AC in this Story
  exercises it directly.
- [ ] State the anti-abuse/rate-limiting scope decision (OD-005).
- [ ] State the session/cookie lifetime (OD-006).
- [ ] State whether logout is in or out of scope (OD-007), and if in scope,
  define its endpoint per the same shape as the login decision.
- [ ] Confirm BR-003 ("a customer may own only one account") is not affected
  by login (it is a registration-time rule; login only reads an existing
  account) — no new rule needed, but `spec-writer` should not accidentally
  reintroduce a uniqueness check here.

## 6. Open Decisions reference

Seven Open Decisions recorded in
`docs/decisions/US-002-open-decisions.md` (OD-001 through OD-007), all
`status: OPEN`, to be resolved at `HUMAN_SPEC_APPROVAL`.

## 7. Assumptions (not Open Decisions — reasonable defaults, flagged for visibility)

- The Customer entity, `Customers` repository, and `IPasswordHasher`
  abstraction from US-001 are reused as-is; no schema change is anticipated
  for login itself (login only reads `Customer`, it does not write it) unless
  a chosen Open Decision option requires one (e.g. a failed-attempt counter,
  which would only arise if OD-005 is resolved as "in scope").
- "Enabled" account state already exists as a concept per US-001's design
  (`security-conventions.md` SC-2: "default account state on registration:
  enabled") — AC-004 assumes this field already exists and can be toggled by
  some means; this Story does not need to add an admin "disable account"
  capability (out of scope, no AC requests it), only to *check* the flag.
