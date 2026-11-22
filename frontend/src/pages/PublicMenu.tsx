import { useEffect, useState } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import axios from "axios";
import { useLanguage } from "../i18n/LanguageContext";

type Item = { id: string; nameAr: string; nameEn: string; price: number; openPrice: boolean };
type Category = { category: string; items: Item[] };
type Menu = { shopNameAr: string; shopNameEn: string; table: string | null; categories: Category[]; canOrder: boolean; radiusMeters: number };
type Sent = { id: string; status: number };

const TEXT = {
  ar: {
    menu: "المنيو", table: "طاولة", loading: "جاري تحميل المنيو...", error: "مقدرناش نفتح المنيو. جرّب تاني بعد شوية.", open: "السعر حسب الطلب", empty: "المنيو لسه فاضي.",
    hint: "لما تحب تطلب، نادي على الجرسون.", order: "أرسل الطلب", cart: "طلبك", total: "الإجمالي", note: "ملاحظة (اختياري)", add: "إضافة", askCashier: "اطلبه من الكاشير",
    locating: "بنحدد موقعك...", locDenied: "لازم تسمح بتحديد الموقع علشان نتأكد إنك جوه المطعم.", sending: "بنبعت الطلب...",
    waiting: "طلبك وصل للكاشير. استنى لحد ما يوافق عليه.", approved: "الطلب اتقبل وراح للمطبخ. بالهنا!", rejected: "الكاشير ما قدرش يقبل الطلب. كلّمه مباشرة.", another: "طلب جديد",
    privacy: "موقعك بيُستخدم مرة واحدة للتأكد إنك جوه المطعم، ومش بيتخزن.",
  },
  en: {
    menu: "Menu", table: "Table", loading: "Loading the menu...", error: "We could not open the menu. Please try again shortly.", open: "Price on request", empty: "The menu is empty for now.",
    hint: "When you are ready to order, please call your waiter.", order: "Send order", cart: "Your order", total: "Total", note: "Note (optional)", add: "Add", askCashier: "Ask the cashier",
    locating: "Finding your location...", locDenied: "Please allow location so we can confirm you are inside the restaurant.", sending: "Sending your order...",
    waiting: "Your order reached the cashier. Please wait for it to be accepted.", approved: "Your order was accepted and sent to the kitchen. Enjoy!", rejected: "The cashier could not accept this order. Please speak to them directly.", another: "New order",
    privacy: "Your location is used once to confirm you are inside the restaurant and is not stored.",
  },
};

/** A random id kept on this phone so guests behind the same Wi-Fi are not counted as one device. */
function deviceId(): string {
  try {
    let id = localStorage.getItem("qrDeviceId");
    if (!id) { id = crypto.randomUUID(); localStorage.setItem("qrDeviceId", id); }
    return id;
  } catch { return ""; }
}

const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 });

/** What a diner sees after scanning the QR on a table. When ordering is on for that table the diner can send an order from the phone;
 *  it only counts once the cashier accepts it, and only from inside the restaurant. */
export default function PublicMenu() {
  const { companyCode = "" } = useParams();
  const [search] = useSearchParams();
  const { lang, setLang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [menu, setMenu] = useState<Menu | null>(null);
  const [error, setError] = useState(false);
  const [cart, setCart] = useState<Record<string, number>>({});
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [sent, setSent] = useState<Sent | null>(null);
  const table = search.get("table");
  const code = search.get("k");
  const headers = { "X-Company-Code": companyCode };

  useEffect(() => {
    // A plain request (not the app's client) so no signed-in person's token is ever sent from this public page.
    axios.get<Menu>("/api/public-menu", { params: { table: table ?? undefined, k: code ?? undefined }, headers })
      .then((r) => setMenu(r.data)).catch(() => setError(true));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyCode, table, code]);

  // After sending, keep asking whether the cashier accepted it.
  useEffect(() => {
    if (!sent || sent.status !== 1) return;
    const h = window.setInterval(() => {
      axios.get<{ status: number }>(`/api/public-menu/order/${sent.id}`, { headers })
        .then((r) => setSent((s) => (s ? { ...s, status: r.data.status } : s))).catch(() => {});
    }, 5000);
    return () => window.clearInterval(h);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sent]);

  const allItems = menu?.categories.flatMap((c) => c.items) ?? [];
  const lines = allItems.filter((i) => (cart[i.id] ?? 0) > 0);
  const total = lines.reduce((s, i) => s + i.price * cart[i.id], 0);
  const change = (id: string, d: number) => setCart((c) => ({ ...c, [id]: Math.max(0, Math.min(20, (c[id] ?? 0) + d)) }));

  function send() {
    setProblem(null);
    if (!navigator.geolocation) { setProblem(x.locDenied); return; }
    setBusy(x.locating);
    navigator.geolocation.getCurrentPosition(async (pos) => {
      setBusy(x.sending);
      try {
        const r = await axios.post<{ id: string }>("/api/public-menu/order", {
          table, code, deviceId: deviceId(), latitude: pos.coords.latitude, longitude: pos.coords.longitude, accuracy: pos.coords.accuracy, note: note || null,
          lines: lines.map((i) => ({ itemId: i.id, quantity: cart[i.id], notes: null })),
        }, { headers });
        setSent({ id: r.data.id, status: 1 });
        setCart({}); setNote("");
      } catch (e) {
        const msg = axios.isAxiosError(e) ? (e.response?.data?.message ?? e.response?.data?.error ?? e.response?.data?.title) : null;
        setProblem(typeof msg === "string" ? msg : x.error);
      } finally { setBusy(null); }
    }, () => { setBusy(null); setProblem(x.locDenied); }, { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 });
  }

  const canOrder = !!menu?.canOrder;
  return (
    <div style={{ maxWidth: 640, margin: "0 auto", padding: "16px 16px 120px" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8 }}>
        <h1 style={{ margin: 0, fontSize: 24 }}>{menu ? (lang === "ar" ? menu.shopNameAr || menu.shopNameEn : menu.shopNameEn || menu.shopNameAr) : x.menu}</h1>
        <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")}>{lang === "ar" ? "EN" : "AR"}</button>
      </div>
      {menu?.table && <div className="text-muted" style={{ marginTop: 4 }}>{x.table} {menu.table}</div>}
      {!menu && !error && <p className="text-muted">{x.loading}</p>}
      {error && <p className="alert-error">{x.error}</p>}
      {menu && menu.categories.length === 0 && <p className="text-muted">{x.empty}</p>}

      {sent && (
        <div className={sent.status === 3 ? "alert-error" : "alert-success"} style={{ marginTop: 14 }}>
          <div>{sent.status === 2 ? x.approved : sent.status === 3 ? x.rejected : x.waiting}</div>
          {sent.status !== 1 && <button className="btn btn-secondary btn-sm" style={{ marginTop: 8 }} onClick={() => setSent(null)}>{x.another}</button>}
        </div>
      )}

      {menu?.categories.map((c) => (
        <section key={c.category} style={{ marginTop: 22 }}>
          <h2 style={{ fontSize: 18, borderBottom: "1px solid currentColor", paddingBottom: 4, opacity: 0.9 }}>{c.category}</h2>
          {c.items.map((i) => (
            <div key={i.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, padding: "8px 0" }}>
              <span style={{ flex: 1 }}>{lang === "ar" ? i.nameAr || i.nameEn : i.nameEn || i.nameAr}</span>
              <strong style={{ whiteSpace: "nowrap" }}>{i.openPrice ? x.open : money(i.price)}</strong>
              {canOrder && !sent && (i.openPrice ? <span className="text-muted" style={{ fontSize: 12 }}>{x.askCashier}</span> : (cart[i.id] ?? 0) === 0
                ? <button className="btn btn-sm" onClick={() => change(i.id, 1)}>{x.add}</button>
                : <span style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>
                    <button className="btn btn-secondary btn-sm" aria-label="-" onClick={() => change(i.id, -1)}>−</button>
                    <strong>{cart[i.id]}</strong>
                    <button className="btn btn-secondary btn-sm" aria-label="+" onClick={() => change(i.id, 1)}>+</button>
                  </span>)}
            </div>
          ))}
        </section>
      ))}

      {menu && menu.categories.length > 0 && !canOrder && <p className="text-muted" style={{ marginTop: 28 }}>{x.hint}</p>}

      {canOrder && !sent && lines.length > 0 && (
        <div style={{ position: "fixed", insetInline: 0, bottom: 0, background: "var(--surface, #fff)", color: "inherit", borderTop: "1px solid rgba(128,128,128,.4)", padding: 12 }}>
          <div style={{ maxWidth: 640, margin: "0 auto" }}>
            <strong>{x.cart}: {lines.reduce((s, i) => s + cart[i.id], 0)} · {x.total} {money(total)}</strong>
            <input type="text" maxLength={300} placeholder={x.note} value={note} onChange={(e) => setNote(e.target.value)} style={{ width: "100%", margin: "8px 0" }} />
            {problem && <div className="alert-error" style={{ marginBottom: 8 }}>{problem}</div>}
            <button className="btn" style={{ width: "100%" }} disabled={!!busy} onClick={send}>{busy ?? x.order}</button>
            <div className="text-muted" style={{ fontSize: 12, marginTop: 6 }}>{x.privacy}</div>
          </div>
        </div>
      )}
    </div>
  );
}
