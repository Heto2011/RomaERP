import { useCallback, useEffect, useRef, useState } from "react";

const PROBE_URL = `${import.meta.env.VITE_API_URL ?? ""}/health`;

async function probe(): Promise<boolean> {
  if (!navigator.onLine) return false;
  try {
    const controller = new AbortController();
    const timer = window.setTimeout(() => controller.abort(), 5000);
    const response = await fetch(PROBE_URL, { cache: "no-store", signal: controller.signal });
    window.clearTimeout(timer);
    return response.ok;
  } catch {
    return false;
  }
}

/**
 * Whether the server can really be reached. The browser's own online flag only knows about the Wi-Fi link, not the internet
 * behind it, so every state change is confirmed with a quick call to the health endpoint, and an offline device re-checks every few seconds.
 */
export function useOnlineStatus(): { online: boolean; recheck: () => Promise<boolean> } {
  const [online, setOnline] = useState<boolean>(navigator.onLine);
  const onlineRef = useRef(online);
  onlineRef.current = online;

  const recheck = useCallback(async () => {
    const reachable = await probe();
    setOnline(reachable);
    return reachable;
  }, []);

  useEffect(() => {
    recheck();
    const handleOnline = () => recheck();
    const handleOffline = () => setOnline(false);
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    // Offline: look again every 8s. Online: a slow 30s check catches a link that died without the browser noticing.
    const timer = window.setInterval(() => recheck(), onlineRef.current ? 30000 : 8000);
    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
      window.clearInterval(timer);
    };
  }, [recheck, online]);

  return { online, recheck };
}
