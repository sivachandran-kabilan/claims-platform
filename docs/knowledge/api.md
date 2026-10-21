# API

Base: `/api/v1`. Errors: RFC 7807. State-changing calls need `If-Match`. POST create needs `Idempotency-Key`.

| Method | Route | Roles | Request | Success | Errors |
|---|---|---|---|---|---|
| POST | /auth/register | anonymous | email, password, name | 201 | 400, 409 |
| POST | /auth/login | anonymous | email, password | 200 token | 401, 429 |
| GET | /auth/me | any | n/a | 200 | 401 |
<!-- Add one row per endpoint as it is built. Must match the role matrix in CLAUDE.md. -->
