import { Fragment, useEffect, useState } from "react";
import { LookupsApi, PayrollApi } from "../../api/services";
import { PayrollRunStatus, type FiscalPeriod, type PayrollRun } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function Payroll() {
  const { t } = useLanguage();
  const statusLabel: Record<PayrollRunStatus, { text: string; cls: string }> = {
    [PayrollRunStatus.Draft]: { text: t.accounting.draft, cls: "badge-draft" },
    [PayrollRunStatus.Approved]: { text: t.hr.approved, cls: "badge-posted" },
    [PayrollRunStatus.Posted]: { text: t.accounting.posted, cls: "badge-posted" },
  };
  const [runs, setRuns] = useState<PayrollRun[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);

  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [runDate, setRunDate] = useState(new Date().toISOString().slice(0, 10));
  const [description, setDescription] = useState("");

  async function load() {
    const [runsRes, periodsRes] = await Promise.all([PayrollApi.getAll(), LookupsApi.fiscalPeriods()]);
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
      await PayrollApi.create({ fiscalPeriodId, runDate, description });
      setShowForm(false);
      setDescription("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleApprove(id: string) {
    setError(null);
    try {
      await PayrollApi.approve(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handlePost(id: string) {
    setError(null);
    try {
      await PayrollApi.post(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.payrollRunsTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.hr.newPayrollRun}
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
                <label>{t.hr.payDate}</label>
                <input type="date" value={runDate} onChange={(e) => setRunDate(e.target.value)} required />
              </div>
              <div className="form-field" style={{ gridColumn: "1 / -1" }}>
                <label>{t.accounting.statement}</label>
                <input value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
            </div>
            <p className="text-muted" style={{ marginTop: 10 }}>
              {t.hr.payrollCalcNote}
            </p>
            <button className="btn" type="submit" style={{ marginTop: 8 }}>
              {t.hr.createAndCalculate}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.hr.payDate}</th>
              <th>{t.accounting.statement}</th>
              <th>{t.hr.employeeCount}</th>
              <th>{t.hr.totalNet}</th>
              <th>{t.common.status}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {runs.map((run) => (
              <Fragment key={run.id}>
                <tr>
                  <td>{new Date(run.runDate).toLocaleDateString()}</td>
                  <td>{run.description}</td>
                  <td>{run.lines.length}</td>
                  <td>{run.totalNet.toLocaleString()}</td>
                  <td>
                    <span className={`badge ${statusLabel[run.status].cls}`}>{statusLabel[run.status].text}</span>
                  </td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button className="btn btn-secondary btn-sm" onClick={() => setExpanded(expanded === run.id ? null : run.id)}>
                      {t.hr.details}
                    </button>
                    {run.status === PayrollRunStatus.Draft && (
                      <button className="btn btn-sm" onClick={() => handleApprove(run.id)}>
                        {t.hr.approve}
                      </button>
                    )}
                    {run.status === PayrollRunStatus.Approved && (
                      <button className="btn btn-sm" onClick={() => handlePost(run.id)}>
                        {t.accounting.post}
                      </button>
                    )}
                  </td>
                </tr>
                {expanded === run.id && (
                  <tr>
                    <td colSpan={6}>
                      <table>
                        <thead>
                          <tr>
                            <th>{t.nav.employees}</th>
                            <th>{t.hr.basicSalary}</th>
                            <th>{t.hr.allowances}</th>
                            <th>{t.hr.deductions}</th>
                            <th>{t.hr.netSalary}</th>
                          </tr>
                        </thead>
                        <tbody>
                          {run.lines.map((line) => (
                            <tr key={line.employeeId}>
                              <td>{line.employeeName}</td>
                              <td>{line.basicSalary.toLocaleString()}</td>
                              <td>{line.totalAllowances.toLocaleString()}</td>
                              <td>{line.totalDeductions.toLocaleString()}</td>
                              <td>{line.netSalary.toLocaleString()}</td>
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
