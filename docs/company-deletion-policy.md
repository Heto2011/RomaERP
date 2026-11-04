# Company deletion policy

- **Every company created before 2026-11-04 03:00 UTC was a test/demo company.** The system console
  (`/system/subscriptions` → *Delete forever*, needs the System Key) may erase it completely, even if its
  test invoices were marked paid.
- **Every company created at or after that moment is a real customer.** *Delete forever* refuses it.
  It can only be removed after an **official e-mail from that company's Admin to support@romagroup.app**
  asking to delete the company and all its data. The owner then processes it from
  `/system/data-deletion`, which records the requester's e-mail, who processed it, the reason and a
  confirmation number (kept as durable proof).
- The cut-off lives in `TenantProvisioningService.LegacyTestCleanupCutoffUtc`.
