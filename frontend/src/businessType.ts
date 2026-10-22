import { BusinessType } from "./api/types";

/** Sections of the top menu keyed by what they are, so a business type can hide the ones it will never use. */
export type NavSectionKey = "restaurant" | "inventory" | "purchasing";

/** What each business type does NOT see by default. Nothing is locked: "Show all sections" brings everything back. */
export const hiddenSectionsFor: Record<BusinessType, NavSectionKey[]> = {
  [BusinessType.General]: [],
  [BusinessType.Restaurant]: [],
  [BusinessType.Retail]: ["restaurant"],
  [BusinessType.Services]: ["restaurant", "inventory"],
  [BusinessType.Wholesale]: ["restaurant"],
  [BusinessType.Manufacturing]: ["restaurant"],
  [BusinessType.Contracting]: ["restaurant"],
};

export const SHOW_ALL_KEY = "showAllSections";
