import { useEffect, useRef, useState } from "react";
import { FinancialReportsApi, PurchasingApi, SalesApi } from "../../api/services";
import { PartnerStatementLineType, type PartnerStatement as PartnerStatementData } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import ReportExportBar from "../../components/ReportExportBar";

interface PartnerOption {
  id: string;
  code: string;
  nameAr: string;
}

function firstDayOfYear() {
  return `${new Date().getFullYear()}-01-01`;
}

/** One screen for both: a customer statement (what they owe us) or a vendor statement (what we owe them). */
export default function PartnerStatement({ kind }: { kind: "customer" | "vendor" }) {
  const { t } = useLanguage();
  const exportRef = useRef<HTMLDivElement>(null);
  const isCustomer = kind === "customer";
  const [partners, setPartners] = useState<PartnerOption[]>([]);
  const [partnerId, setPartnerId] = useState("");
  const [fromDate, setFromDate] = useState(firstDayOfYear());
  const [toDate, setToDate] = useState(new Date().toISOString().slice(0, 10));
  const [data, setData] = useState<PartnerStatementData | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const request = isCustomer ? SalesApi.getCustomers() : PurchasingApi.getVendors();
    request
      .then((r) => setPartners(r.data.map((p) => ({ id: p.id, code: p.code, nameAr: p.nameAr }))))
      .catch((err) => setError(getErrorMessage(err)));
  }, [isCustomer]);

  async function load() {
    if (!partnerId) return;
    setError(null);
    try {
      const res = isCustomer
        ? await FinancialReportsApi.customerStatement(partnerId, fromDate, toDate)
        : await FinancialReportsApi.vendorStatement(partnerId, fromDate, toDate);
      setData(res.data);
    } catch (err) {
      setData(null);
      setError(getErrorMessage(err));
    }
  }

  const typeLabels: Record<PartnerStatementLineType, string> = {
    [PartnerStatementLineType.Invoice]: t.accounting.statementInvoice,
    [PartnerStatementLineType.Payment]: t.accounting.statementPayment,
    [PartnerStatementLineType.CreditNote]: t.accounting.statementCreditNote,
    [PartnerStatementLineType.DebitNote]: t.accounting.statementDebitNote,
  };

  return (
    <div>
      <div className="page-header">
        <h1>{isCustomer ? t.accounting.customerStatementTitle : t.accounting.vendorStatementTitle}</h1>
      </div>

      <div className="card toolbar">
        <div className="form-field" style={{ minWidth: 260 }}>
          <label>{isCustomer ? t.accounting.selectCustomer : t.accounting.selectVendor}</label>
          <select value={partnerId} onChange={(e) => setPartnerId(e.target.value)}>
            <option value="">{isCustomer ? t.accounting.selectCustomer : t.accounting.selectVendor}</option>
            {partners.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code} - {p.nameAr}
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label>{t.common.from}</label>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div className="form-field">
          <label>{t.common.to}</label>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <button className="btn" style={{ alignSelf: "flex-end" }} onClick={load} disabled={!partnerId}>
          {t.common.viewReport}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}
      {!data && !error && <p className="text-muted">{t.accounting.pickFirstHint}</p>}

      {data && (
        <>
          <ReportExportBar targetRef={exportRef} fileName={`${kind}-statement-${data.partnerCode}`} />
          <div className="card" ref={exportRef}>
            <h3 style={{ marginTop: 0 }}>
              {data.partnerCode} - {data.partnerName}
            </h3>
            <table>
              <thead>
                <tr>
                  <th>{t.common.date}</th>
                  <th>{t.accounting.movementType}</th>
                  <th>{t.accounting.documentNumber}</th>
                  <th>{t.common.description}</th>
                  <th>{t.accounting.debit}</th>
                  <th>{t.accounting.credit}</th>
                  <th>{t.common.balance}</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td colSpan={6}>
                    <strong>{t.accounting.openingBalance}</strong>
                  </td>
                  <td>
                    <strong>{data.openingBalance.toLocaleString()}</strong>
                  </td>
                </tr>
                {data.lines.map((l, i) => (
                  <tr key={`${l.number}-${l.type}-${i}`}>
                    <td>{new Date(l.date).toLocaleDateString()}</td>
                    <td>{typeLabels[l.type]}</td>
                    <td>{l.number}</td>
                    <td>{l.description ?? ""}</td>
                    <td>{l.debit ? l.debit.toLocaleString() : ""}</td>
                    <td>{l.credit ? l.credit.toLocaleString() : ""}</td>
                    <td>{l.balance.toLocaleString()}</td>
                  </tr>
                ))}
                {data.lines.length === 0 && (
                  <tr>
                    <td colSpan={7} className="text-muted">
                      {t.accounting.noMovements}
                    </td>
                  </tr>
                )}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={4}>
                    <strong>{t.common.total}</strong>
                  </td>
                  <td>
                    <strong>{data.totalDebit.toLocaleString()}</strong>
                  </td>
                  <td>
                    <strong>{data.totalCredit.toLocaleString()}</strong>
                  </td>
                  <td>
                    <strong>
                      {t.accounting.closingBalance}: {data.closingBalance.toLocaleString()}
                    </strong>
                  </td>
                </tr>
              </tfoot>
            </table>
            <p className="text-muted" style={{ marginBottom: 0 }}>
              {t.accounting.statementNote}
            </p>
          </div>
        </>
      )}
    </div>
  );
}
