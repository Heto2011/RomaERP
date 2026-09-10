import { useEffect, useState } from "react";
import { ManualProfitEntriesApi } from "../api/services";
import { ManualProfitDimension, type ManualProfitEntry } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

function currentMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

interface DraftRow {
  name: string;
  periodMonth: string;
  revenue: number;
  cost: number;
}

export default function ManualProfitGrid({ dimension, nameLabel }: { dimension: ManualProfitDimension; nameLabel: string }) {
  const { t } = useLanguage();
  const [entries, setEntries] = useState<ManualProfitEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showAddForm, setShowAddForm] = useState(false);
  const [newRow, setNewRow] = useState<DraftRow>({ name: "", periodMonth: currentMonth(), revenue: 0, cost: 0 });
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editRow, setEditRow] = useState<DraftRow>({ name: "", periodMonth: currentMonth(), revenue: 0, cost: 0 });

  async function load() {
    const res = await ManualProfitEntriesApi.getAll(dimension);
    setEntries(res.data);
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dimension]);

  async function handleAdd() {
    if (!newRow.name.trim()) return;
    setError(null);
    try {
      await ManualProfitEntriesApi.create({ dimension, ...newRow });
      setNewRow({ name: "", periodMonth: currentMonth(), revenue: 0, cost: 0 });
      setShowAddForm(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function startEdit(entry: ManualProfitEntry) {
    setEditingId(entry.id);
    setEditRow({ name: entry.name, periodMonth: entry.periodMonth.slice(0, 10), revenue: entry.revenue, cost: entry.cost });
  }

  async function handleSaveEdit(id: string) {
    setError(null);
    try {
      await ManualProfitEntriesApi.update(id, editRow);
      setEditingId(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm(t.accounting.confirmDeleteManualEntry)) return;
    setError(null);
    try {
      await ManualProfitEntriesApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div className="card">
      <div className="page-header" style={{ marginBottom: 8 }}>
        <div className="text-muted" style={{ fontSize: 13 }}>{t.accounting.manualEntryNote}</div>
        <button className="btn btn-sm" onClick={() => setShowAddForm((v) => !v)}>
          {showAddForm ? t.common.cancel : t.common.add}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showAddForm && (
        <div className="form-grid" style={{ marginBottom: 12 }}>
          <div className="form-field">
            <label>{nameLabel}</label>
            <input value={newRow.name} onChange={(e) => setNewRow({ ...newRow, name: e.target.value })} />
          </div>
          <div className="form-field">
            <label>{t.accounting.periodMonth}</label>
            <input type="date" value={newRow.periodMonth} onChange={(e) => setNewRow({ ...newRow, periodMonth: e.target.value })} />
          </div>
          <div className="form-field">
            <label>{t.accounting.revenue}</label>
            <input type="number" value={newRow.revenue} onChange={(e) => setNewRow({ ...newRow, revenue: Number(e.target.value) })} />
          </div>
          <div className="form-field">
            <label>{t.accounting.cost}</label>
            <input type="number" value={newRow.cost} onChange={(e) => setNewRow({ ...newRow, cost: Number(e.target.value) })} />
          </div>
          <button className="btn" style={{ alignSelf: "flex-end" }} onClick={handleAdd}>
            {t.common.save}
          </button>
        </div>
      )}

      {entries.length === 0 && <div className="text-muted">{t.common.noData}</div>}

      {entries.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>{nameLabel}</th>
              <th>{t.accounting.periodMonth}</th>
              <th>{t.accounting.revenue}</th>
              <th>{t.accounting.cost}</th>
              <th>{t.accounting.grossProfit}</th>
              <th>{t.accounting.margin}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((e) =>
              editingId === e.id ? (
                <tr key={e.id}>
                  <td><input value={editRow.name} style={{ width: 140 }} onChange={(ev) => setEditRow({ ...editRow, name: ev.target.value })} /></td>
                  <td><input type="date" value={editRow.periodMonth} onChange={(ev) => setEditRow({ ...editRow, periodMonth: ev.target.value })} /></td>
                  <td><input type="number" value={editRow.revenue} style={{ width: 100 }} onChange={(ev) => setEditRow({ ...editRow, revenue: Number(ev.target.value) })} /></td>
                  <td><input type="number" value={editRow.cost} style={{ width: 100 }} onChange={(ev) => setEditRow({ ...editRow, cost: Number(ev.target.value) })} /></td>
                  <td colSpan={2}></td>
                  <td style={{ display: "flex", gap: 4 }}>
                    <button className="btn btn-sm" onClick={() => handleSaveEdit(e.id)}>{t.common.save}</button>
                    <button className="btn btn-secondary btn-sm" onClick={() => setEditingId(null)}>{t.common.cancel}</button>
                  </td>
                </tr>
              ) : (
                <tr key={e.id}>
                  <td>{e.name}</td>
                  <td>{new Date(e.periodMonth).toLocaleDateString()}</td>
                  <td>{e.revenue.toLocaleString()}</td>
                  <td>{e.cost.toLocaleString()}</td>
                  <td className={e.grossProfit >= 0 ? "text-success" : "text-danger"}>{e.grossProfit.toLocaleString()}</td>
                  <td>{e.marginPercent.toFixed(1)}%</td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button className="btn btn-secondary btn-sm" onClick={() => startEdit(e)}>{t.common.edit}</button>
                    <button className="btn btn-danger btn-sm" onClick={() => handleDelete(e.id)}>{t.common.delete}</button>
                  </td>
                </tr>
              )
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}
