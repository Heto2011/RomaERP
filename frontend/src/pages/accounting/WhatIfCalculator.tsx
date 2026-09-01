import { useState } from "react";
import { FinancialReportsApi } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

const COGS_ACCOUNT_CODE = "5500";

function firstDayOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

interface Baseline {
  revenue: number;
  cogs: number;
  fixedCosts: number;
  netProfit: number;
}

export default function WhatIfCalculatorPage() {
  const { t } = useLanguage();
  const [fromDate, setFromDate] = useState(firstDayOfMonth());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [baseline, setBaseline] = useState<Baseline | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [priceChangePct, setPriceChangePct] = useState(0);
  const [volumeChangePct, setVolumeChangePct] = useState(0);
  const [cogsRateChangePct, setCogsRateChangePct] = useState(0);
  const [fixedCostsChangePct, setFixedCostsChangePct] = useState(0);

  async function load() {
    setError(null);
    try {
      const res = await FinancialReportsApi.incomeStatement(fromDate, toDate);
      const income = res.data;
      const cogs = income.expenseLines.find((l) => l.accountCode === COGS_ACCOUNT_CODE)?.amount ?? 0;
      const fixedCosts = income.totalExpense - cogs;
      setBaseline({ revenue: income.totalRevenue, cogs, fixedCosts, netProfit: income.netIncome });
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const volumeFactor = 1 + volumeChangePct / 100;
  const priceFactor = 1 + priceChangePct / 100;
  const cogsRateFactor = 1 + cogsRateChangePct / 100;
  const fixedCostsFactor = 1 + fixedCostsChangePct / 100;

  const whatIf = baseline
    ? (() => {
        const revenue = baseline.revenue * priceFactor * volumeFactor;
        const cogs = baseline.cogs * volumeFactor * cogsRateFactor;
        const fixedCosts = baseline.fixedCosts * fixedCostsFactor;
        const netProfit = revenue - cogs - fixedCosts;
        return {
          revenue,
          cogs,
          fixedCosts,
          netProfit,
          marginPercent: revenue !== 0 ? (netProfit / revenue) * 100 : 0,
        };
      })()
    : null;

  const rows: { label: string; base: number; whatIfVal: number }[] = baseline && whatIf
    ? [
        { label: t.accounting.totalRevenue, base: baseline.revenue, whatIfVal: whatIf.revenue },
        { label: t.dashboard.cogsLabel, base: baseline.cogs, whatIfVal: whatIf.cogs },
        { label: t.accounting.fixedCosts, base: baseline.fixedCosts, whatIfVal: whatIf.fixedCosts },
        { label: t.accounting.netIncome, base: baseline.netProfit, whatIfVal: whatIf.netProfit },
      ]
    : [];

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.whatIfTitle}<InfoTooltip text={t.accounting.whatIfIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.whatIfIntro}</p>

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
          {t.accounting.loadBaseline}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {baseline && (
        <>
          <div className="card" style={{ marginTop: 16 }}>
            <h3>{t.accounting.whatIfLevers}</h3>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.accounting.priceChangePct}</label>
                <input type="number" value={priceChangePct} onChange={(e) => setPriceChangePct(Number(e.target.value))} />
              </div>
              <div className="form-field">
                <label>{t.accounting.volumeChangePct}</label>
                <input type="number" value={volumeChangePct} onChange={(e) => setVolumeChangePct(Number(e.target.value))} />
              </div>
              <div className="form-field">
                <label>{t.accounting.cogsRateChangePct}</label>
                <input type="number" value={cogsRateChangePct} onChange={(e) => setCogsRateChangePct(Number(e.target.value))} />
              </div>
              <div className="form-field">
                <label>{t.accounting.fixedCostsChangePct}</label>
                <input type="number" value={fixedCostsChangePct} onChange={(e) => setFixedCostsChangePct(Number(e.target.value))} />
              </div>
            </div>
          </div>

          <div className="card" style={{ marginTop: 16 }}>
            <table>
              <thead>
                <tr>
                  <th></th>
                  <th>{t.accounting.baseline}</th>
                  <th>{t.accounting.whatIfScenario}</th>
                  <th>{t.accounting.change}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => {
                  const delta = r.base !== 0 ? ((r.whatIfVal - r.base) / Math.abs(r.base)) * 100 : null;
                  return (
                    <tr key={r.label}>
                      <td>{r.label}</td>
                      <td>{r.base.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                      <td>{r.whatIfVal.toLocaleString(undefined, { maximumFractionDigits: 0 })}</td>
                      <td className={delta === null ? "text-muted" : delta >= 0 ? "text-success" : "text-danger"}>
                        {delta === null ? "—" : `${delta >= 0 ? "+" : ""}${delta.toFixed(1)}%`}
                      </td>
                    </tr>
                  );
                })}
                <tr>
                  <td>{t.accounting.netMarginPercent}</td>
                  <td>{baseline.revenue !== 0 ? `${((baseline.netProfit / baseline.revenue) * 100).toFixed(1)}%` : "—"}</td>
                  <td>{whatIf ? `${whatIf.marginPercent.toFixed(1)}%` : "—"}</td>
                  <td></td>
                </tr>
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
