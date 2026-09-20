import { useEffect } from "react";

interface PortalPwaConfig {
  manifestHref: string;
  themeColor: string;
  appleTouchIcon: string;
  appTitle: string;
}

const PORTAL_CONFIG: Record<"people" | "restaurant" | "inventory", PortalPwaConfig> = {
  people: {
    manifestHref: "/manifest-people.json",
    themeColor: "#c2703d",
    appleTouchIcon: "/icons/people-apple-touch.png",
    appTitle: "ROMA People",
  },
  restaurant: {
    manifestHref: "/manifest-restaurant.json",
    themeColor: "#c1442d",
    appleTouchIcon: "/icons/restaurant-apple-touch.png",
    appTitle: "ROMA Restaurant",
  },
  inventory: {
    manifestHref: "/manifest-inventory.json",
    themeColor: "#5b4b9e",
    appleTouchIcon: "/icons/inventory-apple-touch.png",
    appTitle: "ROMA Inventory",
  },
};

// Swaps an attribute on an existing (or newly created) tag, returning a restore function
// that puts back whatever was there before — an empty string means "remove the tag", since
// it didn't exist prior to the swap.
function swapLink(rel: string, href: string): () => void {
  let el = document.head.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`);
  const existed = !!el;
  const previousHref = el?.href ?? "";
  if (!el) {
    el = document.createElement("link");
    el.rel = rel;
    document.head.appendChild(el);
  }
  el.href = href;
  const target = el;
  return () => {
    if (existed) target.href = previousHref;
    else target.remove();
  };
}

function swapMeta(name: string, content: string): () => void {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[name="${name}"]`);
  const existed = !!el;
  const previousContent = el?.content ?? "";
  if (!el) {
    el = document.createElement("meta");
    el.name = name;
    document.head.appendChild(el);
  }
  el.content = content;
  const target = el;
  return () => {
    if (existed) target.content = previousContent;
    else target.remove();
  };
}

// Swaps in a portal-specific PWA manifest/icon/theme-color while its layout is mounted, so
// "Add to Home Screen" installs ROMA People / ROMA Restaurant as their own branded app rather
// than the main RomaERP one. Restores whatever the main app's own tags had on unmount, rather
// than deleting them outright.
export function usePortalManifest(portal: "people" | "restaurant" | "inventory") {
  useEffect(() => {
    const config = PORTAL_CONFIG[portal];
    const restoreManifest = swapLink("manifest", config.manifestHref);
    const restoreAppleIcon = swapLink("apple-touch-icon", config.appleTouchIcon);
    const restoreTheme = swapMeta("theme-color", config.themeColor);
    const restoreCapable = swapMeta("apple-mobile-web-app-capable", "yes");
    const restoreTitle = swapMeta("apple-mobile-web-app-title", config.appTitle);
    const previousTitle = document.title;
    document.title = config.appTitle;

    return () => {
      restoreManifest();
      restoreAppleIcon();
      restoreTheme();
      restoreCapable();
      restoreTitle();
      document.title = previousTitle;
    };
  }, [portal]);
}

export function registerServiceWorker() {
  if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("/sw.js").catch(() => {
      // Installability is a nice-to-have; a failed registration shouldn't break the app.
    });
  }
}
