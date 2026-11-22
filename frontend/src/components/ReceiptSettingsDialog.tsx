import { useState } from "react";
import { useLanguage } from "../i18n/LanguageContext";
import { loadReceiptSettings, saveReceiptSettings, type ReceiptSettings } from "../utils/receiptPrint";

const TEXT = {
  ar: { title: "إعدادات الطباعة (للجهاز ده)", width: "عرض الورق", auto: "اطبع الإيصال تلقائي بعد التحصيل", kitchen: "إظهار زر طباعة تذكرة المطبخ", header: "اسم المحل على الإيصال", footer: "رسالة آخر الإيصال", save: "حفظ", cancel: "إلغاء",
    tip: "الإعدادات دي محفوظة على الجهاز ده بس. لو الطابعة عندها خيار \"فتح سحّارة الكاشير\" في تعريفها، السحّارة بتفتح مع كل طباعة. ولو عايز الطباعة من غير نافذة تأكيد، شغّل المتصفح في وضع الطباعة الصامتة (kiosk printing)." },
  en: { title: "Printing settings (this device)", width: "Paper width", auto: "Print the receipt automatically after billing", kitchen: "Show the kitchen ticket button", header: "Shop name on the receipt", footer: "Message at the end of the receipt", save: "Save", cancel: "Cancel",
    tip: "These settings are kept on this device only. If the printer driver has an \"open cash drawer\" option, the drawer opens with every print. For printing without a confirmation window, run the browser in silent (kiosk) printing mode." },
};

export default function ReceiptSettingsDialog({ onClose }: { onClose: (saved?: ReceiptSettings) => void }) {
  const { lang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [s, setS] = useState<ReceiptSettings>(loadReceiptSettings());
  return (
    <div className="modal-overlay" onClick={() => onClose()}>
      <div className="card" style={{ maxWidth: 480, margin: "8% auto" }} onClick={(e) => e.stopPropagation()}>
        <h3 style={{ marginTop: 0 }}>{x.title}</h3>
        <div className="form-field"><label>{x.width}</label>
          <select value={s.widthMm} onChange={(e) => setS({ ...s, widthMm: Number(e.target.value) as 58 | 80 })}><option value={80}>80 mm</option><option value={58}>58 mm</option></select></div>
        <label style={{ display: "flex", gap: 8, margin: "8px 0" }}><input type="checkbox" checked={s.autoPrint} onChange={(e) => setS({ ...s, autoPrint: e.target.checked })} />{x.auto}</label>
        <label style={{ display: "flex", gap: 8, margin: "8px 0" }}><input type="checkbox" checked={s.kitchenTickets} onChange={(e) => setS({ ...s, kitchenTickets: e.target.checked })} />{x.kitchen}</label>
        <div className="form-field"><label>{x.header}</label><input value={s.header} maxLength={60} onChange={(e) => setS({ ...s, header: e.target.value })} /></div>
        <div className="form-field"><label>{x.footer}</label><input value={s.footer} maxLength={80} onChange={(e) => setS({ ...s, footer: e.target.value })} /></div>
        <p className="text-muted" style={{ fontSize: 13 }}>{x.tip}</p>
        <div style={{ display: "flex", gap: 10 }}>
          <button className="btn" onClick={() => { saveReceiptSettings(s); onClose(s); }}>{x.save}</button>
          <button className="btn btn-secondary" onClick={() => onClose()}>{x.cancel}</button>
        </div>
      </div>
    </div>
  );
}
