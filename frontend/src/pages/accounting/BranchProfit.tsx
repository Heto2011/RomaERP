import { useState } from "react";
import { apiClient, getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const TEXT = {
  ar: { title: "أرباح وخسائر الفروع", intro: "إيرادات ومصروفات كل فرع لوحده. «الإدارة (غير موزّع)» هي المصروفات اللي مش تابعة لفرع معيّن (مرتبات، إيجار المكتب الرئيسي، مصروفات الموردين). الإجمالي بيساوي أرباح وخسائر الشركة.",
    from: "من", to: "إلى", show: "عرض", branch: "الفرع", revenue: "الإيرادات", expense: "المصروفات", net: "الصافي", head: "الإدارة (غير موزّع)", total: "إجمالي الشركة", empty: "مفيش حركة في الفترة دي." },
  en: { title: "Branch profit and loss", intro: "Revenue and expenses of each branch on its own. “Head office (unallocated)” is spending that belongs to no branch (payroll, head-office rent, supplier expenses). The total equals the company profit and loss.",
    from: "From", to: "To", show: "Show", branch: "Branch", revenue: "Revenue", expense: "Expenses", net: "Net", head: "Head office (unallocated)", total: "Company total", empty: "No activity in this period." },
};
type Row = { warehouseId: string | null; branchName: string; revenue: number; expense: number; netProfit: number };
type Report = { branches: Row[]; totalRevenue: number; totalExpense: number; netProfit: number };
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
            <thead><tr><th>{x.branch}</th><th>{x.revenue}</th><th>{x.expense}</th><th>{x.net}</th></tr></thead>
            <tbody>
              {report.branches.map((r) => (
                <tr key={r.warehouseId ?? "head"}>
                  <td>{r.warehouseId ? r.branchName : x.head}</td><td>{money(r.revenue)}</td><td>{money(r.expense)}</td><td className={color(r.netProfit)}>{money(r.netProfit)}</td>
                </tr>
              ))}
              <tr style={{ fontWeight: 700 }}>
                <td>{x.total}</td><td>{money(report.totalRevenue)}</td><td>{money(report.totalExpense)}</td><td className={color(report.netProfit)}>{money(report.netProfit)}</td>
              </tr>
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}
