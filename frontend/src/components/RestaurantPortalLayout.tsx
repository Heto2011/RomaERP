import HelpButton from "./HelpButton";
import FeedbackButton from "./FeedbackButton";
import { useState, type ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import { usePortalManifest } from "../utils/pwa";
import { IconCart, IconCalendar, IconGrid, IconStar, IconBook, IconTruck, IconSwap, IconRefresh, IconClock, IconSun, IconMoon, IconMenuToggle, IconBox, IconArchive, IconCheck, IconTrendDown, IconBarChart } from "./icons";

/// <summary>A distinct-branded shell for the restaurant/POS pages — same components, same data, same
/// login as the existing /restaurant/* section — just its own accent color and name ("ROMA Restaurant")
/// so it reads as its own product, the same treatment PeopleLayout gives the HR module.</summary>
export default function RestaurantPortalLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  usePortalManifest("restaurant");
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const closeMobileNav = () => setMobileNavOpen(false);

  const links = [
    { to: "/restaurant-portal/reservations", label: t.nav.restaurantReservations, icon: <IconCalendar /> },
    { to: "/restaurant-portal/tables", label: t.nav.restaurantTables, icon: <IconGrid /> },
    { to: "/restaurant-portal/loyalty", label: t.nav.restaurantLoyalty, icon: <IconStar /> },
    { to: "/restaurant-portal/menu", label: t.nav.restaurantMenu, icon: <IconBook /> },
    { to: "/restaurant-portal/kitchen", label: t.nav.kitchenDisplay, icon: <IconClock /> },
    { to: "/restaurant-portal/purchase-receiving", label: t.restaurant.purchaseReceivingTitle, icon: <IconTruck /> },
    { to: "/restaurant-portal/delivery-reconciliation", label: t.inventory.deliveryReconciliationTitle, icon: <IconSwap /> },
    { to: "/restaurant-portal/delivery-platforms", label: t.restaurant.deliveryPlatformsTitle, icon: <IconRefresh /> },
  ];

  // Inventory is part of the restaurant/POS product: its screens sit here, in the same page, under their own heading.
  const inventoryLinks = [
    { to: "/restaurant-portal/inventory/items", label: t.nav.items, icon: <IconBox /> },
    { to: "/restaurant-portal/inventory/warehouses", label: t.nav.warehouses, icon: <IconArchive /> },
    { to: "/restaurant-portal/inventory/transfers", label: t.nav.stockTransfers, icon: <IconSwap /> },
    { to: "/restaurant-portal/inventory/adjustments", label: t.nav.stockAdjustments, icon: <IconSwap /> },
    { to: "/restaurant-portal/inventory/warehouse-stock", label: t.nav.warehouseStock, icon: <IconArchive /> },
    { to: "/restaurant-portal/inventory/branch-minimums", label: t.nav.branchMinimums, icon: <IconArchive /> },
    { to: "/restaurant-portal/inventory/movements", label: t.nav.stockMovements, icon: <IconSwap /> },
    { to: "/restaurant-portal/inventory/physical-stock-counts", label: t.inventory.physicalStockCountsTitle, icon: <IconCheck /> },
    { to: "/restaurant-portal/inventory/waste-entries", label: t.inventory.wasteEntriesTitle, icon: <IconTrendDown /> },
    { to: "/restaurant-portal/inventory/manufacturing", label: t.inventory.manufacturingTitle, icon: <IconRefresh /> },
    { to: "/restaurant-portal/inventory/expiring-stock", label: t.inventory.expiringStockTitle, icon: <IconClock /> },
    { to: "/restaurant-portal/inventory/reports/stock-valuation", label: t.inventory.stockValuationTitle, icon: <IconBarChart /> },
    { to: "/restaurant-portal/inventory/reports/reorder-suggestions", label: t.inventory.reorderSuggestionsTitle, icon: <IconBarChart /> },
    { to: "/restaurant-portal/inventory/reports/movement-analysis", label: t.inventory.inventoryReports, icon: <IconBarChart /> },
    { to: "/restaurant-portal/inventory/reports/waste-analysis", label: t.inventory.wasteAnalysisTitle, icon: <IconBarChart /> },
  ];

  return (
    <div className="app-shell restaurant-theme">
      {mobileNavOpen && <div className="mobile-nav-backdrop" onClick={closeMobileNav} />}
      <aside className={"sidebar" + (mobileNavOpen ? " mobile-open" : "")}>
        <div className="sidebar-brand">
          <span className="brand-text">{t.restaurantAppName}</span>
          <div style={{ display: "flex", gap: 4 }}>
            <HelpButton />
            <FeedbackButton />
            <button className="sidebar-toggle" onClick={toggleTheme} title={theme === "dark" ? t.lightMode : t.darkMode}>
              {theme === "dark" ? <IconSun /> : <IconMoon />}
            </button>
            <Link to="/" className="sidebar-toggle" title={t.backToRomaErp} aria-label={t.backToRomaErp} style={{ display: "inline-flex", alignItems: "center", justifyContent: "center" }}>
                <img src="/roma-logo.png" alt="" width={22} height={22} style={{ borderRadius: 6 }} />
              </Link>
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
          <Link to="/restaurant/pos" className="sidebar-link" onClick={closeMobileNav}>
            <span className="sidebar-icon"><IconCart /></span>
            <span className="link-text">{t.nav.restaurantPos}</span>
          </Link>
          <div className="text-muted" style={{ padding: "14px 20px 4px", fontSize: 12, fontWeight: 700 }}>{t.nav.inventory}</div>
          {inventoryLinks.map((item) => (
            <NavLink key={item.to} to={item.to} onClick={closeMobileNav} className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}>
              <span className="sidebar-icon">{item.icon}</span>
              <span className="link-text">{item.label}</span>
            </NavLink>
          ))}
        </div>
        <div className="sidebar-footer">
          <div style={{ fontSize: 13, marginBottom: 8 }}>{user?.fullName}</div>
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
          {t.poweredByRomaErpRestaurant}
        </footer>
      </div>
    </div>
  );
}
