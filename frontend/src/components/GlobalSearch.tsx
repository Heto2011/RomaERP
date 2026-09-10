import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ItemsApi, PurchasingApi, SalesApi } from "../api/services";
import { useLanguage } from "../i18n/LanguageContext";

interface SearchRow {
  id: string;
  code: string;
  name: string;
  to: string;
}

export default function GlobalSearch() {
  const { t, lang } = useLanguage();
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [customers, setCustomers] = useState<SearchRow[]>([]);
  const [vendors, setVendors] = useState<SearchRow[]>([]);
  const [items, setItems] = useState<SearchRow[]>([]);
  const loadedRef = useRef(false);
  const containerRef = useRef<HTMLDivElement>(null);

  function loadOnce() {
    if (loadedRef.current) return;
    loadedRef.current = true;
    SalesApi.getCustomers().then((r) =>
      setCustomers(r.data.map((c) => ({ id: c.id, code: c.code, name: lang === "ar" ? c.nameAr : c.nameEn, to: "/sales/customers" })))
    );
    PurchasingApi.getVendors().then((r) =>
      setVendors(r.data.map((v) => ({ id: v.id, code: v.code, name: lang === "ar" ? v.nameAr : v.nameEn, to: "/purchasing/vendors" })))
    );
    ItemsApi.getAll().then((r) =>
      setItems(r.data.map((i) => ({ id: i.id, code: i.code, name: lang === "ar" ? i.nameAr : i.nameEn, to: "/inventory/items" })))
    );
  }

  useEffect(() => {
    function onOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    }
    function onEscape(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", onOutside);
    document.addEventListener("keydown", onEscape);
    return () => {
      document.removeEventListener("mousedown", onOutside);
      document.removeEventListener("keydown", onEscape);
    };
  }, []);

  const q = query.trim().toLowerCase();
  const match = (row: SearchRow) => !q || row.name.toLowerCase().includes(q) || row.code.toLowerCase().includes(q);
  const groups: { label: string; rows: SearchRow[] }[] = q
    ? [
        { label: t.nav.customers, rows: customers.filter(match).slice(0, 5) },
        { label: t.nav.vendors, rows: vendors.filter(match).slice(0, 5) },
        { label: t.nav.items, rows: items.filter(match).slice(0, 5) },
      ].filter((g) => g.rows.length > 0)
    : [];

  function goTo(row: SearchRow) {
    navigate(row.to);
    setOpen(false);
    setQuery("");
  }

  return (
    <div className="global-search" ref={containerRef}>
      <input
        type="search"
        value={query}
        placeholder={t.common.search}
        onFocus={() => {
          loadOnce();
          setOpen(true);
        }}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
      />
      {open && q && (
        <div className="global-search-dropdown">
          {groups.length === 0 && <div className="global-search-empty">{t.common.noData}</div>}
          {groups.map((g) => (
            <div key={g.label}>
              <div className="global-search-group-label">{g.label}</div>
              {g.rows.map((row) => (
                <button key={row.id} type="button" className="global-search-result" onClick={() => goTo(row)}>
                  <span>{row.name}</span>
                  <span className="text-muted">{row.code}</span>
                </button>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
