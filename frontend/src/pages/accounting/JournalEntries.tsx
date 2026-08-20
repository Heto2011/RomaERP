import { useEffect, useState } from "react";
import { AccountsApi, JournalEntriesApi, LookupsApi } from "../../api/services";
import { JournalEntryStatus, type Account, type FiscalPeriod, type JournalEntry } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const statusLabel: Record<JournalEntryStatus, { text: string; cls: string }> = {
  [JournalEntryStatus.Draft]: { text: "مسودة", cls: "badge-draft" },
  [JournalEntryStatus.Posted]: { text: "مرحل", cls: "badge-posted" },
  [JournalEntryStatus.Reversed]: { text: "معكوس", cls: "badge-reversed" },
};

interface LineForm {
  accountId: string;
  debit: string;
  credit: string;
  description: string;
}

function emptyLine(): LineForm {
  return { accountId: "", debit: "", credit: "", description: "" };
}

export default function JournalEntries() {
  const [entries, setEntries] = useState<JournalEntry[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [entryDate, setEntryDate] = useState(new Date().toISOString().slice(0, 10));
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [description, setDescription] = useState("");
  const [lines, setLines] = useState<LineForm[]>([emptyLine(), emptyLine()]);

  async function load() {
    const [entriesRes, accountsRes, periodsRes] = await Promise.all([
      JournalEntriesApi.getAll(),
      AccountsApi.getAll(),
      LookupsApi.fiscalPeriods(),
    ]);
    setEntries(entriesRes.data);
    setAccounts(accountsRes.data.filter((a) => !a.isControlAccount));
    setPeriods(periodsRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function updateLine(index: number, field: keyof LineForm, value: string) {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, [field]: value } : l)));
  }

  function addLine() {
    setLines((prev) => [...prev, emptyLine()]);
  }

  function removeLine(index: number) {
    setLines((prev) => prev.filter((_, i) => i !== index));
  }

  const totalDebit = lines.reduce((sum, l) => sum + (Number(l.debit) || 0), 0);
  const totalCredit = lines.reduce((sum, l) => sum + (Number(l.credit) || 0), 0);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await JournalEntriesApi.create({
        entryDate,
        fiscalPeriodId,
        description,
        lines: lines.map((l) => ({
          accountId: l.accountId,
          debit: Number(l.debit) || 0,
          credit: Number(l.credit) || 0,
          description: l.description || null,
        })),
      });
      setShowForm(false);
      setDescription("");
      setLines([emptyLine(), emptyLine()]);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handlePost(id: string) {
    setError(null);
    try {
      await JournalEntriesApi.post(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleReverse(id: string) {
    setError(null);
    try {
      await JournalEntriesApi.reverse(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>القيود اليومية</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "إلغاء" : "+ قيد جديد"}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>تاريخ القيد</label>
                <input type="date" value={entryDate} onChange={(e) => setEntryDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الفترة المالية</label>
                <select value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} required>
                  <option value="">اختر الفترة</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id} disabled={p.isClosed}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field" style={{ gridColumn: "1 / -1" }}>
                <label>البيان</label>
                <input value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
            </div>

            <table style={{ marginTop: 16 }}>
              <thead>
                <tr>
                  <th>الحساب</th>
                  <th>مدين</th>
                  <th>دائن</th>
                  <th>بيان السطر</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {lines.map((line, i) => (
                  <tr key={i}>
                    <td>
                      <select value={line.accountId} onChange={(e) => updateLine(i, "accountId", e.target.value)} required>
                        <option value="">اختر الحساب</option>
                        {accounts.map((a) => (
                          <option key={a.id} value={a.id}>
                            {a.code} - {a.nameAr}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <input
                        type="number"
                        step="0.01"
                        value={line.debit}
                        onChange={(e) => updateLine(i, "debit", e.target.value)}
                        style={{ width: 110 }}
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        step="0.01"
                        value={line.credit}
                        onChange={(e) => updateLine(i, "credit", e.target.value)}
                        style={{ width: 110 }}
                      />
                    </td>
                    <td>
                      <input value={line.description} onChange={(e) => updateLine(i, "description", e.target.value)} />
                    </td>
                    <td>
                      {lines.length > 2 && (
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => removeLine(i)}>
                          حذف
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td>
                    <button type="button" className="btn btn-secondary btn-sm" onClick={addLine}>
                      + سطر
                    </button>
                  </td>
                  <td>
                    <strong className={totalDebit !== totalCredit ? "text-danger" : "text-success"}>
                      {totalDebit.toLocaleString()}
                    </strong>
                  </td>
                  <td>
                    <strong className={totalDebit !== totalCredit ? "text-danger" : "text-success"}>
                      {totalCredit.toLocaleString()}
                    </strong>
                  </td>
                  <td colSpan={2}></td>
                </tr>
              </tfoot>
            </table>

            <button className="btn" type="submit" style={{ marginTop: 14 }} disabled={totalDebit !== totalCredit || totalDebit === 0}>
              حفظ كمسودة
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>رقم القيد</th>
              <th>التاريخ</th>
              <th>البيان</th>
              <th>مدين</th>
              <th>دائن</th>
              <th>الحالة</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id}>
                <td>{entry.entryNumber}</td>
                <td>{new Date(entry.entryDate).toLocaleDateString("ar-EG")}</td>
                <td>{entry.description}</td>
                <td>{entry.totalDebit.toLocaleString()}</td>
                <td>{entry.totalCredit.toLocaleString()}</td>
                <td>
                  <span className={`badge ${statusLabel[entry.status].cls}`}>{statusLabel[entry.status].text}</span>
                </td>
                <td>
                  {entry.status === JournalEntryStatus.Draft && (
                    <button className="btn btn-sm" onClick={() => handlePost(entry.id)}>
                      ترحيل
                    </button>
                  )}
                  {entry.status === JournalEntryStatus.Posted && (
                    <button className="btn btn-secondary btn-sm" onClick={() => handleReverse(entry.id)}>
                      عكس القيد
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
