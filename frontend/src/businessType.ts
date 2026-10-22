import { BusinessType } from "./api/types";

/** Sections of the top menu keyed by what they are, so a business can hide the ones it will never use. */
export type NavSectionKey = "restaurant" | "inventory";

const ALL: BusinessType[] = [
  BusinessType.Restaurant, BusinessType.Retail, BusinessType.Services,
  BusinessType.Wholesale, BusinessType.Manufacturing, BusinessType.Contracting,
];

/** Sections each activity needs. Everything else (accounting, sales, purchasing, HR...) is shown to all. */
const needs: Record<number, NavSectionKey[]> = {
  [BusinessType.Restaurant]: ["restaurant", "inventory"],
  [BusinessType.Retail]: ["inventory"],
  [BusinessType.Services]: [],
  [BusinessType.Wholesale]: ["inventory"],
  [BusinessType.Manufacturing]: ["inventory"],
  [BusinessType.Contracting]: ["inventory"],
};

/** The sections a company with these activities does not need. Nothing is locked: "Show all sections" brings
 *  them back. A section is hidden only when none of the chosen activities needs it; nothing chosen hides nothing. */
export function hiddenSectionsFor(types: BusinessType | undefined): NavSectionKey[] {
  const chosen = ALL.filter((t) => ((types ?? 0) & t) !== 0);
  if (chosen.length === 0) return [];
  const used = new Set(chosen.flatMap((t) => needs[t]));
  return (["restaurant", "inventory"] as NavSectionKey[]).filter((k) => !used.has(k));
}

export const SHOW_ALL_KEY = "showAllSections";
