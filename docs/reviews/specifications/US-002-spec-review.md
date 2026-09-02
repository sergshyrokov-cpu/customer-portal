---
artifact_type: specification_review
story: US-002
version: 2
status: APPROVED
created_at: 2026-09-02T13:32:48Z
updated_at: 2026-09-02T13:43:16Z
produced_by: spec-verifier
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/decisions/US-002-open-decisions.md
    version: 1
supersedes: docs/reviews/specifications/US-002-spec-review.md v1
---

# Specification Review — US-002 Customer Login (v2)

## 1. Summary

Re-reviewed `docs/specifications/US-002-spec.md` v2 against v1's Major finding
F-1, then re-ran the full checklist against
`docs/stories/US-002-customer-login.md`, `docs/decisions/US-002-open-decisions.md`
v1, `business-rules.md`, `business-glossary.md`, `non-functional-requirements.md`,
and `AGENTS.md`. **F-1 is confirmed resolved.** No new Critical/Major finding.
**Verdict: PASS.**

## 2. Reviewed Artifacts

| Artifact | Version |
|---|---|
| docs/specifications/US-002-spec.md | 2 |
| docs/decisions/US-002-open-decisions.md | 1 |
| docs/evidence/US-002-clarification-report.md | 1 |
| docs/stories/US-002-customer-login.md | n/a (no mutable version) |

## 3. F-1 resolution check (independent re-verification)

Re-read every location v1 flagged plus every other mention of `FR-7`/`OD-003`
in v2 (§1, §3 step 7, §4 FR-4/FR-5/FR-6/FR-7/FR-16, §5 AC-002/AC-003/AC-004,
§6.1/§6.2, §7 SEC-4, §8, §11 OD-003 row, §12.1). Confirmed, not merely
asserted by the revision note:

- FR-7 is now scoped to only the unknown-email/wrong-password case (FR-4/FR-5)
  and is explicitly marked "fixed by SC-3 ... not subject to OD-003."
- New FR-16 is the sole place that cites OD-003 for the disabled-account
  case, and is explicitly marked as the one genuinely open rule.
- §8's error table is split into a fixed `401` row (FR-7) and a separate
  draft `401`-per-OD-003 row (FR-6/FR-16) that clearly states the OD-003
  option-B alternative (`403`).
- §11's OD-003 row now lists `FR-6, FR-16, AC-004` (not `FR-7`/`AC-003`) and
  explicitly states "FR-7 itself ... is unaffected either way."
- §12.1 traceability: AC-004 now cites `FR-6, FR-16`; AC-002/AC-003 still
  correctly cite `FR-7` (unaffected, correctly unchanged).
- SEC-4 (§7) was already correctly scoped in v1 and remains unchanged and
  consistent with the v2 fix.

No remaining instance conflates the fixed and open rules. **F-1: RESOLVED.**

## 4. Completeness

Unchanged from v1's assessment plus the new FR-16: business goal, business
flow, 16 functional requirements, 7 Acceptance Criteria, validation rules,
11 security requirements, error handling (now 5 rows), NFRs, out-of-scope,
Open Decisions with impact, and traceability are all present and internally
consistent. Complete.

## 5. Consistency

Re-confirmed against current file content (not assumed from the v1 review):
`business-rules.md` BR-002/BR-004, `security-conventions.md` SC-1/SC-2/SC-3/
SC-4, and `api-conventions.md` AC-1/AC-3/AC-4/AC-6/AC-9 are all cited
accurately. Story AC-001..AC-005 still map 1:1 to Specification AC-001..AC-005
(§12.2), unchanged by the correction pass.

## 6. Traceability

§12.1 re-derived independently: AC-001→FR-1,4,5,6,8,9; AC-002→FR-5,7;
AC-003→FR-4,7; AC-004→FR-6,16; AC-005→FR-10; AC-006→FR-2; AC-007→FR-3. No
gap. FR-16's introduction did not orphan or duplicate any other requirement.

## 7. Security

Re-confirmed: authentication, credential-handling, and anti-enumeration
requirements are all cited to `security-conventions.md` or an Open Decision
— none invented. The F-1 fix directly improves this section's precision
(SEC-4's existing wording is now backed by an unambiguous FR-7/FR-16 split
rather than a single conflated FR-7).

## 8. Open Decisions

All 7 Open Decisions (OD-001 through OD-007) still appear in §11 with impact,
now with OD-003's row corrected to reference FR-16 instead of FR-7. The
OD-001/OD-002 tension (M-3, informational) remains correctly self-flagged.

## 9. Findings

| id | Severity | Finding |
|---|---|---|
| M-1 | Minor (carried from v1, unresolved) | FR-11 (cookie-auth challenge behavior, OD-004) still has no Acceptance Criterion or test in this Story — accurately self-disclosed. Flag to `test-writer`/`implementation-verifier` as configuration-verified-by-code-review, not test-verified. |
| M-2 | Minor (carried from v1, unresolved) | The term "session" remains undefined in `docs/product/business-glossary.md`. Non-blocking; outside `spec-writer`'s owned artifact to fix. |
| M-3 | Minor/informational (carried from v1) | The OD-001 (201-implying) vs. OD-002 (200-recommending) tension remains correctly self-flagged in §11 for joint resolution at `HUMAN_SPEC_APPROVAL`. |

No new finding introduced by the v2 correction pass.

## 10. Verdict

`PASS` — 0 Critical, 0 Major, 3 Minor (all carried, non-blocking). The
Specification is implementation-ready pending `HUMAN_SPEC_APPROVAL`'s
resolution of the 7 Open Decisions. Advancing to `HUMAN_SPEC_APPROVAL`.
