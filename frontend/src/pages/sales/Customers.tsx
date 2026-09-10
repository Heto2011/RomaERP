import { useEffect, useRef, useState } from "react";
import { SalesApi } from "../../api/services";
import type { Customer } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import QuickAddBulk from "../../components/QuickAddBulk";
import { makeSequentialCodeGenerator } from "../../utils/sequentialCode";

export default function Customers() {
  const { t } = useLanguage();
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [showQuickAdd, setShowQuickAdd] = useState(false);
  const codeGenRef = useRef<() => string>(() => "");
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [taxRegistrationNumber, setTaxRegistrationNumber] = useState("");

  async function load() {
    const res = await SalesApi.getCustomers();
    setCustomers(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await SalesApi.createCustomer({ code, nameAr, nameEn, phone: phone || null, email: email || null, taxRegistrationNumber: taxRegistrationNumber || null });
      setShowForm(false);
      setCode("");
      setNameAr("");
      setNameEn("");
      setPhone("");
      setEmail("");
      setTaxRegistrationNumber("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.sales.customersTitle}</h1>
        <div style={{ display: "flex", gap: 10 }}>
          <button
            className="btn btn-secondary"
            onClick={() => {
              codeGenRef.current = makeSequentialCodeGenerator(customers.map((c) => c.code));
              setShowQuickAdd(true);
            }}
          >
            {t.common.quickAdd}
          </button>
          <button className="btn" onClick={() => setShowForm((v) => !v)}>
            {showForm ? t.common.cancel : t.sales.newCustomer}
          </button>
        </div>
      </div>

      <QuickAddBulk
        open={showQuickAdd}
        onClose={() => setShowQuickAdd(false)}
        title={t.sales.quickAddCustomersTitle}
        hint={t.sales.quickAddCustomersHint}
        placeholder={t.sales.quickAddCustomersPlaceholder}
        onCreateOne={async (name) => {
          await SalesApi.createCustomer({ code: codeGenRef.current(), nameAr: name, nameEn: name, phone: null, email: null, taxRegistrationNumber: null });
        }}
        onFinished={load}
      />

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.common.code}</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameAr}</label>
                <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameEn}</label>
                <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.phone}</label>
                <input value={phone} onChange={(e) => setPhone(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.common.email}</label>
                <input value={email} onChange={(e) => setEmail(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.common.taxNumber}</label>
                <input value={taxRegistrationNumber} onChange={(e) => setTaxRegistrationNumber(e.target.value)} />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.common.code}</th>
              <th>{t.common.nameAr}</th>
              <th>{t.common.nameEn}</th>
              <th>{t.common.phone}</th>
              <th>{t.sales.arBalance}</th>
            </tr>
          </thead>
          <tbody>
            {customers.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {customers.map((c) => (
              <tr key={c.id}>
                <td>{c.code}</td>
                <td>{c.nameAr}</td>
                <td>{c.nameEn}</td>
                <td>{c.phone ?? "-"}</td>
                <td>{c.arBalance.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
