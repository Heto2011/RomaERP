import { useEffect, useState } from "react";
import { CustomerPortalAdminApi, type PortalLinkStatus } from "../api/services";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

/** The private link a customer opens to see their own invoices and statement. The link is shown only once, right after it is made; making a new one switches the old one off. */
export default function CustomerPortalPanel({ customerId, customerName, companyCode, onClose }: { customerId: string; customerName: string; companyCode: string; onClose: () => void }) {
  const { lang } = useLanguage();
  const ar = lang === "ar";
  const [status, setStatus] = useState<PortalLinkStatus | null>(null);
  const [link, setLink] = useState<string | null>(null);
  const [days, setDays] = useState(180);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = () => CustomerPortalAdminApi.status(customerId).then((r) => setStatus(r.data)).catch((e) => setError(getErrorMessage(e)));
  useEffect(() => { load(); setLink(null); }, [customerId]); // eslint-disable-line react-hooks/exhaustive-deps

  async function make() {
    setError(null);
    try { const r = await CustomerPortalAdminApi.create(customerId, days); setLink(`${window.location.origin}/p/${encodeURIComponent(companyCode)}/${r.data.token}`); await load(); } catch (e) { setError(getErrorMessage(e)); }
  }
  async function revoke() { setError(null); try { await CustomerPortalAdminApi.revoke(customerId); setLink(null); await load(); } catch (e) { setError(getErrorMessage(e)); } }
  async function copy() { if (!link) return; try { await navigator.clipboard.writeText(link); setCopied(true); window.setTimeout(() => setCopied(false), 1500); } catch { /* ignore */ } }
  const msg = link ? (ar ? `أهلاً ${customerName}، ده رابطك الخاص لمتابعة فواتيرك وكشف حسابك:\n${link}` : `Hello ${customerName}, here is your private link to follow your invoices and statement:\n${link}`) : "";

  return (
    <div className="card" style={{ maxWidth: 720, marginBottom: 16 }}>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
        <strong>{ar ? "بوابة العميل" : "Customer portal"}: {customerName}</strong>
        <button className="btn btn-secondary btn-sm" onClick={onClose}>{ar ? "إغلاق" : "Close"}</button>
      </div>
      <p className="text-muted" style={{ margin: "6px 0" }}>
        {ar ? "رابط خاص بيفتح للعميل فواتيره وكشف حسابه بس، من غير تسجيل دخول. الرابط بيظهر مرة واحدة وقت الإنشاء، ولو عملت رابط جديد القديم بيقف." : "A private link that opens only this customer's invoices and statement, with no sign-in. The link is shown once when it is made, and a new one switches the old one off."}
      </p>
      {error && <div className="alert-error">{error}</div>}
      {status && (
        <p style={{ margin: "6px 0" }}>
          {status.hasActiveLink
            ? (ar ? `فيه رابط شغّال لحد ${status.expiresAtUtc?.slice(0, 10)}، اتفتح ${status.viewCount} مرة${status.lastViewedAtUtc ? ` وآخر مرة ${status.lastViewedAtUtc.slice(0, 10)}` : ""}.` : `An active link until ${status.expiresAtUtc?.slice(0, 10)}, opened ${status.viewCount} time(s).`)
            : (ar ? "مفيش رابط شغّال." : "No active link.")}
        </p>
      )}
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
        <label>{ar ? "صالح لمدة (يوم)" : "Valid for (days)"}</label>
        <input type="number" min={1} max={365} value={days} onChange={(e) => setDays(Number(e.target.value) || 180)} style={{ width: 90 }} />
        <button className="btn" onClick={make}>{status?.hasActiveLink ? (ar ? "رابط جديد (يوقّف القديم)" : "New link (stops the old one)") : (ar ? "اعمل رابط" : "Make a link")}</button>
        {status?.hasActiveLink && <button className="btn btn-secondary" onClick={revoke}>{ar ? "أوقف الرابط" : "Switch it off"}</button>}
      </div>
      {link && (
        <div style={{ marginTop: 10 }}>
          <div className="text-muted" style={{ fontSize: 13 }}>{ar ? "انسخه دلوقتي، مش هيظهر تاني:" : "Copy it now, it will not be shown again:"}</div>
          <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
            <input readOnly value={link} style={{ flex: 1, direction: "ltr" }} onFocus={(e) => e.target.select()} />
            <button className="btn btn-secondary btn-sm" onClick={copy}>{copied ? "✓" : "📋"}</button>
            <a className="btn btn-secondary btn-sm" href={`https://wa.me/?text=${encodeURIComponent(msg)}`} target="_blank" rel="noreferrer">{ar ? "ابعته واتساب" : "Send on WhatsApp"}</a>
          </div>
        </div>
      )}
    </div>
  );
}
