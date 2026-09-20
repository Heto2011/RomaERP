import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import { EmployeeRequestsApi } from "../../api/services";
import { EmployeeRequestStatus, EmployeeRequestType, type EmployeeRequest, type LeaveBalance } from "../../api/types";
import { useLanguage } from "../../i18n/LanguageContext";
import { IconCalendar, IconCheck } from "../../components/icons";

export default function PeopleDashboard() {
  const { user } = useAuth();
  const { t } = useLanguage();
  const [leaveBalance, setLeaveBalance] = useState<LeaveBalance | null>(null);
  const [upcomingLeave, setUpcomingLeave] = useState<EmployeeRequest[]>([]);

  useEffect(() => {
    Promise.all([EmployeeRequestsApi.getMine(), EmployeeRequestsApi.getMyLeaveBalance()]).then(([requestsRes, balanceRes]) => {
      const today = new Date().toISOString().slice(0, 10);
      const upcoming = requestsRes.data
        .filter((r) => r.type === EmployeeRequestType.Leave && r.status === EmployeeRequestStatus.Approved && r.dateFrom >= today)
        .sort((a, b) => a.dateFrom.localeCompare(b.dateFrom));
      setUpcomingLeave(upcoming);
      setLeaveBalance(balanceRes.data);
    });
  }, []);

  const nextLeave = upcomingLeave[0];

  return (
    <div>
      <div className="hr-dash-hero">
        <div className="hr-dash-greeting">
          {t.hr.goodDay}, {user?.fullName} 👋
        </div>
        <div className="hr-dash-reminder">
          {nextLeave
            ? `${t.hr.upcomingLeaveReminderPrefix} ${new Date(nextLeave.dateFrom).toLocaleDateString()} ${t.hr.upcomingLeaveReminderTo} ${
                nextLeave.dateTo ? new Date(nextLeave.dateTo).toLocaleDateString() : new Date(nextLeave.dateFrom).toLocaleDateString()
              }`
            : t.hr.noUpcomingLeaveReminder}
        </div>
      </div>

      <div className="stat-grid" style={{ marginTop: -32, position: "relative", zIndex: 1 }}>
        <div className="stat-card hr-dash-stat-card">
          <div className="label">{t.hr.leaveStatTitle}</div>
          <div className="value">{leaveBalance?.remainingDays ?? "…"}</div>
          <div className="hr-dash-stat-sub">{t.hr.leaveDaysRemainingLabel}</div>
        </div>
        <div className="stat-card hr-dash-stat-card">
          <div className="label">{t.hr.sicknessStatTitle}</div>
          <div className="value">{leaveBalance?.sicknessDaysTaken ?? "…"}</div>
          <div className="hr-dash-stat-sub">{t.hr.sicknessDaysTakenLabel}</div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 20 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>{t.hr.upcomingLeaveTitle}</h3>
          <Link to="/people/calendar" className="btn btn-secondary btn-sm" style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <IconCalendar /> {t.hr.hrCalendarTitle}
          </Link>
        </div>
        {upcomingLeave.length === 0 ? (
          <p className="text-muted" style={{ margin: 0 }}>
            {t.hr.noUpcomingLeave}
          </p>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {upcomingLeave.map((r) => (
              <div key={r.id} className="hr-dash-upcoming-row">
                <IconCheck />
                <span>
                  {new Date(r.dateFrom).toLocaleDateString()}
                  {r.dateTo ? ` - ${new Date(r.dateTo).toLocaleDateString()}` : ""}
                </span>
                {r.reason && <span className="text-muted">({r.reason})</span>}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
