import { useEffect, useState } from "react";
import { RestaurantApi } from "../../api/services";
import { RestaurantTableStatus, type RestaurantTable } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function RestaurantTables() {
  const { t } = useLanguage();
  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [number, setNumber] = useState("");
  const [sectionName, setSectionName] = useState("");
  const [capacity, setCapacity] = useState(4);

  async function load() {
    const res = await RestaurantApi.getTables();
    setTables(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await RestaurantApi.createTable({ number, sectionName: sectionName || null, capacity });
      setShowForm(false);
      setNumber("");
      setSectionName("");
      setCapacity(4);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function toggleReserved(table: RestaurantTable) {
    setError(null);
    try {
      const nextStatus = table.status === RestaurantTableStatus.Reserved ? RestaurantTableStatus.Available : RestaurantTableStatus.Reserved;
      await RestaurantApi.setTableStatus(table.id, nextStatus);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const statusLabel: Record<RestaurantTableStatus, string> = {
    [RestaurantTableStatus.Available]: t.restaurant.tableAvailable,
    [RestaurantTableStatus.Occupied]: t.restaurant.tableOccupied,
    [RestaurantTableStatus.Reserved]: t.restaurant.tableReserved,
  };
  const statusClass: Record<RestaurantTableStatus, string> = {
    [RestaurantTableStatus.Available]: "badge badge-posted",
    [RestaurantTableStatus.Occupied]: "badge badge-reversed",
    [RestaurantTableStatus.Reserved]: "badge badge-draft",
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.restaurant.tablesTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.restaurant.newTable}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.restaurant.tableNumber}</label>
                <input value={number} onChange={(e) => setNumber(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.restaurant.section}</label>
                <input value={sectionName} onChange={(e) => setSectionName(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.restaurant.capacity}</label>
                <input type="number" min={1} value={capacity} onChange={(e) => setCapacity(Number(e.target.value))} required />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>{t.common.save}</button>
          </form>
        </div>
      )}

      <div className="card" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))", gap: 12 }}>
        {tables.length === 0 && <div className="text-muted">{t.common.noData}</div>}
        {tables.map((table) => (
          <div key={table.id} className="card" style={{ padding: 14, textAlign: "center" }}>
            <div style={{ fontSize: 20, fontWeight: 700 }}>{table.number}</div>
            {table.sectionName && <div className="text-muted">{table.sectionName}</div>}
            <div className="text-muted">{t.restaurant.capacity}: {table.capacity}</div>
            <div style={{ margin: "8px 0" }}>
              <span className={statusClass[table.status]}>{statusLabel[table.status]}</span>
            </div>
            {table.status !== RestaurantTableStatus.Occupied && (
              <button className="btn btn-secondary btn-sm" onClick={() => toggleReserved(table)}>
                {table.status === RestaurantTableStatus.Reserved ? t.restaurant.markAvailable : t.restaurant.markReserved}
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
