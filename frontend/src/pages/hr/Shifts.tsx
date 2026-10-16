import { useEffect, useMemo, useState } from "react";
import { EmployeesApi, ShiftsApi } from "../../api/services";
import type { Employee, RosterEntry, WorkShift } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const addDays = (d: Date, n: number) => { const x = new Date(d); x.setDate(x.getDate() + n); return x; };
function mondayOf(d: Date) { const x = new Date(d); x.setHours(0, 0, 0, 0); x.setDate(x.getDate() - ((x.getDay() + 6) % 7)); return x; }

export default function Shifts() {
  const { t, lang } = useLanguage();
  const [shifts, setShifts] = useState<WorkShift[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [roster, setRoster] = useState<RosterEntry[]>([]);
  const [weekStart, setWeekStart] = useState(() => mondayOf(new Date()));
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [editing, setEditing] = useState<WorkShift | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState("");
  const [start, setStart] = useState("08:00");
  const [end, setEnd] = useState("16:00");
  const [color, setColor] = useState("#2f7d6d");

  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)), [weekStart]);
  const cell = useMemo(() => {
    const m = new Map<string, RosterEntry>();
    roster.forEach((r) => m.set(`${r.employeeId}|${r.date.slice(0, 10)}`, r));
    return m;
  }, [roster]);

  async function loadShifts() { setShifts((await ShiftsApi.getAll()).data); }
  async function loadRoster() {
    setRoster((await ShiftsApi.roster(iso(weekStart), iso(addDays(weekStart, 6)))).data);
  }

  useEffect(() => {
    loadShifts().catch((e) => setError(getErrorMessage(e)));
    EmployeesApi.getAll().then((r) => setEmployees(r.data.filter((e) => e.employmentStatus === 1))).catch(() => {});
  }, []);
  useEffect(() => {
    loadRoster().catch((e) => setError(getErrorMessage(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [weekStart]);

  function openForm(shift: WorkShift | null) {
    setEditing(shift);
    setName(shift?.name ?? "");
    setStart(shift?.startTime ?? "08:00");
    setEnd(shift?.endTime ?? "16:00");
    setColor(shift?.color ?? "#2f7d6d");
    setShowForm(true);
  }

  async function saveShift(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const payload = { name, startTime: start, endTime: end, color, isActive: editing?.isActive ?? true };
      if (editing) await ShiftsApi.update(editing.id, payload);
      else await ShiftsApi.create(payload);
      setShowForm(false);
      await loadShifts();
      await loadRoster();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function toggleActive(shift: WorkShift) {
    setError(null);
    try {
      await ShiftsApi.update(shift.id, { ...shift, isActive: !shift.isActive });
      await loadShifts();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function removeShift(shift: WorkShift) {
    setError(null);
    try {
      await ShiftsApi.remove(shift.id);
      await loadShifts();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function assign(employeeId: string, day: Date, shiftId: string) {
    setError(null);
    try {
      await ShiftsApi.setAssignment(employeeId, iso(day), shiftId || null);
      await loadRoster();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function copyPrevious() {
    setError(null);
    setMessage(null);
    try {
      const n = (await ShiftsApi.copyWeek(iso(addDays(weekStart, -7)), iso(weekStart))).data;
      setMessage(t.hr.shiftCopied.replace("{n}", String(n)));
      await loadRoster();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const dayLabel = (d: Date) => d.toLocaleDateString(lang === "ar" ? "ar-EG" : "en-GB", { weekday: "short", day: "numeric", month: "numeric" });
  const activeShifts = shifts.filter((s) => s.isActive);

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.shiftsTitle}</h1>
        <button className="btn" onClick={() => (showForm ? setShowForm(false) : openForm(null))}>{showForm ? t.common.cancel : t.hr.shiftAdd}</button>
      </div>
      <p className="text-muted">{t.hr.shiftsIntro}</p>
      {error && <div className="alert-error">{error}</div>}
      {message && <div className="alert-success">{message}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={saveShift}>
            <div className="form-grid">
              <div className="form-field"><label>{t.hr.shiftName}</label><input value={name} onChange={(e) => setName(e.target.value)} required /></div>
              <div className="form-field"><label>{t.hr.shiftStart}</label><input type="time" value={start} onChange={(e) => setStart(e.target.value)} required /></div>
              <div className="form-field"><label>{t.hr.shiftEnd}</label><input type="time" value={end} onChange={(e) => setEnd(e.target.value)} required /></div>
              <div className="form-field"><label>{t.hr.shiftColor}</label><input type="color" value={color} onChange={(e) => setColor(e.target.value)} /></div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 12 }}>{t.common.save}</button>
          </form>
        </div>
      )}

      <div className="card" style={{ overflowX: "auto" }}>
        <table>
          <tbody>
            {shifts.map((s) => (
              <tr key={s.id} style={s.isActive ? undefined : { opacity: 0.55 }}>
                <td><span style={{ display: "inline-block", width: 12, height: 12, borderRadius: 3, background: s.color ?? "#888", marginInlineEnd: 8 }} />{s.name}{!s.isActive && ` (${t.hr.shiftInactive})`}</td>
                <td dir="ltr">{s.startTime} – {s.endTime}{s.isOvernight ? ` · ${t.hr.shiftOvernight}` : ""}</td>
                <td style={{ whiteSpace: "nowrap" }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => openForm(s)}>{t.common.edit}</button>{" "}
                  <button className="btn btn-secondary btn-sm" onClick={() => toggleActive(s)}>{s.isActive ? t.hr.shiftInactive : t.common.active}</button>{" "}
                  <button className="btn btn-secondary btn-sm" onClick={() => removeShift(s)}>{t.common.delete}</button>
                </td>
              </tr>
            ))}
            {shifts.length === 0 && <tr><td className="text-muted">{t.common.noData}</td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card toolbar" style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
        <button className="btn btn-secondary btn-sm" onClick={() => setWeekStart(addDays(weekStart, -7))}>{t.hr.shiftPrevWeek}</button>
        <strong>{t.hr.shiftWeekOf} {iso(weekStart)}</strong>
        <button className="btn btn-secondary btn-sm" onClick={() => setWeekStart(addDays(weekStart, 7))}>{t.hr.shiftNextWeek}</button>
        <button className="btn btn-secondary btn-sm" onClick={copyPrevious}>{t.hr.shiftCopyPrev}</button>
      </div>

      <div className="card" style={{ overflowX: "auto" }}>
        <table>
          <thead>
            <tr><th>{t.hr.employeeLabel}</th>{days.map((d) => <th key={iso(d)}>{dayLabel(d)}</th>)}</tr>
          </thead>
          <tbody>
            {employees.length === 0 && <tr><td colSpan={8} className="text-muted">{t.hr.shiftNoEmployees}</td></tr>}
            {employees.map((emp) => (
              <tr key={emp.id}>
                <td>{lang === "ar" ? emp.fullNameAr : emp.fullNameEn || emp.fullNameAr}</td>
                {days.map((d) => {
                  const current = cell.get(`${emp.id}|${iso(d)}`);
                  return (
                    <td key={iso(d)} style={{ minWidth: 120, borderTop: current?.color ? `3px solid ${current.color}` : undefined }}>
                      <select value={current?.shiftId ?? ""} onChange={(e) => assign(emp.id, d, e.target.value)}>
                        <option value="">{t.hr.shiftOff}</option>
                        {activeShifts.map((s) => <option key={s.id} value={s.id}>{s.name} {s.startTime}-{s.endTime}</option>)}
                        {current && !activeShifts.some((s) => s.id === current.shiftId) && <option value={current.shiftId}>{current.shiftName}</option>}
                      </select>
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
