import { type ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useTheme } from "../context/ThemeContext";
import { useLanguage } from "../i18n/LanguageContext";
import { IconCart, IconGrid, IconBook, IconTruck, IconSwap, IconRefresh, IconClock, IconSun, IconMoon } from "./icons";

/// <summary>A distinct-branded shell for the restaurant/POS pages — same components, same data, same
/// login as the existing /restaurant/* section — just its own accent color and name ("ROMA Restaurant")
/// so it reads as its own product, the same treatment PeopleLayout gives the HR module.</summary>
export default function RestaurantPortalLayout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const { t, lang, setLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();

  const links = [
    { to: "/restaurant-portal/tables", label: t.nav.restaurantTables, icon: <IconGrid /> },
    { to: "/restaurant-portal/menu", label: t.nav.restaurantMenu, icon: <IconBook /> },
    { to: "/restaurant-portal/kitchen", label: t.nav.kitchenDisplay, icon: <IconClock /> },
    { to: "/restaurant-portal/purchase-receiving", label: t.restaurant.purchaseReceivingTitle, icon: <IconTruck /> },
    { to: "/restaurant-portal/delivery-reconciliation", label: t.inventory.deliveryReconciliationTitle, icon: <IconSwap /> },
    { to: "/restaurant-portal/delivery-platforms", label: t.restaurant.deliveryPlatformsTitle, icon: <IconRefresh /> },
  ];

  return (
    <div className="app-shell restaurant-theme">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="brand-text">{t.restaurantAppName}</span>
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
            <NavLink key={item.to} to={item.to} className={({ isActive }) => "sidebar-link" + (isActive ? " active" : "")}>
              <span className="sidebar-icon">{item.icon}</span>
              <span className="link-text">{item.label}</span>
            </NavLink>
          ))}
          <Link to="/restaurant/pos" className="sidebar-link">
            <span className="sidebar-icon"><IconCart /></span>
            <span className="link-text">{t.nav.restaurantPos}</span>
          </Link>
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
        <main className="main-content">{children}</main>
        <footer style={{ textAlign: "center", padding: "12px 0", fontSize: 12 }} className="text-muted">
          {t.poweredByRomaErpRestaurant}
        </footer>
      </div>
    </div>
  );
}
