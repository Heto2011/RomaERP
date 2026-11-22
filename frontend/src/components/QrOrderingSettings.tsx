import { useEffect, useState } from "react";
import { QrOrdersApi, WarehousesApi, type QrOrderingSettings as Settings } from "../api/services";
import type { Warehouse } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const TEXT = {
  ar: {
    title: "الطلب من موبايل العميل", intro: "العميل يصوّر كود الطاولة ويطلب من موبايله. الطلب بيتقبل من جوه المطعم بس، وبيوصل للكاشير يوافق عليه قبل ما يروح المطبخ.",
    enabled: "تفعيل الطلب من الموبايل", warehouse: "الفرع اللي الطلبات تتسحب منه", choose: "اختار...", radius: "أقصى مسافة من المطعم (متر)",
    radiusHint: "50 متر مناسبة. أقل من كده الـGPS جوه المبنى ممكن يرفض عملاء موجودين فعلاً.",
    location: "موقع المطعم", setHere: "استخدم موقعي الحالي (وأنا جوه المطعم)", noLocation: "لسه ما اتحددش", hasLocation: "اتحدد",
    save: "حفظ", saved: "اتحفظ.", rotate: "تغيير كل الأكواد", rotateWarn: "كل الأكواد المطبوعة هتبطل تشتغل وهتحتاج تطبع الأكواد الجديدة. تكمل؟", rotated: "الأكواد اتغيّرت. اطبع الأكواد الجديدة.",
    locFail: "مقدرناش نحدد موقعك. اسمح للمتصفح بالموقع وجرّب تاني.",
  },
  en: {
    title: "Ordering from the guest's phone", intro: "Guests scan the table code and order from their phone. Orders are only accepted from inside the restaurant, and the cashier must approve each one before it reaches the kitchen.",
    enabled: "Allow ordering from phones", warehouse: "Branch the orders are taken from", choose: "Choose...", radius: "Furthest distance from the restaurant (metres)",
    radiusHint: "50 m works well. Much smaller and indoor GPS may turn away guests who really are inside.",
    location: "Restaurant location", setHere: "Use my current location (while inside the restaurant)", noLocation: "Not set yet", hasLocation: "Set",
    save: "Save", saved: "Saved.", rotate: "Change all codes", rotateWarn: "Every printed code will stop working and you will need to print the new ones. Continue?", rotated: "Codes changed. Print the new ones.",
    locFail: "Could not get your location. Allow location in the browser and try again.",
  },
};

/** The owner's switch and safety settings for phone ordering. */
export default function QrOrderingSettings() {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [s, setS] = useState<Settings | null>(null);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);

  useEffect(() => {
    QrOrdersApi.getSettings().then((r) => setS(r.data)).catch(() => setS(null));
    WarehousesApi.getAll().then((r) => setWarehouses(r.data.filter((w) => w.isActive))).catch(() => setWarehouses([]));
  }, []);
  if (!s) return null;

  const fail = (e: unknown) => setMsg({ ok: false, text: getErrorMessage(e) });

  function useHere() {
    setMsg(null);
    navigator.geolocation.getCurrentPosition(
      async (p) => { try { setS((await QrOrdersApi.setLocation(p.coords.latitude, p.coords.longitude)).data); setMsg({ ok: true, text: x.saved }); } catch (e) { fail(e); } },
      () => setMsg({ ok: false, text: x.locFail }),
      { enableHighAccuracy: true, timeout: 20000 });
  }

  async function save() {
    setMsg(null);
    try { setS((await QrOrdersApi.saveSettings(s!)).data); setMsg({ ok: true, text: x.saved }); } catch (e) { fail(e); }
  }

  async function rotate() {
    if (!window.confirm(x.rotateWarn)) return;
    try { await QrOrdersApi.rotate(); setMsg({ ok: true, text: x.rotated }); } catch (e) { fail(e); }
  }

  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <h3 style={{ marginTop: 0 }}>{x.title}</h3>
      <p className="text-muted">{x.intro}</p>
      {msg && <div className={msg.ok ? "alert-success" : "alert-error"}>{msg.text}</div>}
      <div className="form-grid">
        <div className="form-field">
          <label><input type="checkbox" checked={s.enabled} onChange={(e) => setS({ ...s, enabled: e.target.checked })} /> {x.enabled}</label>
        </div>
        <div className="form-field">
          <label>{x.warehouse}</label>
          <select value={s.warehouseId ?? ""} onChange={(e) => setS({ ...s, warehouseId: e.target.value || null })}>
            <option value="">{x.choose}</option>
            {warehouses.map((w) => <option key={w.id} value={w.id}>{lang === "ar" ? w.nameAr : w.nameEn || w.nameAr}</option>)}
          </select>
        </div>
        <div className="form-field">
          <label>{x.radius}</label>
          <input type="number" min={10} max={500} value={s.radiusMeters} onChange={(e) => setS({ ...s, radiusMeters: Number(e.target.value) })} />
          <small className="text-muted">{x.radiusHint}</small>
        </div>
        <div className="form-field">
          <label>{x.location}: <strong>{s.hasLocation ? x.hasLocation : x.noLocation}</strong></label>
          <button type="button" className="btn btn-secondary btn-sm" onClick={useHere}>{x.setHere}</button>
        </div>
      </div>
      <div style={{ display: "flex", gap: 8, marginTop: 10 }}>
        <button className="btn" onClick={save}>{x.save}</button>
        <button className="btn btn-secondary" onClick={rotate}>{x.rotate}</button>
      </div>
    </div>
  );
}
