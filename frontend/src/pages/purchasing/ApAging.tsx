import { useEffect, useState } from "react";
import { PurchasingApi } from "../../api/services";
import type { VendorAging } from "../../api/types";
import { useLanguage } from "../../i18n/LanguageContext";

export default function ApAging() {
  const { t } = useLanguage();
  const [rows, setRows] = useState<VendorAging[]>([]);

  useEffect(() => {
    PurchasingApi.getAging().then((r) => setRows(r.data));
  }, []);

  const totals = rows.reduce(
    (acc, r) => ({
      totalOutstanding: acc.totalOutstanding + r.totalOutstanding,
      current: acc.current + r.current,
      days31To60: acc.days31To60 + r.days31To60,
      days61To90: acc.days61To90 + r.days61To90,
      over90Days: acc.over90Days + r.over90Days,
    }),
    { totalOutstanding: 0, current: 0, days31To60: 0, days61To90: 0, over90Days: 0 }
  );

  return (
    <div>
      <div className="page-header">
        <h1>{t.purchasing.agingTitle}</h1>
      </div>

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.purchasing.vendor}</th>
              <th>{t.common.agingCurrent}</th>
              <th>{t.common.aging31to60}</th>
              <th>{t.common.aging61to90}</th>
              <th>{t.common.agingOver90}</th>
              <th>{t.common.total}</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {rows.map((r) => (
              <tr key={r.vendorId}>
                <td>{r.vendorCode} - {r.vendorName}</td>
                <td>{r.current.toLocaleString()}</td>
                <td className={r.days31To60 > 0 ? "text-danger" : undefined}>{r.days31To60.toLocaleString()}</td>
                <td className={r.days61To90 > 0 ? "text-danger" : undefined}>{r.days61To90.toLocaleString()}</td>
                <td className={r.over90Days > 0 ? "text-danger" : undefined}>{r.over90Days.toLocaleString()}</td>
                <td><strong>{r.totalOutstanding.toLocaleString()}</strong></td>
              </tr>
            ))}
          </tbody>
          {rows.length > 0 && (
            <tfoot>
              <tr>
                <td><strong>{t.common.total}</strong></td>
                <td><strong>{totals.current.toLocaleString()}</strong></td>
                <td><strong>{totals.days31To60.toLocaleString()}</strong></td>
                <td><strong>{totals.days61To90.toLocaleString()}</strong></td>
                <td><strong>{totals.over90Days.toLocaleString()}</strong></td>
                <td><strong>{totals.totalOutstanding.toLocaleString()}</strong></td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}
