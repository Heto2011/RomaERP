import { useEffect } from "react";

interface PortalPwaConfig {
  manifestHref: string;
  themeColor: string;
  appleTouchIcon: string;
  appTitle: string;
}

const PORTAL_CONFIG: Record<"people" | "restaurant", PortalPwaConfig> = {
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
};

function upsertLink(rel: string, href: string): HTMLLinkElement {
  let el = document.head.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`);
  if (!el) {
    el = document.createElement("link");
    el.rel = rel;
    document.head.appendChild(el);
  }
  el.href = href;
  return el;
}

function upsertMeta(name: string, content: string): HTMLMetaElement {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[name="${name}"]`);
  if (!el) {
    el = document.createElement("meta");
    el.name = name;
    document.head.appendChild(el);
  }
  el.content = content;
  return el;
}

// Swaps in a portal-specific PWA manifest/icon/theme-color while its layout is mounted,
// so "Add to Home Screen" installs ROMA People / ROMA Restaurant as their own branded app
// rather than the main RomaERP one. Removed again on unmount so the main app stays plain.
export function usePortalManifest(portal: "people" | "restaurant") {
  useEffect(() => {
    const config = PORTAL_CONFIG[portal];
    const manifestLink = upsertLink("manifest", config.manifestHref);
    const appleIconLink = upsertLink("apple-touch-icon", config.appleTouchIcon);
    const themeMeta = upsertMeta("theme-color", config.themeColor);
    const capableMeta = upsertMeta("apple-mobile-web-app-capable", "yes");
    const titleMeta = upsertMeta("apple-mobile-web-app-title", config.appTitle);
    const previousTitle = document.title;
    document.title = config.appTitle;

    return () => {
      manifestLink.remove();
      appleIconLink.remove();
      themeMeta.remove();
      capableMeta.remove();
      titleMeta.remove();
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
