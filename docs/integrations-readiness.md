# External integrations: what is ready, what we need from the customer

Rule for all of them: the connection is **off until the customer gives us the credential**, anything that costs money is an **optional paid add-on**, and what comes in lands in the **Inbox for approval** (nothing reaches the books by itself).

## What already exists
- **Integration API + Inbox** (`docs/integration-api.md`): any outside program (a store connector, a bot) can send customers, items, invoices, payments, bank lines. A person approves in *Administration → Bot connection*. This is the door every store connector goes through.
- **E-invoicing code** for Egypt (ETA) and Saudi Arabia (ZATCA): built, switched off until the customer gives the certificate and keys.

## Online stores (Salla, Zid, Shopify, WooCommerce)
| | |
|---|---|
| What we do once (us) | Register an app with each platform (partner account, free). Needed for the "Connect" button to work with one click. |
| What the customer does | Press "Connect" and approve. Or, without our app: create an API key in their store and give it to us. |
| What flows in | Orders → sales invoices (draft in Inbox), customers, products/prices, stock levels. |
| Cost | Platforms charge nothing for the API. Optional paid add-on if we decide to charge for it. |
| Status | Not built. Built only when a real customer has a store, so it can be tested against their real store. |

## WhatsApp
| | |
|---|---|
| What is needed | A Meta Business account and a verified phone number for the customer (or us as an official provider). |
| Who pays | Meta bills per message, by type (marketing, utility, authentication; replies inside a customer-started 24 hour window are normally free). Rates differ per country and change; confirm on Meta's current rate card before quoting. |
| Third-party figures seen | Marketing about $0.05–0.074 per message for Egypt/Saudi (SleekFlow, ManyChat tables). Not Meta's own number, so treat as a rough guide only. |
| Formula | monthly cost = messages per month × rate for that message type. Example: 500 marketing messages × $0.07 ≈ $35. |
| Rule | Customer's own number and billing by default. If we carry the cost it is a priced add-on, never hidden in the plan. |

## Bank statements
| | |
|---|---|
| Today | Upload the statement file (Excel/CSV) and match against the books (to build). |
| Direct bank link | Each bank gives access only to the account owner, usually after an agreement or through a paid aggregator. Per customer, after they ask for it. |

## E-invoicing
| | Egypt (ETA) | Saudi Arabia (ZATCA) |
|---|---|---|
| Customer gives us | ETA portal registration, client id/secret, signing certificate (token) | ZATCA onboarding OTP, then CSID certificates |
| Our side | Code is built | Code is built; onboarding wizard exists |
| Test | Only possible with their sandbox credentials | Only possible with their sandbox OTP |
