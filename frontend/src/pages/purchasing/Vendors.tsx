import { useEffect, useRef, useState } from "react";
import { PurchasingApi } from "../../api/services";
import type { Vendor } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import QuickAddBulk from "../../components/QuickAddBulk";
import { makeSequentialCodeGenerator } from "../../utils/sequentialCode";

export default function Vendors() {
  const { t } = useLanguage();
  const [vendors, setVendors] = useState<Vendor[]>([]);
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
    const res = await PurchasingApi.getVendors();
    setVendors(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await PurchasingApi.createVendor({ code, nameAr, nameEn, phone: phone || null, email: email || null, taxRegistrationNumber: taxRegistrationNumber || null });
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
        <h1>{t.purchasing.vendorsTitle}</h1>
        <div style={{ display: "flex", gap: 10 }}>
          <button
            className="btn btn-secondary"
            onClick={() => {
              codeGenRef.current = makeSequentialCodeGenerator(vendors.map((v) => v.code), "VEND");
              setShowQuickAdd(true);
            }}
          >
            {t.common.quickAdd}
          </button>
          <button className="btn" onClick={() => setShowForm((v) => !v)}>
            {showForm ? t.common.cancel : t.purchasing.newVendor}
          </button>
        </div>
      </div>

      <QuickAddBulk
        open={showQuickAdd}
        onClose={() => setShowQuickAdd(false)}
        title={t.purchasing.quickAddVendorsTitle}
        hint={t.purchasing.quickAddVendorsHint}
        placeholder={t.purchasing.quickAddVendorsPlaceholder}
        onCreateOne={async (name) => {
          await PurchasingApi.createVendor({ code: codeGenRef.current(), nameAr: name, nameEn: name, phone: null, email: null, taxRegistrationNumber: null });
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
              <th>{t.purchasing.apBalance}</th>
            </tr>
          </thead>
          <tbody>
            {vendors.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {vendors.map((v) => (
              <tr key={v.id}>
                <td>{v.code}</td>
                <td>{v.nameAr}</td>
                <td>{v.nameEn}</td>
                <td>{v.phone ?? "-"}</td>
                <td>{v.apBalance.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
