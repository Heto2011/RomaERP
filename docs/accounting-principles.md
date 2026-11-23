# Accounting principles of RomaERP

Decided by the owner. They apply to every change that touches the books.

1. **The accounting standards govern.** The system follows the internationally accepted standards (IFRS / IAS). They are the governing basis for every
   country; there is no separate accounting basis per country. Only the local rules that differ by country (tax, social
   insurance, payroll) are kept per country (see 3 below). If an instruction conflicts with a standard, the standard wins and the owner is told why.
   Examples already applied: inventory cost per IAS 2 (conversion costs and fixed production overhead on normal capacity; unabsorbed overhead,
   shortages and abnormal waste are expenses of the period; no LIFO; lower of cost and net realisable value).
2. **The base chart of accounts is used as it is.** No new accounts for a feature unless the standard needs one (e.g. 5510, production costs
   capitalised to inventory, the "overhead absorbed" side of IAS 2). A company that wants its own accounts creates them itself.
3. **Country rules stay inside their country.** Each country's tax, social insurance and payroll rules live in their own class
   (`HR/Services/Countries`), never mixed with another country's or with the country-neutral flow.
4. **Anything automatic is approved first** by the owner or the accountant, with the chance to edit it (count differences, payroll deductions
   of employee debts, ...).
5. **A company's own rule that differs from the standard or from this default** is handled only on a formal request from the company by
   official email. Without it, the default applies. The system does not enforce legal limits of labour law (e.g. caps on deductions from
   pay); that is the company's responsibility.
