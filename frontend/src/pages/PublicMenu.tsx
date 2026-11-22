import { useEffect, useState } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import axios from "axios";
import { useLanguage } from "../i18n/LanguageContext";

type Item = { nameAr: string; nameEn: string; price: number; openPrice: boolean };
type Category = { category: string; items: Item[] };
type Menu = { shopNameAr: string; shopNameEn: string; table: string | null; categories: Category[] };

const TEXT = {
  ar: { menu: "المنيو", table: "طاولة", loading: "جاري تحميل المنيو...", error: "مقدرناش نفتح المنيو. جرّب تاني بعد شوية.", open: "السعر حسب الطلب", empty: "المنيو لسه فاضي.", hint: "لما تحب تطلب، نادي على الجرسون." },
  en: { menu: "Menu", table: "Table", loading: "Loading the menu...", error: "We could not open the menu. Please try again shortly.", open: "Price on request", empty: "The menu is empty for now.", hint: "When you are ready to order, please call your waiter." },
};

/** What a diner sees after scanning the QR on a table. No login; it only shows names, prices and categories. */
export default function PublicMenu() {
  const { companyCode = "" } = useParams();
  const [search] = useSearchParams();
  const { lang, setLang } = useLanguage();
  const x = TEXT[lang === "ar" ? "ar" : "en"];
  const [menu, setMenu] = useState<Menu | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    // A plain request (not the app's client) so no signed-in person's token is ever sent from this public page.
    axios.get<Menu>("/api/public-menu", { params: { table: search.get("table") ?? undefined }, headers: { "X-Company-Code": companyCode } })
      .then((r) => setMenu(r.data)).catch(() => setError(true));
  }, [companyCode, search]);

  return (
    <div style={{ maxWidth: 640, margin: "0 auto", padding: "16px 16px 48px" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8 }}>
        <h1 style={{ margin: 0, fontSize: 24 }}>{menu ? (lang === "ar" ? menu.shopNameAr || menu.shopNameEn : menu.shopNameEn || menu.shopNameAr) : x.menu}</h1>
        <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")}>{lang === "ar" ? "EN" : "AR"}</button>
      </div>
      {menu?.table && <div className="text-muted" style={{ marginTop: 4 }}>{x.table} {menu.table}</div>}
      {!menu && !error && <p className="text-muted">{x.loading}</p>}
      {error && <p className="alert-error">{x.error}</p>}
      {menu && menu.categories.length === 0 && <p className="text-muted">{x.empty}</p>}
      {menu?.categories.map((c) => (
        <section key={c.category} style={{ marginTop: 22 }}>
          <h2 style={{ fontSize: 18, borderBottom: "1px solid currentColor", paddingBottom: 4, opacity: 0.9 }}>{c.category}</h2>
          {c.items.map((i, k) => (
            <div key={k} style={{ display: "flex", justifyContent: "space-between", gap: 12, padding: "8px 0" }}>
              <span>{lang === "ar" ? i.nameAr || i.nameEn : i.nameEn || i.nameAr}</span>
              <strong style={{ whiteSpace: "nowrap" }}>{i.openPrice ? x.open : i.price.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 })}</strong>
            </div>
          ))}
        </section>
      ))}
      {menu && menu.categories.length > 0 && <p className="text-muted" style={{ marginTop: 28 }}>{x.hint}</p>}
    </div>
  );
}
