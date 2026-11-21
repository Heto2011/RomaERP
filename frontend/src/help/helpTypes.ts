/** One in-app help guide. `routes` are path prefixes; the ? button opens the guide whose route best matches the page the person is on. */
export type HelpTopic = {
  id: string;
  routes: string[];
  section: { ar: string; en: string };
  title: { ar: string; en: string };
  keywords: string;           // extra words people might search for, both languages
  steps: { ar: string[]; en: string[] };
  tips?: { ar: string[]; en: string[] };
};

