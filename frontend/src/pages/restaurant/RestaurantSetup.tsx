import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { RestaurantApi, UsersApi } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import MenuImportDialog from "./MenuImportDialog";

interface Status {
  menuCount: number;
  tableCount: number;
  cashierCount: number;
  tableNumbers: string[];
}

function StepBadge({ done, n }: { done: boolean; n: number }) {
  return (
    <span
      style={{
        width: 30,
        height: 30,
        borderRadius: "50%",
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        fontWeight: 700,
        background: done ? "var(--color-primary)" : "var(--color-surface-2, var(--color-border))",
        color: done ? "#fff" : "var(--color-text)",
        flexShrink: 0,
      }}
    >
      {done ? "✓" : n}
    </span>
  );
}

export default function RestaurantSetup() {
  const { t } = useLanguage();
  const r = t.restaurant;
  const [status, setStatus] = useState<Status | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [showImport, setShowImport] = useState(false);
  const [tablesToAdd, setTablesToAdd] = useState("10");
  const [cashierName, setCashierName] = useState("");
  const [pin, setPin] = useState("");
  const [busy, setBusy] = useState(false);
  const companyCode = (() => {
    try {
      return localStorage.getItem("companyCode") ?? "";
    } catch {
      return "";
    }
  })();

  async function load() {
    try {
      const [menu, tables, users] = await Promise.all([RestaurantApi.getMenu(), RestaurantApi.getTables(), UsersApi.getAll()]);
      setStatus({
        menuCount: menu.data.length,
        tableCount: tables.data.length,
        cashierCount: users.data.filter((u) => u.isActive && u.hasPosPin).length,
        tableNumbers: tables.data.map((x) => x.number),
      });
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function addTables() {
    const count = Math.max(0, Math.min(200, Number(tablesToAdd) || 0));
    if (count === 0 || !status) return;
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const taken = new Set(status.tableNumbers);
      let added = 0;
      for (let n = 1; added < count && n < 1000; n++) {
        if (taken.has(String(n))) continue;
        await RestaurantApi.createTable({ number: String(n), sectionName: null, capacity: 4 });
        added++;
      }
      setMessage(r.setupTablesAdded.replace("{n}", String(added)));
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
      await load();
    } finally {
      setBusy(false);
    }
  }

  async function addOpenPriceItem() {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await RestaurantApi.createOpenPriceItem(r.setupOpenPriceName);
      setMessage(r.setupOpenPriceDone);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function addCashier(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await UsersApi.createPosCashier({ fullName: cashierName.trim(), pin });
      setMessage(r.setupCashierAdded);
      setCashierName("");
      setPin("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  const menuDone = (status?.menuCount ?? 0) > 0;
  const tablesDone = (status?.tableCount ?? 0) > 0;
  const cashierDone = (status?.cashierCount ?? 0) > 0;
  const ready = menuDone && cashierDone;

  return (
    <div style={{ maxWidth: 820 }}>
      <div className="page-header">
        <h1>{r.setupTitle}</h1>
      </div>
      <p className="text-muted" style={{ marginTop: 0 }}>{r.setupIntro}</p>

      {error && <div className="alert alert-error">{error}</div>}
      {message && <div className="alert alert-success">{message}</div>}

      <div className="card" style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <StepBadge done={menuDone} n={1} />
          <h3 style={{ margin: 0 }}>{r.setupMenuTitle}</h3>
          {menuDone && <span className="text-muted">{r.setupMenuCount.replace("{n}", String(status?.menuCount))}</span>}
        </div>
        <p className="text-muted" style={{ margin: 0 }}>{r.setupMenuHint}</p>
        <div>
          <button className="btn" onClick={() => setShowImport(true)}>{menuDone ? r.setupMenuMore : r.setupMenuStart}</button>
        </div>
        <div className="text-muted" style={{ marginTop: 4 }}>{r.setupOpenPriceHint}</div>
        <div>
          <button className="btn btn-secondary btn-sm" onClick={addOpenPriceItem} disabled={busy}>{r.setupOpenPriceButton}</button>
        </div>
      </div>

      <div className="card" style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 16 }}>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <StepBadge done={tablesDone} n={2} />
          <h3 style={{ margin: 0 }}>{r.setupTablesTitle}</h3>
          {tablesDone && <span className="text-muted">{r.setupTablesCount.replace("{n}", String(status?.tableCount))}</span>}
        </div>
        <p className="text-muted" style={{ margin: 0 }}>{r.setupTablesHint}</p>
        <div style={{ display: "flex", gap: 8, alignItems: "end" }}>
          <div className="form-field" style={{ margin: 0 }}>
            <label>{r.setupTablesLabel}</label>
            <input type="number" min={1} max={200} value={tablesToAdd} onChange={(e) => setTablesToAdd(e.target.value)} style={{ width: 120 }} />
          </div>
          <button className="btn btn-secondary" onClick={addTables} disabled={busy}>{r.setupTablesAdd}</button>
        </div>
      </div>

      <form className="card" onSubmit={addCashier} style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 16 }}>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <StepBadge done={cashierDone} n={3} />
          <h3 style={{ margin: 0 }}>{r.setupCashierTitle}</h3>
          {cashierDone && <span className="text-muted">{r.setupCashierCount.replace("{n}", String(status?.cashierCount))}</span>}
        </div>
        <p className="text-muted" style={{ margin: 0 }}>{r.setupCashierHint}</p>
        <div className="form-grid">
          <div className="form-field">
            <label>{r.setupCashierName}</label>
            <input value={cashierName} onChange={(e) => setCashierName(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{r.setupCashierPin}</label>
            <input inputMode="numeric" pattern="[0-9]{4,6}" maxLength={6} value={pin} onChange={(e) => setPin(e.target.value.replace(/\D/g, ""))} required />
          </div>
        </div>
        <div>
          <button className="btn" disabled={busy || !cashierName.trim() || pin.length < 4}>{r.setupCashierAdd}</button>
        </div>
      </form>

      <div className="card" style={{ marginTop: 16, display: "flex", flexDirection: "column", gap: 10 }}>
        <h3 style={{ margin: 0 }}>{r.setupOpenTitle}</h3>
        {ready ? (
          <>
            <p style={{ margin: 0 }}>
              {r.setupOpenHint}
              {companyCode && (
                <> {r.setupCompanyCode} <strong dir="ltr">{companyCode}</strong></>
              )}
            </p>
            <div>
              <Link className="btn btn-primary" to="/pos-login">{r.setupOpenButton}</Link>
            </div>
          </>
        ) : (
          <p className="text-muted" style={{ margin: 0 }}>{r.setupNotReady}</p>
        )}
      </div>

      {showImport && <MenuImportDialog onClose={() => setShowImport(false)} onDone={() => load()} />}
    </div>
  );
}
