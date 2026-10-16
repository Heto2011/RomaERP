import { useEffect, useState } from "react";
import { ShiftsApi } from "../../api/services";
import type { RosterEntry } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

export default function MySchedule() {
  const { t, lang } = useLanguage();
  const [rows, setRows] = useState<RosterEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const from = new Date();
    const to = new Date();
    to.setDate(to.getDate() + 13);
    ShiftsApi.mine(iso(from), iso(to)).then((r) => setRows(r.data)).catch((e) => setError(getErrorMessage(e)));
  }, []);

  return (
    <div>
      <div className="page-header"><h1>{t.hr.mySchedule}</h1></div>
      {error && <div className="alert-error">{error}</div>}
      {rows && rows.length === 0 && <div className="card text-muted">{t.hr.myScheduleEmpty}</div>}
      {rows && rows.length > 0 && (
        <div className="card">
          <table>
            <tbody>
              {rows.map((r) => (
                <tr key={r.date}>
                  <td>{new Date(r.date).toLocaleDateString(lang === "ar" ? "ar-EG" : "en-GB", { weekday: "long", day: "numeric", month: "long" })}</td>
                  <td><span style={{ display: "inline-block", width: 12, height: 12, borderRadius: 3, background: r.color ?? "#888", marginInlineEnd: 8 }} />{r.shiftName}</td>
                  <td dir="ltr">{r.startTime} – {r.endTime}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
