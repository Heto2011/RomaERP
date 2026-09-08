import { useEffect, useState } from "react";
import { EmployeeRequestsApi } from "../../api/services";
import { EmployeeRequestStatus, EmployeeRequestType, type EmployeeRequest } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const typeLabelKey = { [EmployeeRequestType.Leave]: "leaveType", [EmployeeRequestType.Permission]: "permissionType", [EmployeeRequestType.Other]: "otherType" } as const;
const statusLabelKey = { [EmployeeRequestStatus.Pending]: "statusPending", [EmployeeRequestStatus.Approved]: "statusApproved", [EmployeeRequestStatus.Rejected]: "statusRejected" } as const;
const statusBadgeClass = { [EmployeeRequestStatus.Pending]: "badge-draft", [EmployeeRequestStatus.Approved]: "badge-posted", [EmployeeRequestStatus.Rejected]: "badge-reversed" } as const;

export default function EmployeeRequestsAdmin() {
  const { t } = useLanguage();
  const [pending, setPending] = useState<EmployeeRequest[]>([]);
  const [all, setAll] = useState<EmployeeRequest[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const [pendingRes, allRes] = await Promise.all([EmployeeRequestsApi.getPending(), EmployeeRequestsApi.getAll()]);
    setPending(pendingRes.data);
    setAll(allRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleDecide(id: string, approve: boolean) {
    setError(null);
    try {
      await EmployeeRequestsApi.decide(id, { approve });
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.employeeRequestsTitle}</h1>
      </div>
      <p className="text-muted">{t.hr.employeeRequestsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.hr.pendingApprovals} ({pending.length})</h3>
        <table>
          <thead>
            <tr>
              <th>{t.hr.employeesTitle}</th>
              <th>{t.hr.requestType}</th>
              <th>{t.hr.dateFrom}</th>
              <th>{t.hr.dateTo}</th>
              <th>{t.hr.reason}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {pending.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {pending.map((r) => (
              <tr key={r.id}>
                <td>{r.employeeName}</td>
                <td>{t.hr[typeLabelKey[r.type]]}</td>
                <td>{new Date(r.dateFrom).toLocaleDateString()}</td>
                <td>{r.dateTo ? new Date(r.dateTo).toLocaleDateString() : "-"}</td>
                <td>{r.reason ?? "-"}</td>
                <td style={{ display: "flex", gap: 8 }}>
                  <button className="btn btn-sm" onClick={() => handleDecide(r.id, true)}>
                    {t.hr.approveRequest}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDecide(r.id, false)}>
                    {t.hr.rejectRequest}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.hr.allRequests} ({all.length})</h3>
        <table>
          <thead>
            <tr>
              <th>{t.hr.employeesTitle}</th>
              <th>{t.hr.requestType}</th>
              <th>{t.hr.dateFrom}</th>
              <th>{t.hr.dateTo}</th>
              <th>{t.hr.requestStatus}</th>
            </tr>
          </thead>
          <tbody>
            {all.map((r) => (
              <tr key={r.id}>
                <td>{r.employeeName}</td>
                <td>{t.hr[typeLabelKey[r.type]]}</td>
                <td>{new Date(r.dateFrom).toLocaleDateString()}</td>
                <td>{r.dateTo ? new Date(r.dateTo).toLocaleDateString() : "-"}</td>
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
