import { Fragment, useEffect, useState } from "react";
import { DepreciationApi, LookupsApi } from "../../api/services";
import { DepreciationRunStatus, type DepreciationRun, type FiscalPeriod } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function DepreciationRuns() {
  const { t } = useLanguage();
  const statusLabel: Record<DepreciationRunStatus, { text: string; cls: string }> = {
    [DepreciationRunStatus.Draft]: { text: t.accounting.draft, cls: "badge-draft" },
    [DepreciationRunStatus.Posted]: { text: t.accounting.posted, cls: "badge-posted" },
  };

  const [runs, setRuns] = useState<DepreciationRun[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);

  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [runDate, setRunDate] = useState(new Date().toISOString().slice(0, 10));
  const [description, setDescription] = useState("");

  async function load() {
    const [runsRes, periodsRes] = await Promise.all([DepreciationApi.getAll(), LookupsApi.fiscalPeriods()]);
    setRuns(runsRes.data);
    setPeriods(periodsRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await DepreciationApi.create({ fiscalPeriodId, runDate, description: description || null });
      setShowForm(false);
      setDescription("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handlePost(id: string) {
    setError(null);
    try {
      await DepreciationApi.post(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.fixedAssets.depreciationRunsTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.fixedAssets.newDepreciationRun}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.accounting.fiscalPeriod}</label>
                <select value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} required>
                  <option value="">{t.accounting.selectPeriod}</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id} disabled={p.isClosed}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.common.date}</label>
                <input type="date" value={runDate} onChange={(e) => setRunDate(e.target.value)} required />
              </div>
              <div className="form-field" style={{ gridColumn: "1 / -1" }}>
                <label>{t.accounting.statement}</label>
                <input value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.fixedAssets.calculateAndCreate}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.common.date}</th>
              <th>{t.accounting.statement}</th>
              <th>{t.fixedAssets.totalAmount}</th>
              <th>{t.common.status}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {runs.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {runs.map((run) => (
              <Fragment key={run.id}>
                <tr>
                  <td>{new Date(run.runDate).toLocaleDateString()}</td>
                  <td>{run.description}</td>
                  <td>{run.totalAmount.toLocaleString()}</td>
                  <td>
                    <span className={`badge ${statusLabel[run.status].cls}`}>{statusLabel[run.status].text}</span>
                  </td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button className="btn btn-secondary btn-sm" onClick={() => setExpanded(expanded === run.id ? null : run.id)}>
                      {t.hr.details}
                    </button>
                    {run.status === DepreciationRunStatus.Draft && (
                      <button className="btn btn-sm" onClick={() => handlePost(run.id)}>
                        {t.accounting.post}
                      </button>
                    )}
                  </td>
                </tr>
                {expanded === run.id && (
                  <tr>
                    <td colSpan={5}>
                      <table>
                        <thead>
                          <tr>
                            <th>{t.fixedAssets.asset}</th>
                            <th>{t.common.amount}</th>
                          </tr>
                        </thead>
                        <tbody>
                          {run.lines.map((line) => (
                            <tr key={line.fixedAssetId}>
                              <td>{line.assetCode} - {line.assetName}</td>
                              <td>{line.amount.toLocaleString()}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
