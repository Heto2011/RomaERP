import { useState, type ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import { usePortalManifest } from "../utils/pwa";
import {
  IconBox,
  IconArchive,
  IconRefresh,
  IconClock,
  IconSwap,
  IconCheck,
  IconTrendDown,
  IconBarChart,
  IconSun,
  IconMoon,
  IconMenuToggle,
} from "./icons";

/// <summary>A distinct-branded shell for the inventory/warehouse pages — same components, same data, same
/// login as the existing /inventory/* section — just its own accent color and name ("ROMA Inventory"), the
/// same treatment PeopleLayout/RestaurantPortalLayout give the HR and POS modules.</summary>
export default function InventoryPortalLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  usePortalManifest("inventory");
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const closeMobileNav = () => setMobileNavOpen(false);

  const links = [
    { to: "/inventory-portal/items", label: t.nav.items, icon: <IconBox /> },
    { to: "/inventory-portal/warehouses", label: t.nav.warehouses, icon: <IconArchive /> },
    { to: "/inventory-portal/manufacturing", label: t.inventory.manufacturingTitle, icon: <IconRefresh /> },
    { to: "/inventory-portal/expiring-stock", label: t.inventory.expiringStockTitle, icon: <IconClock /> },
    { to: "/inventory-portal/movements", label: t.nav.stockMovements, icon: <IconSwap /> },
    { to: "/inventory-portal/physical-stock-counts", label: t.inventory.physicalStockCountsTitle, icon: <IconCheck /> },
    { to: "/inventory-portal/waste-entries", label: t.inventory.wasteEntriesTitle, icon: <IconTrendDown /> },
    { to: "/inventory-portal/reports/stock-valuation", label: t.inventory.stockValuationTitle, icon: <IconBarChart /> },
    { to: "/inventory-portal/reports/movement-analysis", label: t.inventory.inventoryReports, icon: <IconBarChart /> },
    { to: "/inventory-portal/reports/purchase-price-variance", label: t.inventory.purchasePriceVarianceTitle, icon: <IconBarChart /> },
    { to: "/inventory-portal/reports/recipe-cost", label: t.inventory.navRealProductCost, icon: <IconBarChart /> },
    { to: "/inventory-portal/reports/waste-analysis", label: t.inventory.wasteAnalysisTitle, icon: <IconBarChart /> },
  ];

  return (
    <div className="app-shell inventory-theme">
      {mobileNavOpen && <div className="mobile-nav-backdrop" onClick={closeMobileNav} />}
      <aside className={"sidebar" + (mobileNavOpen ? " mobile-open" : "")}>
        <div className="sidebar-brand">
          <span className="brand-text">{t.inventoryAppName}</span>
          <div style={{ display: "flex", gap: 4 }}>
            <button className="sidebar-toggle" onClick={toggleTheme} title={theme === "dark" ? t.lightMode : t.darkMode}>
              {theme === "dark" ? <IconSun /> : <IconMoon />}
            </button>
          </div>
        </div>
        <div style={{ padding: "0 20px 12px" }}>
          <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")} title={t.language}>
            {lang === "ar" ? "EN" : "AR"}
          </button>
        </div>
        <div className="sidebar-scroll">
          {links.map((item) => (
            <NavLink key={item.to} to={item.to} onClick={closeMobileNav} className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}>
              <span className="sidebar-icon">{item.icon}</span>
              <span className="link-text">{item.label}</span>
            </NavLink>
          ))}
        </div>
        <div className="sidebar-footer">
          <div style={{ fontSize: 13, marginBottom: 8 }}>{user?.fullName}</div>
          <Link to="/" className="btn btn-secondary btn-sm" style={{ display: "block", textAlign: "center", marginBottom: 8, textDecoration: "none" }}>
            {t.backToRomaErp}
          </Link>
          <button className="btn btn-secondary btn-sm" onClick={logout}>
            {t.logout}
          </button>
        </div>
      </aside>
      <div className="main-column">
        <button
          className="btn btn-secondary btn-sm mobile-nav-toggle"
          style={{ margin: "10px 16px 0" }}
          onClick={() => setMobileNavOpen((open) => !open)}
        >
          <IconMenuToggle collapsed={!mobileNavOpen} />
        </button>
        <main className="main-content">{children}</main>
        <footer style={{ textAlign: "center", padding: "12px 0", fontSize: 12 }} className="text-muted">
          {t.poweredByRomaErpInventory}
        </footer>
      </div>
    </div>
  );
}
