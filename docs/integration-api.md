# Roma ERP — Integration API (for bots and outside systems)

Everything an outside program sends lands in the company's **inbox** and waits for a person to approve it in
**Administration → Bot connection**. Nothing reaches the books before approval.

## 1. Connect

| | |
|---|---|
| Base URL | `https://romagroup.app/api/integration` |
| Header `X-Company-Code` | the company's code (shown on the Bot connection page) |
| Header `X-Api-Key` | a key created on the Bot connection page (`rk_...`). Shown once, can be revoked. |
| Body | JSON, UTF-8 |
| Limit | 120 requests per minute per IP; up to 100 items per batch call |

`GET /ping` returns `{"ok":true}` when the company code and key are right (401 otherwise).

## 2. Send an item

`POST /items`

```json
{
  "type": "SalesInvoice",
  "externalRef": "bot-inv-2041",
  "data": { }
}
```

* `type`: `Customer`, `Vendor`, `Item`, `SalesInvoice`, `PurchaseInvoice`, `SalesPayment`, `PurchasePayment`, `Expense`, `JournalEntry`, `BankStatementLine`, `Data`
* `externalRef`: **your own unique id** for the item (up to 100 characters). Sending the same `type` + `externalRef` again never
  creates a second item: you get the first one back with `"duplicate": true` (HTTP 200). New items answer HTTP 201.
* Answer: `{ "id": "...", "status": "Pending", "duplicate": false }`. A malformed item answers HTTP 400 `{ "error": "..." }`.

`POST /items/batch` takes `{ "items": [ {type, externalRef, data}, ... ] }` (1 to 100). Each item succeeds or fails on its own;
the answer lists them in order: `{ accepted, rejected, results: [{ externalRef, type, id, status, duplicate, error }] }`.

`GET /items/{id}` tells you what happened to an item: `Pending`, `Approved`, `Rejected` or `Failed`, with `errorMessage`
(why it failed), `reviewNote` and `resultEntityId`.

**No bot yet?** The same items can be loaded from a file: on the Bot connection page choose **Upload a file** and pick a JSON file shaped
`{ "items": [ {type, externalRef, data}, ... ] }` (up to 100). A complete worked example (a real client's sales report and two
supplier invoices) is in `docs/examples/salad-bar-samples.json`.

## 3. What to send in `data`

Numbers may be sent as numbers or strings. Dates are `YYYY-MM-DD`. Customers, vendors, items, warehouses and accounts are named by
**code**, never by id.

### Customer / Vendor
```json
{ "code": "NOOR", "nameAr": "شركة النور", "nameEn": "Al Noor", "phone": "0100...", "email": "a@b.com",
  "taxRegistrationNumber": "123", "address": "...", "city": "...", "paymentTermsDays": 30, "creditLimit": 50000 }
```
`nameAr` or `nameEn` is required. Without `code`, one is made from `externalRef`. If the code already exists, the record is updated.

### Item (stock item)
```json
{ "code": "ITM-1", "nameAr": "قهوة", "nameEn": "Coffee", "unitOfMeasure": "kg", "categoryCode": "RAW", "reorderLevel": 10 }
```
`code` and a name are required. Without `categoryCode` the first active category is used. If the code exists, the item is updated.

### BankStatementLine
```json
{ "accountCode": "1112", "date": "2026-11-12", "description": "Transfer from Al Noor", "amount": 2500 }
```
`amount` is positive for money in, negative for money out. Approving adds the line to the bank feed; matching it to the books
is then done on the reconciliation screen. `externalRef` must be the bank's own id for the line (it stops duplicates).

### SalesInvoice
```json
{ "customerCode": "NOOR", "invoiceDate": "2026-11-15", "paymentTerm": "Credit",
  "currencyCode": "EGP", "notes": "...",
  "lines": [ { "description": "Consulting", "quantity": 2, "unitPrice": 500 } ] }
```
`customerCode` or `customerName` (exact name). `paymentTerm`: `Cash`, `Card`, `Credit` (default), `Installment`
(then also `numberOfInstallments` and `firstInstallmentDueDate`). A line with `itemCode` takes stock out of `warehouseCode`.
Prices are before VAT; the system adds VAT and posts the journal entry on approval.

### PurchaseInvoice
```json
{ "vendorCode": "ELEC", "invoiceDate": "2026-11-15", "paymentTerm": "Credit",
  "lines": [ { "description": "Electricity", "quantity": 1, "unitPrice": 1200, "accountCode": "5300" } ] }
```
`accountCode` is where the cost goes (default 5300 general expenses; 1160 inventory when `itemCode` is given).

### SalesPayment / PurchasePayment
```json
{ "invoiceExternalRef": "bot-inv-2041", "amount": 500, "method": "Cash", "paymentDate": "2026-11-20", "reference": "RCPT-9" }
```
Refers to an invoice **you sent earlier** by its `externalRef`. The invoice must be approved first.

### Expense
```json
{ "date": "2026-11-15", "description": "Fuel", "amount": 100, "expenseAccountCode": "5300", "paidFrom": "cash" }
```
`paidFrom`: `cash` (default), `bank`, `payable`, or give `paidFromAccountCode`. Posts debit expense / credit the paying account.

### JournalEntry
```json
{ "date": "2026-11-30", "description": "Accrual", "reference": "ADJ-1",
  "lines": [ { "accountCode": "5300", "debit": 300 }, { "accountCode": "2120", "credit": 300 } ] }
```
Debits must equal credits; each line has one side only.

### Data
Anything else (documents, notes, statements, lists). Any JSON object; `title` or `category` is used as the heading.
It is stored and reviewed but never posted by itself.

## 4. Order and failures

Approval turns an item into the real record through the same rules as the screens. If the books refuse it (unknown customer,
closed fiscal period, unknown account...) the item becomes `Failed` with the reason, and can be approved again after the cause is fixed.

Send parents first: **Customers and vendors before their invoices; invoices before their payments.** "Approve all" on the
inbox page processes them in that order regardless of arrival order.

## 5. Not posted automatically

Fixed assets, payroll, employees, contracts, documents and anything else: send them as `Data`. They are stored and reviewed on the
inbox page, but a person enters them in the books.
