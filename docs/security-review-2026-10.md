# Security review — 5 Oct 2026 (code review, not a penetration test)

Scope: the API, multi-tenant isolation, authentication, uploads, secrets, deploy/nginx, public repo exposure.

## Checked and fine
- **Tenant isolation:** each company has its own database; every request must carry `X-Company-Code`, and the JWT's `company_code` claim must match it (`TenantClaimConsistencyMiddleware`) or the request is refused.
- **Authorization:** every controller is `[Authorize]` except the intended public ones (login, trial signup, marketing page-view, delivery webhooks); platform-owner endpoints need the `X-System-Key` (constant-time compare, rate-limited).
- **No raw SQL** (no `FromSqlRaw`/`ExecuteSqlRaw`) — EF parameterises everything.
- **Passwords/lockout:** Identity hashing, lockout on failed logins, per-IP login/recovery rate limits, generic error messages (no account enumeration), reset links valid 2 h.
- **Webhooks:** HMAC signature, constant-time comparison.
- **Secrets:** none in the repo (scanned); real values come from GitHub secrets at deploy; deploy logs never print values.
- **Swagger** only in Development; HTTPS with Let's Encrypt; `X-Forwarded-For` is set by nginx from the real peer address (cannot be spoofed by clients).

## Fixed in this change
| # | Finding | Fix |
|---|---|---|
| 1 | A 4-digit POS PIN could open an **Admin** session; limit was per IP only | PIN login never signs in an Admin; extra cap of 20 attempts/min **per company** |
| 2 | Server could run with the public placeholder JWT key (forgeable tokens) if the secret was missing | API refuses to start with the placeholder; deploy stops **before** restarting |
| 3 | Expense-proof upload stored the client's file extension, and wrote the file before checking the record existed | fixed list of extensions (images/PDF), PDF signature check, record checked first |
| 4 | Face-photo upload wrote the file before checking the employee existed in this company, and trusted the `Content-Type` | employee checked first, real JPEG signature required |
| 5 | No browser security headers | HSTS, nosniff, frame protection, referrer policy, permissions policy (location + camera allowed for attendance), `server_tokens off` |
| 6 | Nothing watched for the database/API ports being open to the internet | health check (every 6 h) fails and emails if ports 1433, 5000 or 14330 answer from outside |
| 7 | Every SQL statement was written to the server log at Information level | EF logging set to Warning |

## Still open (need a decision or an owner action)
- **DigitalOcean firewall:** confirm only ports 22, 80, 443 are public (the new health check will alert if 1433/5000 are open). Prefer restricting 22 to your own IP or key-only login.
- **`JWT_SIGNING_KEY` GitHub secret:** must be set (the deploy now stops if it is not).
- **Logged-in sessions last 2 hours and are not revoked** when an account is deactivated or a password is reset; a deactivated user's token keeps working until it expires. Option: re-check `IsActive` on each request (small DB read).
- **No Content-Security-Policy** yet (the site uses inline scripts and Google Fonts; adding CSP needs testing page by page).
- **Face reference photos / contract PDFs** live on the server disk (not in the database); contracts are stored per company, face photos by employee id only (ids are unguessable GUIDs). Backups include them.
- **Penetration test:** none done. Before ~20 customers, an outside tester (or a bug-bounty style check) is worth the cost.
- **Dependencies:** `npm audit` reports 1 high-severity advisory in the frontend build tooling — review (`npm audit`) and update.
