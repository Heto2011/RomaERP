// ISO 3166-1 alpha-2 code + Arabic/English display names. Gulf and Arab countries first (our main markets),
// then the nationalities most common among expatriate workers there.
export interface Country {
  code: string;
  ar: string;
  en: string;
}

export const COUNTRIES: Country[] = [
  { code: "SA", ar: "السعودية", en: "Saudi Arabia" },
  { code: "EG", ar: "مصر", en: "Egypt" },
  { code: "AE", ar: "الإمارات", en: "United Arab Emirates" },
  { code: "KW", ar: "الكويت", en: "Kuwait" },
  { code: "QA", ar: "قطر", en: "Qatar" },
  { code: "BH", ar: "البحرين", en: "Bahrain" },
  { code: "OM", ar: "عُمان", en: "Oman" },
  { code: "JO", ar: "الأردن", en: "Jordan" },
  { code: "LB", ar: "لبنان", en: "Lebanon" },
  { code: "SY", ar: "سوريا", en: "Syria" },
  { code: "IQ", ar: "العراق", en: "Iraq" },
  { code: "PS", ar: "فلسطين", en: "Palestine" },
  { code: "YE", ar: "اليمن", en: "Yemen" },
  { code: "SD", ar: "السودان", en: "Sudan" },
  { code: "LY", ar: "ليبيا", en: "Libya" },
  { code: "TN", ar: "تونس", en: "Tunisia" },
  { code: "DZ", ar: "الجزائر", en: "Algeria" },
  { code: "MA", ar: "المغرب", en: "Morocco" },
  { code: "IN", ar: "الهند", en: "India" },
  { code: "PK", ar: "باكستان", en: "Pakistan" },
  { code: "BD", ar: "بنغلاديش", en: "Bangladesh" },
  { code: "PH", ar: "الفلبين", en: "Philippines" },
  { code: "ID", ar: "إندونيسيا", en: "Indonesia" },
  { code: "LK", ar: "سريلانكا", en: "Sri Lanka" },
  { code: "NP", ar: "نيبال", en: "Nepal" },
  { code: "ET", ar: "إثيوبيا", en: "Ethiopia" },
  { code: "KE", ar: "كينيا", en: "Kenya" },
  { code: "TR", ar: "تركيا", en: "Türkiye" },
  { code: "GB", ar: "المملكة المتحدة", en: "United Kingdom" },
  { code: "US", ar: "الولايات المتحدة", en: "United States" },
  { code: "CA", ar: "كندا", en: "Canada" },
  { code: "FR", ar: "فرنسا", en: "France" },
  { code: "DE", ar: "ألمانيا", en: "Germany" },
  { code: "IT", ar: "إيطاليا", en: "Italy" },
  { code: "ES", ar: "إسبانيا", en: "Spain" },
  { code: "XX", ar: "جنسية أخرى", en: "Other" },
];

export function countryName(code: string | null | undefined, lang: string): string {
  if (!code) return "";
  const c = COUNTRIES.find((x) => x.code === code);
  if (!c) return code;
  return lang === "ar" ? c.ar : c.en;
}
