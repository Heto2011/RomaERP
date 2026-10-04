import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AttendanceApi, EmployeeContractsApi, EmployeeRequestsApi, EmployeesApi } from "../api/services";
import {
  EmployeeContractStatus,
  EmploymentStatus,
  EmployeeRequestType,
  type CalendarEntry,
  type EmployeeContract,
  type EmployeeRequest,
} from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

/// <summary>The HR manager's / owner's view of the company on the Roma HR home page: headline numbers,
/// requests waiting for a decision, contract alerts and who is out today. Everything comes from existing
/// HR endpoints; the ordinary employee never sees (or can load) any of it.</summary>
export default function ManagerOverview() {
  const { t } = useLanguage();
  const [activeCount, setActiveCount] = useState<number | null>(null);
  const [presentToday, setPresentToday] = useState<number | null>(null);
  const [pending, setPending] = useState<EmployeeRequest[]>([]);
  const [contracts, setContracts] = useState<EmployeeContract[]>([]);
  const [outToday, setOutToday] = useState<CalendarEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    const today = new Date().toISOString().slice(0, 10);
    try {
      const [employees, attendance, pendingRes, contractsRes, calendar] = await Promise.all([
        EmployeesApi.getAll(),
        AttendanceApi.getAll(today, today),
        EmployeeRequestsApi.getPending(),
        EmployeeContractsApi.getAll(),
        EmployeeRequestsApi.getCalendar(today, today),
      ]);
      setActiveCount(employees.data.filter((e) => e.employmentStatus !== EmploymentStatus.Terminated).length);
      setPresentToday(new Set(attendance.data.map((a) => a.employeeId)).size);
      setPending(pendingRes.data);
      setContracts(contractsRes.data);
      setOutToday(calendar.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function decide(id: string, approve: boolean) {
    setError(null);
    try {
      await EmployeeRequestsApi.decide(id, { approve });
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const typeLabel = (type: EmployeeRequestType) =>
    type === EmployeeRequestType.Leave ? t.hr.mgrLeaveType : type === EmployeeRequestType.Sickness ? t.hr.mgrSickType : t.hr.mgrOtherType;
  const fmt = (d: string) => new Date(d).toLocaleDateString();

  const active = contracts.filter((c) => c.status === EmployeeContractStatus.Active && c.daysUntilExpiry !== null);
  const overdue = active.filter((c) => (c.daysUntilExpiry ?? 0) < 0);
  const expiring = active.filter((c) => (c.daysUntilExpiry ?? 0) >= 0 && (c.daysUntilExpiry ?? 0) <= 30);

  return (
    <section style={{ marginTop: 28 }}>
      <h3 style={{ margin: "0 0 12px" }}>{t.hr.mgrOverviewTitle}</h3>
      {error && <div className="alert-error">{error}</div>}

      <div className="stat-grid">
        <div className="stat-card hr-dash-stat-card">
          <div className="value">{activeCount ?? "…"}</div>
          <div className="hr-dash-stat-sub">{t.hr.mgrActiveEmployees}</div>
        </div>
        <div className="stat-card hr-dash-stat-card">
          <div className="value">{presentToday ?? "…"}</div>
          <div className="hr-dash-stat-sub">{t.hr.mgrPresentToday}</div>
        </div>
        <div className="stat-card hr-dash-stat-card">
          <div className="value">{outToday.length}</div>
          <div className="hr-dash-stat-sub">{t.hr.mgrOnLeaveToday}</div>
        </div>
        <div className="stat-card hr-dash-stat-card">
          <div className="value">{pending.length}</div>
          <div className="hr-dash-stat-sub">{t.hr.mgrPendingRequests}</div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>{t.hr.mgrNeedsApproval}</h3>
          <Link to="/people/employee-requests" className="btn btn-secondary btn-sm">
            {t.hr.mgrSeeAll}
          </Link>
        </div>
        {pending.length === 0 ? (
          <p className="text-muted" style={{ margin: 0 }}>{t.hr.mgrNothingPending}</p>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {pending.slice(0, 5).map((r) => (
              <div key={r.id} className="hr-dash-upcoming-row" style={{ justifyContent: "space-between", flexWrap: "wrap" }}>
                <span>
                  <strong>{r.employeeName}</strong> — {typeLabel(r.type)} {fmt(r.dateFrom)}
                  {r.dateTo ? ` → ${fmt(r.dateTo)}` : ""}
                  {r.reason && <span className="text-muted"> ({r.reason})</span>}
                </span>
                <span style={{ display: "flex", gap: 6 }}>
                  <button className="btn btn-sm" onClick={() => decide(r.id, true)}>{t.hr.mgrApprove}</button>
                  <button className="btn btn-secondary btn-sm" onClick={() => decide(r.id, false)}>{t.hr.mgrReject}</button>
                </span>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="stat-grid" style={{ marginTop: 16 }}>
        <div className="card" style={{ margin: 0 }}>
          <h3 style={{ margin: "0 0 12px" }}>{t.hr.mgrAlerts}</h3>
          {overdue.length + expiring.length === 0 ? (
            <p className="text-muted" style={{ margin: 0 }}>{t.hr.mgrNoAlerts}</p>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {overdue.length > 0 && (
                <Link to="/people/employee-contracts" className="hr-dash-upcoming-row" style={{ color: "var(--color-danger)" }}>
                  <strong>{overdue.length}</strong> {t.hr.mgrContractsOverdue}
                </Link>
              )}
              {expiring.length > 0 && (
                <Link to="/people/employee-contracts" className="hr-dash-upcoming-row">
                  <strong>{expiring.length}</strong> {t.hr.mgrContractsExpiring}
                </Link>
              )}
            </div>
          )}
        </div>
        <div className="card" style={{ margin: 0 }}>
          <h3 style={{ margin: "0 0 12px" }}>{t.hr.mgrWhoIsOut}</h3>
          {outToday.length === 0 ? (
            <p className="text-muted" style={{ margin: 0 }}>{t.hr.mgrNobodyOut}</p>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {outToday.map((c, i) => (
                <div key={i} className="hr-dash-upcoming-row">
                  <strong>{c.employeeName}</strong>
                  <span className="text-muted">{typeLabel(c.type)} · {fmt(c.dateFrom)} → {fmt(c.dateTo)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
