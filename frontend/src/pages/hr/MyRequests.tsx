import { useEffect, useState } from "react";
import { EmployeeRequestsApi } from "../../api/services";
import { EmployeeRequestStatus, EmployeeRequestType, type EmployeeRequest } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const typeLabelKey = { [EmployeeRequestType.Leave]: "leaveType", [EmployeeRequestType.Permission]: "permissionType", [EmployeeRequestType.Other]: "otherType" } as const;
const statusLabelKey = { [EmployeeRequestStatus.Pending]: "statusPending", [EmployeeRequestStatus.Approved]: "statusApproved", [EmployeeRequestStatus.Rejected]: "statusRejected" } as const;
const statusBadgeClass = { [EmployeeRequestStatus.Pending]: "badge-draft", [EmployeeRequestStatus.Approved]: "badge-posted", [EmployeeRequestStatus.Rejected]: "badge-reversed" } as const;

export default function MyRequests() {
  const { t } = useLanguage();
  const [requests, setRequests] = useState<EmployeeRequest[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [type, setType] = useState<EmployeeRequestType>(EmployeeRequestType.Leave);
  const [dateFrom, setDateFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [dateTo, setDateTo] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const res = await EmployeeRequestsApi.getMine();
    setRequests(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await EmployeeRequestsApi.create({ type, dateFrom, dateTo: dateTo || null, reason: reason || null });
      setShowForm(false);
      setDateTo("");
      setReason("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.myRequests}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.hr.newEmployeeRequest}
        </button>
      </div>
      <p className="text-muted">{t.hr.employeeRequestsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.hr.requestType}</label>
                <select value={type} onChange={(e) => setType(Number(e.target.value) as EmployeeRequestType)}>
                  <option value={EmployeeRequestType.Leave}>{t.hr.leaveType}</option>
                  <option value={EmployeeRequestType.Permission}>{t.hr.permissionType}</option>
                  <option value={EmployeeRequestType.Other}>{t.hr.otherType}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.dateFrom}</label>
                <input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.dateTo}</label>
                <input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.hr.reason}</label>
                <input value={reason} onChange={(e) => setReason(e.target.value)} />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.hr.requestType}</th>
              <th>{t.hr.dateFrom}</th>
              <th>{t.hr.dateTo}</th>
              <th>{t.hr.reason}</th>
              <th>{t.hr.requestStatus}</th>
            </tr>
          </thead>
          <tbody>
            {requests.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {requests.map((r) => (
              <tr key={r.id}>
                <td>{t.hr[typeLabelKey[r.type]]}</td>
                <td>{new Date(r.dateFrom).toLocaleDateString()}</td>
                <td>{r.dateTo ? new Date(r.dateTo).toLocaleDateString() : "-"}</td>
                <td>{r.reason ?? "-"}</td>
                <td>
                  <span className={`badge ${statusBadgeClass[r.status]}`}>{t.hr[statusLabelKey[r.status]]}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
