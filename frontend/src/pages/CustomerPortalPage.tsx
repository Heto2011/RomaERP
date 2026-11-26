import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { systemApiClient, getErrorMessage } from "../api/client";

type PortalInvoice = { id: string; invoiceNumber: string; invoiceDate: string; totalAmount: number; paidAmount: number; outstandingAmount: number; currencyCode: string };
type StatementLine = { date: string; type: number; number: string; description: string | null; debit: number; credit: number; balance: number };
type Portal = { companyName: string; customerName: string; balance: number; currency: string; invoices: PortalInvoice[]; statement: { openingBalance: number; lines: StatementLine[]; closingBalance: number; fromDate: string; toDate: string } };

const T = {
  ar: { title: "فواتيرك وكشف حسابك", balance: "الرصيد المستحق", invoices: "الفواتير", statement: "كشف الحساب (آخر 12 شهر)", no: "الرقم", date: "التاريخ", total: "الإجمالي", paid: "المدفوع", due: "المتبقي", pdf: "تحميل PDF", print: "طباعة", opening: "رصيد أول الفترة", closing: "الرصيد الحالي", desc: "البيان", debit: "مدين", credit: "دائن", bal: "الرصيد", none: "مفيش فواتير.", loading: "جاري التحميل...", bad: "الرابط غير صالح أو انتهى. اطلب رابط جديد من الشركة.", note: "الصفحة دي خاصة بيك. متشاركش الرابط مع حد." },
  en: { title: "Your invoices and statement", balance: "Amount due", invoices: "Invoices", statement: "Statement (last 12 months)", no: "No.", date: "Date", total: "Total", paid: "Paid", due: "Due", pdf: "Download PDF", print: "Print", opening: "Opening balance", closing: "Closing balance", desc: "Description", debit: "Debit", credit: "Credit", bal: "Balance", none: "No invoices.", loading: "Loading...", bad: "This link is not valid or has expired. Ask the company for a new one.", note: "This page is private to you. Do not share the link.", },
};
const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 });

/** What a customer sees from their private link: their own invoices and statement, no sign-in. */
export default function CustomerPortalPage() {
  const { companyCode, token } = useParams();
  const lang = (() => { try { return localStorage.getItem("lang") === "en" ? "en" : "ar"; } catch { return "ar"; } })();
  const x = T[lang];
  const [data, setData] = useState<Portal | null>(null);
  const [error, setError] = useState<string | null>(null);
  const headers = { "X-Company-Code": companyCode ?? "", "X-Portal-Token": token ?? "" };

  useEffect(() => {
    document.documentElement.lang = lang; document.documentElement.dir = lang === "ar" ? "rtl" : "ltr";
    const meta = document.createElement("meta"); meta.name = "robots"; meta.content = "noindex,nofollow"; document.head.appendChild(meta);
    systemApiClient.get<Portal>("/portal/me", { headers }).then((r) => setData(r.data)).catch((e) => setError(getErrorMessage(e) || x.bad));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyCode, token]);

  async function pdf(id: string, no: string) {
    try {
      const r = await systemApiClient.get<Blob>(`/portal/invoices/${id}/pdf`, { headers, responseType: "blob" });
      const url = URL.createObjectURL(r.data); const a = document.createElement("a"); a.href = url; a.download = `${no}.pdf`; a.click(); URL.revokeObjectURL(url);
    } catch (e) { setError(getErrorMessage(e)); }
  }

  const wrap: React.CSSProperties = { maxWidth: 860, margin: "0 auto", padding: 16, fontFamily: "sans-serif", color: "var(--color-text, #1c2b2a)" };
  if (error) return <div style={wrap}><div className="alert-error">{error}</div></div>;
  if (!data) return <div style={wrap}><p>{x.loading}</p></div>;
  return (
    <div style={wrap}>
      <h1 style={{ marginBottom: 4 }}>{data.companyName}</h1>
      <p className="text-muted" style={{ marginTop: 0 }}>{x.title} — <strong>{data.customerName}</strong></p>
      <div className="card" style={{ marginBottom: 16 }}>
        <div className="text-muted">{x.balance}</div>
        <div style={{ fontSize: 28, fontWeight: 700 }}>{money(data.balance)} {data.currency}</div>
        <button className="btn btn-secondary btn-sm" style={{ marginTop: 8 }} onClick={() => window.print()}>{x.print}</button>
      </div>
      <div className="card" style={{ overflowX: "auto", marginBottom: 16 }}>
        <strong>{x.invoices}</strong>
        {data.invoices.length === 0 ? <p className="text-muted">{x.none}</p> : (
          <table>
            <thead><tr><th>{x.no}</th><th>{x.date}</th><th>{x.total}</th><th>{x.paid}</th><th>{x.due}</th><th /></tr></thead>
            <tbody>{data.invoices.map((i) => (
              <tr key={i.id}><td>{i.invoiceNumber}</td><td>{i.invoiceDate.slice(0, 10)}</td><td>{money(i.totalAmount)}</td><td>{money(i.paidAmount)}</td><td>{money(i.outstandingAmount)}</td>
                <td><button className="btn btn-secondary btn-sm" onClick={() => pdf(i.id, i.invoiceNumber)}>{x.pdf}</button></td></tr>
            ))}</tbody>
          </table>
        )}
      </div>
      <div className="card" style={{ overflowX: "auto", marginBottom: 16 }}>
        <strong>{x.statement}</strong>
        <table>
          <thead><tr><th>{x.date}</th><th>{x.no}</th><th>{x.desc}</th><th>{x.debit}</th><th>{x.credit}</th><th>{x.bal}</th></tr></thead>
          <tbody>
            <tr><td colSpan={5}>{x.opening}</td><td>{money(data.statement.openingBalance)}</td></tr>
            {data.statement.lines.map((l, k) => (<tr key={k}><td>{l.date.slice(0, 10)}</td><td>{l.number}</td><td>{l.description ?? ""}</td><td>{l.debit ? money(l.debit) : ""}</td><td>{l.credit ? money(l.credit) : ""}</td><td>{money(l.balance)}</td></tr>))}
            <tr><td colSpan={5}><strong>{x.closing}</strong></td><td><strong>{money(data.statement.closingBalance)}</strong></td></tr>
          </tbody>
        </table>
      </div>
      <p className="text-muted" style={{ fontSize: 13 }}>{x.note}</p>
    </div>
  );
}
