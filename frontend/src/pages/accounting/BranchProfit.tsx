import { useState } from "react";
import { apiClient, getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: { title: "أرباح وخسائر الفروع", intro: "مرحلتين: الأولى مساهمة كل فرع (إيراداته ناقص تكلفة بضاعته ومصروفاته الخاصة)، والتانية بنوزّع عليه نصيبه من المصروفات المشتركة (مرتبات، إيجار المكتب الرئيسي، مصروفات الموردين) بنسبة مبيعاته. الإجمالي بيساوي أرباح وخسائر الشركة.",
    from: "من", to: "إلى", show: "عرض", branch: "الفرع", revenue: "المبيعات", share: "نسبة المبيعات", cogs: "تكلفة البضاعة", cogsPct: "تكلفة البضاعة %", gross: "مجمل الربح",
    direct: "مصروفات الفرع", contrib: "مساهمة الفرع", contribPct: "المساهمة %", shared: "نصيبه من المصروفات المشتركة", net: "الصافي بعد التوزيع", head: "الإدارة",
    total: "إجمالي الشركة", empty: "مفيش حركة في الفترة دي.", stage1: "المرحلة 1: أداء الفرع نفسه", stage2: "المرحلة 2: بعد توزيع المشترك",
    unalloc: "مصروفات مشتركة ماقدرناش نوزّعها (مفيش مبيعات للفروع في الفترة):", basis: "أساس التوزيع: نسبة مبيعات كل فرع من إجمالي مبيعات الفروع." },
  en: { title: "Branch profit and loss", intro: "Two stages: first each branch's contribution (its revenue less its cost of goods and its own expenses), then its share of the shared spending (payroll, head-office rent, supplier expenses) by its share of sales. The total equals the company profit and loss.",
    from: "From", to: "To", show: "Show", branch: "Branch", revenue: "Sales", share: "Sales share", cogs: "Cost of goods", cogsPct: "Cost of goods %", gross: "Gross profit",
    direct: "Branch expenses", contrib: "Branch contribution", contribPct: "Contribution %", shared: "Share of shared costs", net: "Net after sharing", head: "Head office",
    total: "Company total", empty: "No activity in this period.", stage1: "Stage 1: the branch on its own", stage2: "Stage 2: after sharing",
    unalloc: "Shared spending that could not be allocated (no branch sales in the period):", basis: "Allocation basis: each branch's share of total branch sales." },
};
type Row = { warehouseId: string | null; branchName: string; revenue: number; salesSharePercent: number; costOfGoods: number; costOfGoodsPercent: number; grossProfit: number; otherDirectExpenses: number; contribution: number; contributionPercent: number; sharedAllocated: number; netProfit: number };
type Report = { branches: Row[]; unallocatedShared: number; totalRevenue: number; totalCostOfGoods: number; totalDirectExpenses: number; totalShared: number; netProfit: number; sharedCostsToAllocate: number };
const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function BranchProfit() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [fromDate, setFromDate] = useState(`${new Date().getFullYear()}-01-01`);
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<Report | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setError(null);
    try { setReport((await apiClient.get<Report>("/financialreports/branch-profit", { params: { fromDate, toDate } })).data); }
    catch (e) { setError(getErrorMessage(e)); }
  }
  const color = (n: number) => (n < 0 ? "text-danger" : n > 0 ? "text-success" : "");
  const pct = (n: number) => `${n.toLocaleString(undefined, { maximumFractionDigits: 1 })}%`;
  const tot = report ? report.totalRevenue - report.totalCostOfGoods - report.totalDirectExpenses : 0;

  return (
    <div>
      <div className="page-header"><h1>{x.title}</h1></div>
      <p className="text-muted">{x.intro}</p>
      <div className="card toolbar">
        <div className="form-field"><label>{x.from}</label><input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} /></div>
        <div className="form-field"><label>{x.to}</label><input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} /></div>
        <button className="btn" onClick={load}>{x.show}</button>
      </div>
      {error && <div className="alert-error">{error}</div>}
      {report && (report.branches.length === 0 ? <div className="card text-muted">{x.empty}</div> : (
        <div className="card" style={{ overflowX: "auto" }}>
          <table>
            <thead>
              <tr><th rowSpan={2}>{x.branch}</th><th colSpan={7} style={{ textAlign: "center" }}>{x.stage1}</th><th colSpan={2} style={{ textAlign: "center" }}>{x.stage2}</th></tr>
              <tr><th>{x.revenue}</th><th>{x.cogs}</th><th>{x.cogsPct}</th><th>{x.gross}</th><th>{x.direct}</th><th>{x.contrib}</th><th>{x.contribPct}</th><th>{x.shared}</th><th>{x.net}</th></tr>
            </thead>
            <tbody>
              {report.branches.map((r) => (
                <tr key={r.warehouseId ?? "head"}>
                  <td>{r.warehouseId ? r.branchName : x.head}{r.warehouseId ? <span className="text-muted"> ({pct(r.salesSharePercent)})</span> : null}</td>
                  <td>{money(r.revenue)}</td><td>{money(r.costOfGoods)}</td>
                  <td className={r.costOfGoodsPercent >= 40 ? "text-danger" : ""}>{r.warehouseId ? pct(r.costOfGoodsPercent) : "—"}</td>
                  <td>{money(r.grossProfit)}</td><td>{money(r.otherDirectExpenses)}</td>
                  <td className={color(r.contribution)}>{money(r.contribution)}</td><td>{r.warehouseId ? pct(r.contributionPercent) : "—"}</td>
                  <td>{money(r.sharedAllocated)}</td><td className={color(r.netProfit)}><strong>{money(r.netProfit)}</strong></td>
                </tr>
              ))}
              <tr style={{ fontWeight: 700 }}>
                <td>{x.total}</td><td>{money(report.totalRevenue)}</td><td>{money(report.totalCostOfGoods)}</td>
                <td>{report.totalRevenue ? pct((report.totalCostOfGoods / report.totalRevenue) * 100) : "—"}</td>
                <td>{money(report.totalRevenue - report.totalCostOfGoods)}</td><td>{money(report.totalDirectExpenses)}</td>
                <td className={color(tot)}>{money(tot)}</td><td>{report.totalRevenue ? pct((tot / report.totalRevenue) * 100) : "—"}</td>
                <td>{money(report.totalShared)}</td><td className={color(report.netProfit)}>{money(report.netProfit)}</td>
              </tr>
            </tbody>
          </table>
          <p className="text-muted" style={{ fontSize: 13 }}>{x.basis}</p>
          {report.unallocatedShared !== 0 && <p className="text-danger">{x.unalloc} {money(report.unallocatedShared)}</p>}
        </div>
      ))}
    </div>
  );
}
