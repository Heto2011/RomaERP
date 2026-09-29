import { useEffect, useMemo, useState } from "react";
import { AccountsApi, FiscalPeriodsAdminApi, OpeningBalanceApi } from "../../api/services";
import type { Account, FiscalYearDetail, JournalEntry } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

interface RowState {
  debit: string;
  credit: string;
}

export default function OpeningBalances() {
  const { t, lang } = useLanguage();
  const typeLabels: Record<number, string> = {
    1: t.accounting.types.asset,
    2: t.accounting.types.liability,
    3: t.accounting.types.equity,
    4: t.accounting.types.revenue,
    5: t.accounting.types.expense,
  };
  const [years, setYears] = useState<FiscalYearDetail[]>([]);
  const [selectedYearId, setSelectedYearId] = useState("");
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [existingEntry, setExistingEntry] = useState<JournalEntry | null>(null);
  const [rows, setRows] = useState<Record<string, RowState>>({});
  const [entryDate, setEntryDate] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    const [yearsRes, accountsRes] = await Promise.all([
      FiscalPeriodsAdminApi.getAllYears(),
      AccountsApi.getAll(),
    ]);
    setYears(yearsRes.data);
    setAccounts(accountsRes.data.filter((a) => !a.isControlAccount && a.isActive));

    const defaultYear = yearsRes.data.find((y) => !y.isClosed) ?? yearsRes.data[0];
    if (defaultYear) {
      setSelectedYearId(defaultYear.id);
      const firstPeriod = [...defaultYear.periods].sort((a, b) => a.startDate.localeCompare(b.startDate))[0];
      if (firstPeriod) setEntryDate(firstPeriod.startDate.slice(0, 10));
    }
    setLoading(false);
  }

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (!selectedYearId) return;
    setError(null);
    setSuccess(false);
    OpeningBalanceApi.getForFiscalYear(selectedYearId).then((res) => setExistingEntry(res.data));
  }, [selectedYearId]);

  const selectedYear = years.find((y) => y.id === selectedYearId);
  const firstPeriodId = useMemo(() => {
    if (!selectedYear) return "";
    const sorted = [...selectedYear.periods].sort((a, b) => a.startDate.localeCompare(b.startDate));
    return sorted[0]?.id ?? "";
  }, [selectedYear]);

  function updateRow(accountId: string, field: keyof RowState, value: string) {
    setRows((prev) => ({ ...prev, [accountId]: { ...prev[accountId], [field]: value } }));
  }

  const totalDebit = Object.values(rows).reduce((sum, r) => sum + (Number(r.debit) || 0), 0);
  const totalCredit = Object.values(rows).reduce((sum, r) => sum + (Number(r.credit) || 0), 0);
  const isBalanced = totalDebit === totalCredit && totalDebit > 0;

  const accountsByType = useMemo(() => {
    const groups: Record<number, Account[]> = {};
    for (const a of accounts) {
      groups[a.accountType] = groups[a.accountType] ?? [];
      groups[a.accountType].push(a);
    }
    return groups;
  }, [accounts]);

  async function handleSubmit() {
    setError(null);
    if (!isBalanced) {
      setError(t.accounting.unbalancedError);
      return;
    }

    const lines = Object.entries(rows)
      .map(([accountId, r]) => ({ accountId, debit: Number(r.debit) || 0, credit: Number(r.credit) || 0 }))
      .filter((l) => l.debit > 0 || l.credit > 0);

    try {
      await OpeningBalanceApi.create({ fiscalPeriodId: firstPeriodId, entryDate, lines });
      setSuccess(true);
      const res = await OpeningBalanceApi.getForFiscalYear(selectedYearId);
      setExistingEntry(res.data);
      setRows({});
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  if (loading) return <div className="page-header"><h1>{t.accounting.openingBalancesTitle}</h1></div>;

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.openingBalancesTitle}</h1>
      </div>

      <div className="card">
        <p className="text-muted" style={{ marginTop: 0 }}>
          {t.accounting.openingBalancesIntro}
        </p>

        <div className="form-grid">
          <div className="form-field">
            <label>{t.accounting.fiscalYear}</label>
            <select value={selectedYearId} onChange={(e) => setSelectedYearId(e.target.value)}>
              {years.map((y) => (
                <option key={y.id} value={y.id}>
                  {y.name}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{t.accounting.openingBalanceDate}</label>
            <input type="date" value={entryDate} onChange={(e) => setEntryDate(e.target.value)} />
          </div>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}
      {success && (
        <div className="card" style={{ borderColor: "var(--color-success)" }}>
          <strong className="text-success">{t.accounting.openingBalancesSaved}</strong>
        </div>
      )}

      {existingEntry ? (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>
            {t.accounting.openingBalancesForYearPrefix} {selectedYear?.name} — {t.accounting.entryNumberPrefix} {existingEntry.entryNumber}
          </h3>
          <p className="text-muted">
            {t.accounting.alreadyEnteredNote}
          </p>
          <table>
            <thead>
              <tr>
                <th>{t.common.account}</th>
                <th>{t.accounting.debit}</th>
                <th>{t.accounting.credit}</th>
              </tr>
            </thead>
            <tbody>
              {existingEntry.lines.map((l) => (
                <tr key={l.id}>
                  <td>{l.accountCode} - {l.accountName}</td>
                  <td>{l.debit.toLocaleString()}</td>
                  <td>{l.credit.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td><strong>{t.common.total}</strong></td>
                <td><strong>{existingEntry.totalDebit.toLocaleString()}</strong></td>
                <td><strong>{existingEntry.totalCredit.toLocaleString()}</strong></td>
              </tr>
            </tfoot>
          </table>
        </div>
      ) : (
        <div className="card">
          {Object.entries(typeLabels).map(([type, label]) => {
            const typeAccounts = accountsByType[Number(type)];
            if (!typeAccounts?.length) return null;

            return (
              <div key={type} style={{ marginBottom: 20 }}>
                <h3>{label}</h3>
                <table>
                  <thead>
                    <tr>
                      <th>{t.common.code}</th>
                      <th>{t.common.account}</th>
                      <th>{t.accounting.debit}</th>
                      <th>{t.accounting.credit}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {typeAccounts.map((a) => (
                      <tr key={a.id}>
                        <td>{a.code}</td>
                        <td>{bilingualName(a.nameAr, a.nameEn, lang)}</td>
                        <td>
                          <input
                            type="number"
                            step="0.01"
                            style={{ width: 130 }}
                            value={rows[a.id]?.debit ?? ""}
                            disabled={Number(rows[a.id]?.credit) > 0}
                            onChange={(e) => updateRow(a.id, "debit", e.target.value)}
                          />
                        </td>
                        <td>
                          <input
                            type="number"
                            step="0.01"
                            style={{ width: 130 }}
                            value={rows[a.id]?.credit ?? ""}
                            disabled={Number(rows[a.id]?.debit) > 0}
                            onChange={(e) => updateRow(a.id, "credit", e.target.value)}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            );
          })}

          <table>
            <tfoot>
              <tr>
                <td><strong>{t.common.total}</strong></td>
                <td>
                  <strong className={isBalanced ? "text-success" : "text-danger"}>{totalDebit.toLocaleString()}</strong>
                </td>
                <td>
                  <strong className={isBalanced ? "text-success" : "text-danger"}>{totalCredit.toLocaleString()}</strong>
                </td>
              </tr>
            </tfoot>
          </table>

          <button className="btn" style={{ marginTop: 14 }} onClick={handleSubmit} disabled={!isBalanced}>
            {t.accounting.saveAndPostOpeningBalances}
          </button>
        </div>
      )}
    </div>
  );
}
