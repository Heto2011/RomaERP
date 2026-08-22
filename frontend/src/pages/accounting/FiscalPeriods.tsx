import { useEffect, useState } from "react";
import { FiscalPeriodsAdminApi } from "../../api/services";
import type { FiscalYearDetail } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function FiscalPeriods() {
  const { t } = useLanguage();
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
    if (!confirm(t.accounting.closeYearConfirm)) return;
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
        <h1>{t.accounting.fiscalPeriodsTitle}</h1>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {years.map((year) => (
        <div className="card" key={year.id}>
          <div className="toolbar" style={{ justifyContent: "space-between" }}>
            <div>
              <strong>{year.name}</strong>{" "}
              <span className={`badge ${year.isClosed ? "badge-reversed" : "badge-posted"}`}>
                {year.isClosed ? t.accounting.closed : t.accounting.openStatus}
              </span>
            </div>
            {!year.isClosed && (
              <button className="btn" onClick={() => handleCloseYear(year.id)}>
                {t.accounting.closeYear}
              </button>
            )}
          </div>

          <table>
            <thead>
              <tr>
                <th>{t.accounting.period}</th>
                <th>{t.common.from}</th>
                <th>{t.common.to}</th>
                <th>{t.common.status}</th>
                <th>{t.common.actions}</th>
              </tr>
            </thead>
            <tbody>
              {year.periods.map((p) => (
                <tr key={p.id}>
                  <td>{p.name}</td>
                  <td>{new Date(p.startDate).toLocaleDateString()}</td>
                  <td>{new Date(p.endDate).toLocaleDateString()}</td>
                  <td>
                    <span className={`badge ${p.isClosed ? "badge-reversed" : "badge-posted"}`}>
                      {p.isClosed ? t.accounting.closed : t.accounting.openStatus}
                    </span>
                  </td>
                  <td>
                    {!p.isClosed && (
                      <button className="btn btn-sm" onClick={() => handleClosePeriod(p.id)}>
                        {t.accounting.closePeriod}
                      </button>
                    )}
                    {p.isClosed && !year.isClosed && (
                      <button className="btn btn-secondary btn-sm" onClick={() => handleReopenPeriod(p.id)}>
                        {t.accounting.reopenPeriod}
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
