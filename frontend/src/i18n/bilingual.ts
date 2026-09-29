import type { Lang } from "./translations";

/** Picks the name matching the active UI language, falling back to the other when the preferred one is blank. */
export function bilingualName(nameAr: string, nameEn: string | null | undefined, lang: Lang): string {
  return lang === "en" ? nameEn || nameAr : nameAr || nameEn || "";
}
