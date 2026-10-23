import { useEffect, useState } from "react";
import { AccountsApi, RecurringExpensesApi } from "../../api/services";
import { AccountType, ExpensePaymentMethod, type Account, type RecurringExpense } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { useAuth } from "../../context/AuthContext";

export default function RecurringExpenses() {
  const { t, lang } = useLanguage();
  const { user } = useAuth();
  const isAdmin = !!user?.roles.includes("Admin");
  const [items, setItems] = useState<RecurringExpense[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<RecurringExpense | "new" | null>(null);
  const [form, setForm] = useState({ description: "", accountId: "", amount: "", dayOfMonth: 1, paymentMethod: ExpensePaymentMethod.Cash });

  async function load() {
    setItems((await RecurringExpensesApi.getAll()).data);
  }

  useEffect(() => {
    load().catch((e) => setError(getErrorMessage(e)));
    AccountsApi.getAll().then((r) => setAccounts(r.data.filter((a) => a.accountType === AccountType.Expense && !a.isControlAccount))).catch(() => undefined);
  }, []);

  function open(item: RecurringExpense | "new") {
    setError(null);
    setEditing(item);
    setForm(item === "new"
      ? { description: "", accountId: "", amount: "", dayOfMonth: 1, paymentMethod: ExpensePaymentMethod.Cash }
      : { description: item.description, accountId: item.accountId, amount: item.amount != null ? String(item.amount) : "", dayOfMonth: item.dayOfMonth, paymentMethod: item.paymentMethod });
  }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const body = {
      description: form.description,
      accountId: form.accountId,
      amount: form.amount ? Number(form.amount) : null,
      dayOfMonth: form.dayOfMonth,
      paymentMethod: form.paymentMethod,
      isActive: editing !== "new" && editing ? editing.isActive : true,
    };
    try {
      if (editing === "new") await RecurringExpensesApi.create(body);
      else if (editing) await RecurringExpensesApi.update(editing.id, body);
      setEditing(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function run(action: () => Promise<unknown>) {
    setError(null);
    try {
      await action();
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.assistant.recurringTitle}</h1>
        {isAdmin && <button className="btn" onClick={() => open("new")}>{t.assistant.recurringNewItem}</button>}
      </div>
      <p className="text-muted">{t.assistant.recurringIntro}</p>
      {error && <div className="alert-error">{error}</div>}

      <div className="card" style={{ overflowX: "auto" }}>
        {items.length === 0 ? (
          <div className="text-muted">{t.assistant.recurringEmpty}</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>{t.assistant.itemName}</th>
                <th>{t.assistant.suggestedAccount}</th>
                <th>{t.common.amount}</th>
                <th>{t.assistant.everyMonthOnDay}</th>
                <th>{t.assistant.lastPrepared}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((i) => (
                <tr key={i.id} style={i.isActive ? undefined : { opacity: 0.55 }}>
                  <td>{i.description}{!i.isActive && <span className="badge" style={{ marginInlineStart: 8 }}>{t.assistant.recurringPaused}</span>}</td>
                  <td>{i.accountCode} - {i.accountName}</td>
                  <td>{i.amount != null ? `${i.amount.toLocaleString()} ${i.currency}` : <span className="text-muted">{t.assistant.amountVaries}</span>}</td>
                  <td>{i.dayOfMonth}</td>
                  <td className="text-muted">{i.lastPreparedMonth ? new Date(i.lastPreparedMonth).toLocaleDateString(lang === "ar" ? "ar-EG" : "en-GB", { month: "long", year: "numeric" }) : "—"}</td>
                  <td style={{ whiteSpace: "nowrap" }}>
                    {isAdmin && (
                      <>
                        <button className="btn btn-secondary btn-sm" onClick={() => open(i)}>{t.common.edit}</button>{" "}
                        <button className="btn btn-secondary btn-sm" onClick={() => run(() => RecurringExpensesApi.setActive(i.id, !i.isActive))}>
                          {i.isActive ? t.assistant.pauseItem : t.assistant.resumeItem}
                        </button>{" "}
                        <button className="btn btn-secondary btn-sm" onClick={() => confirm(t.assistant.recurringDeleteConfirm) && run(() => RecurringExpensesApi.remove(i.id))}>
                          {t.common.delete}
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editing && (
        <div className="modal-overlay" onClick={() => setEditing(null)}>
          <div className="card" style={{ maxWidth: 520, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ marginTop: 0 }}>{editing === "new" ? t.assistant.recurringNewItem : t.common.edit}</h3>
            <form onSubmit={save}>
              <div className="form-field">
                <label>{t.assistant.itemName}</label>
                <input value={form.description} maxLength={200} onChange={(e) => setForm({ ...form, description: e.target.value })} required />
              </div>
              <div className="form-field">
                <label>{t.assistant.suggestedAccount}</label>
                <select value={form.accountId} onChange={(e) => setForm({ ...form, accountId: e.target.value })} required>
                  <option value="">-</option>
                  {accounts.map((a) => <option key={a.id} value={a.id}>{a.code} - {lang === "ar" ? a.nameAr : a.nameEn || a.nameAr}</option>)}
                </select>
              </div>
              <div className="form-grid">
                <div className="form-field">
                  <label>{t.common.amount} <span className="text-muted">({t.assistant.amountVaries})</span></label>
                  <input type="number" min={0} step="0.01" value={form.amount} placeholder={t.assistant.amountVaries} onChange={(e) => setForm({ ...form, amount: e.target.value })} />
                </div>
                <div className="form-field">
                  <label>{t.assistant.dayOfMonth}</label>
                  <input type="number" min={1} max={28} value={form.dayOfMonth} onChange={(e) => setForm({ ...form, dayOfMonth: Number(e.target.value) })} required />
                </div>
                <div className="form-field">
                  <label>{t.assistant.paymentMethod}</label>
                  <select value={form.paymentMethod} onChange={(e) => setForm({ ...form, paymentMethod: Number(e.target.value) as ExpensePaymentMethod })}>
                    <option value={ExpensePaymentMethod.Cash}>{t.assistant.paymentMethods.cash}</option>
                    <option value={ExpensePaymentMethod.Card}>{t.assistant.paymentMethods.card}</option>
                  </select>
                </div>
              </div>
              <p className="text-muted" style={{ fontSize: 13 }}>{t.assistant.recurringHowTo}</p>
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit">{t.common.save}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setEditing(null)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
