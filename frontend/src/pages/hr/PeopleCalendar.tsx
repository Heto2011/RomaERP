import { useEffect, useMemo, useState } from "react";
import { EmployeeRequestsApi } from "../../api/services";
import { EmployeeRequestType, type CalendarEntry } from "../../api/types";
import { useLanguage } from "../../i18n/LanguageContext";

function toIsoDate(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export default function PeopleCalendar() {
  const { t, lang } = useLanguage();
  const [monthStart, setMonthStart] = useState(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1);
  });
  const [entries, setEntries] = useState<CalendarEntry[]>([]);

  const monthEnd = useMemo(() => new Date(monthStart.getFullYear(), monthStart.getMonth() + 1, 0), [monthStart]);

  useEffect(() => {
    EmployeeRequestsApi.getCalendar(toIsoDate(monthStart), toIsoDate(monthEnd)).then((r) => setEntries(r.data));
  }, [monthStart, monthEnd]);

  const weekDayLabels = useMemo(() => {
    const formatter = new Intl.DateTimeFormat(lang === "ar" ? "ar" : "en", { weekday: "short" });
    // Monday-first week, matching the grid below.
    return [1, 2, 3, 4, 5, 6, 0].map((dow) => formatter.format(new Date(2024, 0, 7 + dow)));
  }, [lang]);

  const cells = useMemo(() => {
    const firstDow = (monthStart.getDay() + 6) % 7; // Monday = 0
    const daysInMonth = monthEnd.getDate();
    const result: (Date | null)[] = [];
    for (let i = 0; i < firstDow; i++) result.push(null);
    for (let day = 1; day <= daysInMonth; day++) result.push(new Date(monthStart.getFullYear(), monthStart.getMonth(), day));
    return result;
  }, [monthStart, monthEnd]);

  const entriesByDay = useMemo(() => {
    const map = new Map<string, CalendarEntry[]>();
    for (const entry of entries) {
      const from = new Date(entry.dateFrom);
      const to = new Date(entry.dateTo);
      for (let d = new Date(Math.max(from.getTime(), monthStart.getTime())); d <= to && d <= monthEnd; d.setDate(d.getDate() + 1)) {
        const key = toIsoDate(d);
        const list = map.get(key) ?? [];
        list.push(entry);
        map.set(key, list);
      }
    }
    return map;
  }, [entries, monthStart, monthEnd]);

  const monthLabel = monthStart.toLocaleDateString(lang === "ar" ? "ar" : "en", { month: "long", year: "numeric" });
  const hasAnyEntry = entries.length > 0;

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.hrCalendarTitle}</h1>
      </div>
      <p className="text-muted">{t.hr.hrCalendarIntro}</p>

      <div className="card">
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 16 }}>
          <button
            className="btn btn-secondary btn-sm"
            title={t.hr.hrCalendarPrevMonth}
            onClick={() => setMonthStart((d) => new Date(d.getFullYear(), d.getMonth() - 1, 1))}
          >
            ‹
          </button>
          <strong style={{ fontSize: 16 }}>{monthLabel}</strong>
          <button
            className="btn btn-secondary btn-sm"
            title={t.hr.hrCalendarNextMonth}
            onClick={() => setMonthStart((d) => new Date(d.getFullYear(), d.getMonth() + 1, 1))}
          >
            ›
          </button>
        </div>

        {!hasAnyEntry && <p className="text-muted" style={{ textAlign: "center", padding: "8px 0 16px" }}>{t.hr.hrCalendarNoEntries}</p>}

        <div className="hr-calendar-grid">
          {weekDayLabels.map((label) => (
            <div key={label} className="hr-calendar-weekday">
              {label}
            </div>
          ))}
          {cells.map((date, idx) => {
            if (!date) return <div key={`empty-${idx}`} className="hr-calendar-cell hr-calendar-cell-empty" />;
            const dayEntries = entriesByDay.get(toIsoDate(date)) ?? [];
            const isToday = toIsoDate(date) === toIsoDate(new Date());
            return (
              <div key={toIsoDate(date)} className={"hr-calendar-cell" + (isToday ? " hr-calendar-cell-today" : "")}>
                <div className="hr-calendar-day-number">{date.getDate()}</div>
                <div className="hr-calendar-badges">
                  {dayEntries.map((entry, i) => (
                    <span
                      key={i}
                      className={"hr-calendar-badge" + (entry.type === EmployeeRequestType.Sickness ? " hr-calendar-badge-sick" : " hr-calendar-badge-leave")}
                      title={`${entry.employeeName} — ${entry.type === EmployeeRequestType.Sickness ? t.hr.hrCalendarSicknessBadge : t.hr.hrCalendarLeaveBadge}`}
                    >
                      {entry.employeeName}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
