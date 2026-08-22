import { useEffect, useState } from "react";
import { FiscalPeriodsAdminApi } from "../../api/services";
import type { FiscalYearDetail } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function FiscalPeriods() {
  const [years, setYears] = useState<FiscalYearDetail[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const res = await FiscalPeriodsAdminApi.getAllYears();
    setYears(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleClosePeriod(id: string) {
    setError(null);
    try {
      await FiscalPeriodsAdminApi.closePeriod(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleReopenPeriod(id: string) {
    setError(null);
    try {
      await FiscalPeriodsAdminApi.reopenPeriod(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCloseYear(id: string) {
    setError(null);
    if (!confirm("إقفال السنة المالية سينشئ قيد إقفال يصفّر الإيرادات والمصروفات ويرحّل صافي الربح/الخسارة للأرباح المرحلة. متأكد؟")) return;
    try {
      await FiscalPeriodsAdminApi.closeYear(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>إقفال الفترات المالية</h1>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {years.map((year) => (
        <div className="card" key={year.id}>
          <div className="toolbar" style={{ justifyContent: "space-between" }}>
            <div>
              <strong>{year.name}</strong>{" "}
              <span className={`badge ${year.isClosed ? "badge-reversed" : "badge-posted"}`}>
                {year.isClosed ? "مقفلة" : "مفتوحة"}
              </span>
            </div>
            {!year.isClosed && (
              <button className="btn" onClick={() => handleCloseYear(year.id)}>
                إقفال السنة المالية
              </button>
            )}
          </div>

          <table>
            <thead>
              <tr>
                <th>الفترة</th>
                <th>من</th>
                <th>إلى</th>
                <th>الحالة</th>
                <th>إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {year.periods.map((p) => (
                <tr key={p.id}>
                  <td>{p.name}</td>
                  <td>{new Date(p.startDate).toLocaleDateString("ar-EG")}</td>
                  <td>{new Date(p.endDate).toLocaleDateString("ar-EG")}</td>
                  <td>
                    <span className={`badge ${p.isClosed ? "badge-reversed" : "badge-posted"}`}>
                      {p.isClosed ? "مقفلة" : "مفتوحة"}
                    </span>
                  </td>
                  <td>
                    {!p.isClosed && (
                      <button className="btn btn-sm" onClick={() => handleClosePeriod(p.id)}>
                        إقفال
                      </button>
                    )}
                    {p.isClosed && !year.isClosed && (
                      <button className="btn btn-secondary btn-sm" onClick={() => handleReopenPeriod(p.id)}>
                        فتح
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}
