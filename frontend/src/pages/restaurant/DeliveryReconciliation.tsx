import { useEffect, useRef, useState } from "react";
import { DeliveryReconciliationApi } from "../../api/services";
import type { DeliveryReconciliationReport, DeliverySettlementImportResult } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function DeliveryReconciliationPage() {
  const { t } = useLanguage();
  const [imports, setImports] = useState<DeliverySettlementImportResult[]>([]);
  const [platformName, setPlatformName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<DeliveryReconciliationReport | null>(null);

  async function load() {
    const res = await DeliveryReconciliationApi.getImports();
    setImports(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleImport(file: File) {
    if (!platformName.trim()) {
      setError(t.inventory.platformName);
      return;
    }
    setError(null);
    try {
      await DeliveryReconciliationApi.import(file, platformName.trim());
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function loadReport() {
    setError(null);
    try {
      const res = await DeliveryReconciliationApi.getReconciliation(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.inventory.deliveryReconciliationTitle}<InfoTooltip text={t.inventory.deliveryReconciliationIntro} /></h1>
      </div>
      <p className="text-muted">{t.inventory.deliveryReconciliationIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.inventory.platformName}</label>
          <input value={platformName} onChange={(e) => setPlatformName(e.target.value)} placeholder={t.inventory.platformNamePlaceholder} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={() => fileInputRef.current?.click()}>
          {t.inventory.uploadSettlement}
        </button>
        <input
          ref={fileInputRef}
          type="file"
          accept=".csv"
          style={{ display: "none" }}
          onChange={(e) => e.target.files?.[0] && handleImport(e.target.files[0])}
        />
      </div>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.common.from}</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.common.to}</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={loadReport}>
          {t.common.viewReport}
        </button>
      </div>

      {report && (
        <div className="card">
          <table style={{ maxWidth: 480 }}>
            <tbody>
              <tr><td>{t.inventory.expectedRevenue}</td><td style={{ textAlign: "end" }}>{report.expectedRevenue.toLocaleString()}</td></tr>
              <tr><td>{t.inventory.receivedAmount}</td><td style={{ textAlign: "end" }}>{report.receivedAmount.toLocaleString()}</td></tr>
              <tr>
                <td><strong>{t.inventory.varianceLabel}</strong></td>
                <td style={{ textAlign: "end" }} className={report.variance >= 0 ? "text-success" : "text-danger"}>
                  <strong>{report.variance.toLocaleString()}</strong>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      )}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.inventory.pastImports} ({imports.length})</h3>
        <table>
          <thead>
            <tr>
              <th>{t.inventory.platformName}</th>
              <th>{t.common.from}</th>
              <th>{t.common.to}</th>
              <th>{t.common.total}</th>
            </tr>
          </thead>
          <tbody>
            {imports.map((i) => (
              <tr key={i.id}>
                <td>{i.platformName}</td>
                <td>{new Date(i.periodFrom).toLocaleDateString()}</td>
                <td>{new Date(i.periodTo).toLocaleDateString()}</td>
                <td>{i.totalAmount.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
