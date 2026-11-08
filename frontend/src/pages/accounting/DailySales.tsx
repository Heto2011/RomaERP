import { useEffect, useState } from "react";
import { DailySalesApi, JournalEntriesApi } from "../../api/services";
import type { DailySalesEntry } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const VAT_KEY = "roma.dailySales.vatRate";

function readVat(): string {
  try {
    return localStorage.getItem(VAT_KEY) ?? "0";
  } catch {
    return "0";
  }
}

const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function DailySales() {
  const { t } = useLanguage();
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [cash, setCash] = useState("");
  const [card, setCard] = useState("");
  const [vatRate, setVatRate] = useState(readVat());
  const [label, setLabel] = useState("");
  const [rows, setRows] = useState<DailySalesEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    try {
      setRows((await DailySalesApi.recent()).data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
  }, []);

  const cashN = Number(cash) || 0;
  const cardN = Number(card) || 0;
  const rate = Number(vatRate) || 0;
  const gross = cashN + cardN;
  const vat = rate > 0 ? Math.round((gross - gross / (1 + rate / 100)) * 100) / 100 : 0;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      await DailySalesApi.create({
        salesDate: date,
        cashAmount: cashN,
        cardAmount: cardN,
        vatRatePercent: rate,
        label: label.trim() || undefined,
      });
      try {
        localStorage.setItem(VAT_KEY, String(rate));
      } catch {
        /* remembered rate is only a convenience */
      }
      setCash("");
      setCard("");
      setMessage(t.accounting.dailySalesSaved);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function reverse(id: string) {
    if (!window.confirm(t.accounting.dailySalesReverseConfirm)) return;
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
        <h1>{t.accounting.dailySalesTitle}</h1>
      </div>
      <p className="text-muted" style={{ marginTop: 0 }}>{t.accounting.dailySalesHint}</p>

      {error && <div className="alert alert-error">{error}</div>}
      {message && <div className="alert alert-success">{message}</div>}

      <form className="card" onSubmit={submit}>
        <div className="form-grid">
          <div className="form-field">
            <label>{t.common.date}</label>
            <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.accounting.dailySalesCash}</label>
            <input type="number" min="0" step="0.01" value={cash} onChange={(e) => setCash(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{t.accounting.dailySalesCard}</label>
            <input type="number" min="0" step="0.01" value={card} onChange={(e) => setCard(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{t.accounting.dailySalesVatRate}</label>
            <input type="number" min="0" max="100" step="0.01" value={vatRate} onChange={(e) => setVatRate(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{t.accounting.dailySalesLabel}</label>
            <input value={label} onChange={(e) => setLabel(e.target.value)} />
          </div>
        </div>

        <div className="d2-facts" style={{ margin: "16px 0" }}>
          <div><strong>{t.accounting.dailySalesTotal}:</strong> {money(gross)}</div>
          <div><strong>{t.accounting.dailySalesNet}:</strong> {money(gross - vat)}</div>
          <div><strong>{t.common.vat}:</strong> {money(vat)}</div>
        </div>

        <button className="btn btn-primary" disabled={busy || gross <= 0}>
          {t.accounting.dailySalesSave}
        </button>
      </form>

      <div className="card" style={{ marginTop: 16 }}>
        <h3 style={{ marginTop: 0 }}>{t.accounting.dailySalesRecent}</h3>
        {rows.length === 0 ? (
          <p className="text-muted">{t.accounting.dailySalesEmpty}</p>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table className="table">
              <thead>
                <tr>
                  <th>{t.common.date}</th>
                  <th>{t.accounting.dailySalesLabel.split(" — ")[0]}</th>
                  <th>{t.common.cash}</th>
                  <th>{t.accounting.dailySalesCardCol}</th>
                  <th>{t.accounting.dailySalesNet}</th>
                  <th>{t.common.vat}</th>
                  <th>{t.common.total}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.journalEntryId} style={r.status === "Reversed" ? { opacity: 0.5, textDecoration: "line-through" } : undefined}>
                    <td>{r.salesDate.slice(0, 10)}</td>
                    <td>{r.label ?? "—"}</td>
                    <td>{money(r.cashAmount)}</td>
                    <td>{money(r.cardAmount)}</td>
                    <td>{money(r.netSales)}</td>
                    <td>{money(r.vatAmount)}</td>
                    <td>{money(r.total)}</td>
                    <td>
                      {r.status !== "Reversed" && (
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => reverse(r.journalEntryId)}>
                          {t.accounting.dailySalesReverse}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
