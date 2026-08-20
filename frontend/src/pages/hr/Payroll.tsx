import { Fragment, useEffect, useState } from "react";
import { LookupsApi, PayrollApi } from "../../api/services";
import { PayrollRunStatus, type FiscalPeriod, type PayrollRun } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const statusLabel: Record<PayrollRunStatus, { text: string; cls: string }> = {
  [PayrollRunStatus.Draft]: { text: "مسودة", cls: "badge-draft" },
  [PayrollRunStatus.Approved]: { text: "معتمد", cls: "badge-posted" },
  [PayrollRunStatus.Posted]: { text: "مرحل", cls: "badge-posted" },
};

export default function Payroll() {
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
        <h1>دورات الرواتب</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "إلغاء" : "+ دورة رواتب جديدة"}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
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
              <div className="form-field">
                <label>تاريخ الصرف</label>
                <input type="date" value={runDate} onChange={(e) => setRunDate(e.target.value)} required />
              </div>
              <div className="form-field" style={{ gridColumn: "1 / -1" }}>
                <label>وصف</label>
                <input value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
            </div>
            <p className="text-muted" style={{ marginTop: 10 }}>
              سيتم حساب الرواتب تلقائيًا لجميع الموظفين النشطين بناءً على الراتب الأساسي وعناصر الأجر المضافة لكل موظف.
            </p>
            <button className="btn" type="submit" style={{ marginTop: 8 }}>
              إنشاء وحساب
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>تاريخ الصرف</th>
              <th>الوصف</th>
              <th>عدد الموظفين</th>
              <th>إجمالي الصافي</th>
              <th>الحالة</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            {runs.map((run) => (
              <Fragment key={run.id}>
                <tr>
                  <td>{new Date(run.runDate).toLocaleDateString("ar-EG")}</td>
                  <td>{run.description}</td>
                  <td>{run.lines.length}</td>
                  <td>{run.totalNet.toLocaleString()}</td>
                  <td>
                    <span className={`badge ${statusLabel[run.status].cls}`}>{statusLabel[run.status].text}</span>
                  </td>
                  <td style={{ display: "flex", gap: 6 }}>
                    <button className="btn btn-secondary btn-sm" onClick={() => setExpanded(expanded === run.id ? null : run.id)}>
                      تفاصيل
                    </button>
                    {run.status === PayrollRunStatus.Draft && (
                      <button className="btn btn-sm" onClick={() => handleApprove(run.id)}>
                        اعتماد
                      </button>
                    )}
                    {run.status === PayrollRunStatus.Approved && (
                      <button className="btn btn-sm" onClick={() => handlePost(run.id)}>
                        ترحيل
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
                            <th>الموظف</th>
                            <th>الأساسي</th>
                            <th>البدلات</th>
                            <th>الاستقطاعات</th>
                            <th>الصافي</th>
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
