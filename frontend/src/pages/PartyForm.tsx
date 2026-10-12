import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { PurchasingApi, SalesApi } from "../api/services";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import { makeSequentialCodeGenerator } from "../utils/sequentialCode";

type Kind = "vendor" | "customer";

interface FormState {
  code: string;
  nameAr: string;
  nameEn: string;
  phone: string;
  email: string;
  taxRegistrationNumber: string;
  contactPerson: string;
  mobile: string;
  address: string;
  city: string;
  commercialRegisterNumber: string;
  paymentTermsDays: string;
  creditLimit: string;
  bankName: string;
  bankAccountNumber: string;
  notes: string;
  isActive: boolean;
}

const empty: FormState = {
  code: "", nameAr: "", nameEn: "", phone: "", email: "", taxRegistrationNumber: "", contactPerson: "", mobile: "",
  address: "", city: "", commercialRegisterNumber: "", paymentTermsDays: "0", creditLimit: "0", bankName: "", bankAccountNumber: "",
  notes: "", isActive: true,
};

const orNull = (v: string) => (v.trim() === "" ? null : v.trim());

/** One screen to register — or edit — a vendor or a customer with every detail. Same layout for both. */
export default function PartyForm({ kind }: { kind: Kind }) {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const { id } = useParams();
  const isVendor = kind === "vendor";
  const listPath = isVendor ? "/purchasing/vendors" : "/sales/customers";

  const [form, setForm] = useState<FormState>(empty);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => setForm((f) => ({ ...f, [key]: value }));

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const all = isVendor ? (await PurchasingApi.getVendors()).data : (await SalesApi.getCustomers()).data;
        if (cancelled) return;
        if (id) {
          const found = all.find((p) => p.id === id);
          if (found) {
            const bank = found as { bankName?: string | null; bankAccountNumber?: string | null };
            setForm({
              code: found.code, nameAr: found.nameAr, nameEn: found.nameEn, phone: found.phone ?? "", email: found.email ?? "",
              taxRegistrationNumber: found.taxRegistrationNumber ?? "", contactPerson: found.contactPerson ?? "", mobile: found.mobile ?? "",
              address: found.address ?? "", city: found.city ?? "", commercialRegisterNumber: found.commercialRegisterNumber ?? "",
              paymentTermsDays: String(found.paymentTermsDays ?? 0), creditLimit: String(found.creditLimit ?? 0),
              bankName: bank.bankName ?? "", bankAccountNumber: bank.bankAccountNumber ?? "", notes: found.notes ?? "", isActive: found.isActive,
            });
          }
        } else {
          setForm({ ...empty, code: makeSequentialCodeGenerator(all.map((p) => p.code))() });
        }
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [id, isVendor]);

  const title = useMemo(() => {
    if (isVendor) return id ? t.purchasing.partyEditVendor : t.purchasing.partyNewVendorFull;
    return id ? t.purchasing.partyEditCustomer : t.purchasing.partyNewCustomerFull;
  }, [id, isVendor, t]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    const common = {
      nameAr: form.nameAr.trim(),
      nameEn: form.nameEn.trim() || form.nameAr.trim(),
      phone: orNull(form.phone),
      email: orNull(form.email),
      taxRegistrationNumber: orNull(form.taxRegistrationNumber),
      contactPerson: orNull(form.contactPerson),
      mobile: orNull(form.mobile),
      address: orNull(form.address),
      city: orNull(form.city),
      commercialRegisterNumber: orNull(form.commercialRegisterNumber),
      notes: orNull(form.notes),
      paymentTermsDays: Math.max(0, Math.round(Number(form.paymentTermsDays) || 0)),
      creditLimit: Math.max(0, Number(form.creditLimit) || 0),
    };
    try {
      if (isVendor) {
        const vendor = { ...common, bankName: orNull(form.bankName), bankAccountNumber: orNull(form.bankAccountNumber) };
        if (id) await PurchasingApi.updateVendor(id, { ...vendor, isActive: form.isActive });
        else await PurchasingApi.createVendor({ ...vendor, code: form.code.trim() });
      } else {
        if (id) await SalesApi.updateCustomer(id, { ...common, isActive: form.isActive });
        else await SalesApi.createCustomer({ ...common, code: form.code.trim() });
      }
      navigate(listPath);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <div className="text-muted">{t.common.loading}</div>;

  const field = (label: string, key: keyof FormState, opts: { type?: string; required?: boolean; readOnly?: boolean; dir?: string; step?: string } = {}) => (
    <div className="form-field">
      <label>{label}</label>
      <input
        type={opts.type ?? "text"}
        step={opts.step}
        dir={opts.dir}
        value={String(form[key])}
        readOnly={opts.readOnly}
        required={opts.required}
        onChange={(e) => set(key, e.target.value as never)}
      />
    </div>
  );

  return (
    <div>
      <div className="page-header">
        <h1>{title}</h1>
        <button className="btn btn-secondary" type="button" onClick={() => navigate(listPath)}>{t.common.cancel}</button>
      </div>
      {error && <div className="alert-error">{error}</div>}

      <form onSubmit={handleSubmit}>
        <div className="card">
          <div className="form-grid">
            {field(t.common.code, "code", { required: true, readOnly: !!id })}
            {field(t.common.nameAr, "nameAr", { required: true })}
            {field(t.common.nameEn, "nameEn")}
            {field(t.common.taxNumber, "taxRegistrationNumber", { dir: "ltr" })}
            {field(t.purchasing.partyCommercialRegister, "commercialRegisterNumber", { dir: "ltr" })}
          </div>
          {!id && <p className="text-muted" style={{ marginBottom: 0 }}>{t.purchasing.partyCodeAuto}</p>}
        </div>

        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.purchasing.partySectionContact}</h3>
          <div className="form-grid">
            {field(t.purchasing.partyContactPerson, "contactPerson")}
            {field(t.common.phone, "phone", { dir: "ltr" })}
            {field(t.purchasing.partyMobile, "mobile", { dir: "ltr" })}
            {field(t.common.email, "email", { type: "email", dir: "ltr" })}
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.purchasing.partySectionAddress}</h3>
          <div className="form-grid">
            {field(t.purchasing.partyAddress, "address")}
            {field(t.purchasing.partyCity, "city")}
          </div>
        </div>

        <div className="card">
          <h3 style={{ marginTop: 0 }}>{t.purchasing.partySectionCredit}</h3>
          <div className="form-grid">
            {field(t.purchasing.partyPaymentTermsDays, "paymentTermsDays", { type: "number", step: "1" })}
            {field(t.purchasing.partyCreditLimit, "creditLimit", { type: "number", step: "0.01" })}
          </div>
          <p className="text-muted" style={{ marginBottom: 0 }}>{t.purchasing.partyCreditLimitHint}</p>
        </div>

        {isVendor && (
          <div className="card">
            <h3 style={{ marginTop: 0 }}>{t.purchasing.partySectionBank}</h3>
            <div className="form-grid">
              {field(t.purchasing.partyBankName, "bankName")}
              {field(t.purchasing.partyBankAccount, "bankAccountNumber", { dir: "ltr" })}
            </div>
          </div>
        )}

        <div className="card">
          <div className="form-field">
            <label>{t.common.notes}</label>
            <textarea rows={3} value={form.notes} onChange={(e) => set("notes", e.target.value)} />
          </div>
          {id && (
            <label style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: "normal", marginTop: 10 }}>
              <input type="checkbox" checked={form.isActive} onChange={(e) => set("isActive", e.target.checked)} />
              {t.common.active}
            </label>
          )}
        </div>

        <button className="btn" type="submit" disabled={saving}>{t.common.save}</button>
      </form>
    </div>
  );
}
