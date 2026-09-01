import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import { ManualProfitDimension, RestaurantOrderType, type SalesChannelProfitabilityReport } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";
import ManualProfitGrid from "../../components/ManualProfitGrid";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function SalesChannelProfitabilityPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<SalesChannelProfitabilityReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  const channelLabel: Record<RestaurantOrderType, string> = {
    [RestaurantOrderType.DineIn]: t.restaurant.dineIn,
    [RestaurantOrderType.Takeaway]: t.restaurant.takeaway,
    [RestaurantOrderType.Delivery]: t.restaurant.delivery,
  };

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.salesChannelProfitability(fromDate, toDate);
      setReport(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.salesChannelProfitabilityTitle}<InfoTooltip text={t.accounting.salesChannelProfitabilityIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.salesChannelProfitabilityIntro}</p>

      <div className="card toolbar">
        <div className="form-field">
          <label>{t.common.from}</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.common.to}</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {report && report.channels.length === 0 && <div className="card text-muted">{t.common.noData}</div>}

      {report && report.channels.length > 0 && (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>{t.restaurant.orderType}</th>
                <th>{t.accounting.revenue}</th>
                <th>{t.accounting.cost}</th>
                <th>{t.accounting.grossProfit}</th>
                <th>{t.accounting.margin}</th>
              </tr>
            </thead>
            <tbody>
              {report.channels.map((c) => (
                <tr key={c.channel}>
                  <td>{channelLabel[c.channel]}</td>
                  <td>{c.revenue.toLocaleString()}</td>
                  <td>{c.cost.toLocaleString()}</td>
                  <td className={c.grossProfit >= 0 ? "text-success" : "text-danger"}>{c.grossProfit.toLocaleString()}</td>
                  <td>{c.marginPercent.toFixed(1)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h3>{t.accounting.otherChannelsManual}</h3>
      <ManualProfitGrid dimension={ManualProfitDimension.Channel} nameLabel={t.accounting.channelName} />
    </div>
  );
}
