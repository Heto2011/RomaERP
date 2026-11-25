import type { Item } from "../api/types";

/** The item a scanned (or typed) code belongs to: its barcode first, then its item code. Inactive items are not offered. */
export function findItemByCode(items: Item[], raw: string): Item | undefined {
  const c = raw.trim().toLowerCase();
  if (!c) return undefined;
  const live = items.filter((i) => i.isActive);
  return live.find((i) => (i.barcode ?? "").toLowerCase() === c) ?? live.find((i) => i.code.toLowerCase() === c);
}
