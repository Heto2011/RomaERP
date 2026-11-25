# First real customer: what to check together

Done by tests and screens already (nothing to do): invoices, payments, quotations, purchase orders, payroll, bank file upload, assistant, support tickets, install-the-app button.

## Needs a real device or a real person (cannot be proven from here)
| What | How to check | Who |
|---|---|---|
| Receipt printing | Print a POS receipt and a kitchen ticket on the customer's real printer (58/80 mm) | us + customer |
| QR menu | Scan a table QR from a phone on mobile data, order, see it reach the cashier queue | us |
| Chart of accounts upload | Upload the customer's own Excel, review the preview, keep or replace | customer's accountant |
| Real assistant questions | Ask 4 questions with the real Claude key: customers owe us, add a supplier, change a phone, attach a supplier invoice photo | us |
| Payment of the subscription | Pay the first month by InstaPay (Egypt) and confirm it in the console | us |
| Phone install | "Install app" on Android, Add to Home Screen on iPhone | customer |
| Support loop | Customer sends a ticket → email reaches support@romagroup.app → reply in the console → the 💬 icon shows for the customer | us |

## Ask the customer for (only when they want the feature)
- E-invoicing: ETA or ZATCA credentials.
- Online store, WhatsApp, bank feed: their own account details (see `integrations-readiness.md`).
- Anything that costs money is an optional paid add-on, never switched on without them saying yes.

## Before the call
- Make a demo company with sample data (customers, items, one invoice) so we never demo on their data.
- Have `docs/support-replies.md` open for the usual questions.
