# Global Business Rules

## BR-001

Customer email must be unique.

*Implemented by US-001 (Customer Registration): `UNIQUE` constraint on
`customer.email`, rejected with `409` on the `POST /api/v1/customers` path.*

---

## BR-002

Customer email comparison is case-insensitive.

*Implemented by US-001: email normalized to lowercase before persistence
(see `persistence-conventions.md` PC-4/PC-5).*

---

## BR-003

A customer may own only one account.

---

## BR-004

Customer account cannot be authenticated if disabled.

*Not yet exercised: no authentication endpoint exists until US-002 (Customer
Login).*

---

## BR-005

Passwords must never be stored in plain text.

*Implemented by US-001: BCrypt hash stored in `password_hash`; see
`security-conventions.md` SC-1.*

---

## BR-006

Default role after registration is CUSTOMER.

*Implemented by US-001: registration always assigns role `CUSTOMER`.*

---

## BR-007

System timestamps are stored in UTC.

*Implemented by US-001: `created_at`/`updated_at` populated in UTC via
`AppDbContext.SaveChangesAsync` (see `persistence-conventions.md` PC-6).*