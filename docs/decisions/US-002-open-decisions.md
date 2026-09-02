---
artifact_type: open_decisions
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

# Open Decisions — US-002 Customer Login

Each entry is a decision a human must make. Status `OPEN` entries are resolved at
`HUMAN_SPEC_APPROVAL`, not during CLARIFICATION. `recommended` values are
non-binding.

---

## OD-001 — Login endpoint shape and path

- **id:** OD-001
- **question:** What is the exact path, HTTP method, and success status code
  for the login operation?
- **context:** `api-conventions.md` AC-3 forbids verbs in paths and requires an
  approved design decision for any action that is not CRUD. "Login" is not a
  CRUD operation on the `customers` collection, so neither
  `POST /api/v1/customers/login` (verb in path) nor an unstated ad-hoc path is
  automatically correct.
- **affects:** SPECIFICATION, API_DESIGN
- **status:** OPEN
- **options:**
  - A. Model authentication as a resource: `POST /api/v1/sessions`,
    `201 Created` on success (a session/cookie is being created), no verb in
    the path.
  - B. `POST /api/v1/auth/login` as a recognized, approved exception to AC-3
    (a small, explicit "auth" sub-resource area, common REST practice).
  - C. `POST /api/v1/customers/login` (verb in path; would require an explicit
    AC-3 exception).
- **recommended:** A — cleanest fit with AC-3's "no verbs" rule and pairs
  naturally with a future logout as `DELETE /api/v1/sessions` (see OD-007).

---

## OD-002 — Successful login response: status code and body fields

- **id:** OD-002
- **question:** What HTTP status code and which fields does the successful
  login response return?
- **context:** AC-005 requires that sensitive credential information is never
  returned, but does not state the positive field list or status code. Depends
  on OD-001 (if login is modeled as creating a session resource, `201` with a
  `Location` header would follow `api-conventions.md` AC-4; if login is
  treated as a non-resource-creating action, `200 OK` is more natural).
- **affects:** SPECIFICATION, API_DESIGN, TEST_WRITING
- **status:** OPEN
- **options:**
  - A. `200 OK` with `{ id, email, role }` (mirrors the customer fields from
    US-001's registration response, minus `createdAt`).
  - B. `201 Created` with a `Location` header (if OD-001:A is chosen) and the
    same body as A.
  - C. `200 OK` with no body (cookie alone carries the session).
- **recommended:** A — simplest, consistent with AC-005, gives the client
  enough to confirm identity/role without over-committing to the session-as-
  resource modeling of OD-001:A.

---

## OD-003 — Disabled-account response vs. account-state disclosure

- **id:** OD-003
- **question:** Does a login attempt against a **disabled** account (AC-004)
  return the same generic failure as an unknown account (AC-003) / wrong
  password (AC-002), or a distinct response that reveals the account exists
  but is disabled?
- **context:** BR-004 requires a disabled account not be authenticated, but
  the Story does not say whether that failure may be distinguishable from
  "wrong credentials." `security-conventions.md` SC-3 already forbids revealing
  *whether an email exists* on plain credential failure; this decision extends
  (or does not extend) that same anti-enumeration principle to account state.
- **affects:** SPECIFICATION, API_DESIGN, SECURITY_REVIEW, TEST_WRITING
- **status:** OPEN
- **options:**
  - A. Identical generic `401` for AC-002/AC-003/AC-004 — strongest
    anti-enumeration, but a legitimately disabled customer gets no actionable
    error message.
  - B. Distinct message for a disabled account (e.g. `403 Forbidden` /
    "Account is disabled") — better UX, but confirms the account exists and
    its state to anyone probing with an email address.
- **recommended:** A — consistent with SC-3's existing anti-enumeration
  default and with US-001 OD-003's resolution (enumeration risk accepted as
  low for this training project, but the *direction* chosen there was to
  disclose less, not more, once a security-sensitive default already exists).

---

## OD-004 — Cookie-authentication challenge behavior (401 vs. redirect)

- **id:** OD-004
- **question:** When `AddAuthentication().AddCookie(...)` is registered by
  this Story (the first Story to wire real cookie authentication), how should
  an unauthenticated/forbidden access to a **protected** endpoint respond —
  ASP.NET Core's default `302` redirect-to-login-path challenge, or the `401`/
  `403` that `security-conventions.md` SC-3/`api-conventions.md` AC-5 commit
  to?
- **context:** Flagged as a known gap during US-001's security review and
  delivery (`docs/evidence/US-001-delivery-summary.md`): this project has no
  server-rendered login page, only a JSON API, so the cookie scheme's default
  browser-oriented redirect challenge is wrong for this API but was never
  exercised because no protected endpoint existed yet. US-002 is the first
  Story to register the cookie scheme; US-003 (Profile View) will be the
  first to add an `[Authorize]` endpoint that actually exercises this path.
- **affects:** API_DESIGN, IMPLEMENTATION, SECURITY_REVIEW (this Story and
  US-003)
- **status:** OPEN
- **options:**
  - A. Override `CookieAuthenticationEvents.OnRedirectToLogin` /
    `OnRedirectToAccessDenied` to short-circuit with `401` / `403` and no
    redirect, for every request (this is a JSON API only, no browser login
    page exists or is planned).
  - B. Condition the override on an `Accept: application/json` /
    `X-Requested-With` header, falling back to the framework default
    otherwise.
  - C. Leave the ASP.NET Core default (`302`) and accept the deviation from
    SC-3/AC-5 as a documented, approved exception.
- **recommended:** A — there is no browser login page in this project's scope
  (Out of Scope explicitly excludes it), so an unconditional `401`/`403`
  override is simpler and has no legitimate redirect case to preserve.

---

## OD-005 — Anti-abuse / brute-force protection on login (scope check)

- **id:** OD-005
- **question:** Does US-002 include any rate limiting, account lockout, or
  other brute-force protection on repeated failed login attempts, or is that
  explicitly deferred?
- **context:** Mirrors US-001 OD-005. Neither the NFRs nor the Story's
  Acceptance Criteria mention brute-force protection; recorded so the decision
  is explicit for `security-reviewer`, especially since a login endpoint is a
  more attractive brute-force target than registration.
- **affects:** SPECIFICATION, SECURITY_REVIEW
- **status:** OPEN
- **options:**
  - A. Out of scope for US-002; note as a follow-up Story.
  - B. In scope; add a basic per-account or per-IP failed-attempt lockout/
    delay.
- **recommended:** A — consistent with US-001 OD-005:A's resolution; no NFR or
  Acceptance Criterion requires it for this Story.

---

## OD-006 — Session/cookie lifetime and expiration policy

- **id:** OD-006
- **question:** What is the authentication cookie's expiration policy —
  sliding or absolute expiration, and what duration?
- **context:** Neither the Story, the NFRs, nor `security-conventions.md`
  currently state a cookie lifetime. ASP.NET Core's cookie-authentication
  default (`ExpireTimeSpan` = 14 days, sliding) is a long-lived session for a
  customer-facing portal and has not been reviewed as a project decision.
- **affects:** SPECIFICATION, API_DESIGN, SECURITY_REVIEW
- **status:** OPEN
- **options:**
  - A. Explicit shorter sliding window (e.g. 30–60 minutes idle timeout) with
    an explicit absolute cap (e.g. 8–12 hours), values recorded in
    `security-conventions.md`.
  - B. Accept the ASP.NET Core 14-day sliding default explicitly (documented
    as a deliberate training-project choice, not an oversight).
- **recommended:** A — a customer-facing authentication cookie left at the
  framework's 14-day default is a real (if modest, for a training project)
  session-fixation/exposure-window risk; an explicit, shorter, documented
  value is safer and no more work to implement.

---

## OD-007 — Logout endpoint inclusion

- **id:** OD-007
- **question:** Does US-002 include a logout (session-termination) endpoint,
  or is logout explicitly deferred to a follow-up Story?
- **context:** The Story's Acceptance Criteria (AC-001..AC-005) only cover
  login; "Out of Scope" lists MFA/OAuth/Social Login/Password Recovery but
  does not mention logout either way. Without a logout endpoint, a customer
  has no way to explicitly end a session before cookie expiration (see
  OD-006), which is a usability/security gap worth deciding explicitly rather
  than leaving implicit.
- **affects:** SPECIFICATION, API_DESIGN
- **status:** OPEN
- **options:**
  - A. Out of scope for US-002; the Story's ACs define login only, logout is a
    separate follow-up Story.
  - B. In scope alongside login (e.g. `DELETE /api/v1/sessions` if OD-001:A is
    chosen) as the natural pair to creating a session.
- **recommended:** A — strict reading of the Acceptance Criteria; logout has
  no AC in this Story and adding it would be scope expansion beyond what was
  requested, however B is a reasonable, low-cost alternative if the human
  prefers not to leave customers without a sign-out path for however long
  US-003/a follow-up Story takes.
