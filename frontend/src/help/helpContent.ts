/** The in-app help: short step-by-step guides, written once, in Arabic and English. No cost to run and nothing is sent anywhere.
 *  `routes` are path prefixes: the "?" button opens the guide whose route best matches the page the person is on. */
export type HelpTopic = {
  id: string;
  routes: string[];
  section: { ar: string; en: string };
  title: { ar: string; en: string };
  keywords: string;           // extra words people might search for, both languages
  steps: { ar: string[]; en: string[] };
  tips?: { ar: string[]; en: string[] };
};

export const HELP_TOPICS: HelpTopic[] = [
  {
    id: "start", routes: ["/"], section: { ar: "البداية", en: "Getting started" },
    title: { ar: "إزاي أتنقل في النظام", en: "Finding your way around" },
    keywords: "قائمة تنقل بداية menu navigation start language dark",
    steps: {
      ar: [
        "القايمة اللي فوق فيها الأقسام: لوحة التحكم، المحاسبة، المشتريات، المبيعات، المراجعة، التقارير، الإعدادات. دوس على القسم وهتنزل منه الصفحات.",
        "أيقونة الموارد البشرية وأيقونة المطعم فوق جنب اللغة، كل واحدة بتفتح صفحتها بكل تفاصيلها.",
        "مربع البحث فوق بيدوّر في الصفحات والبيانات.",
        "زر EN/AR بيغيّر اللغة، والشمس/القمر بيغيّر الوضع الفاتح والداكن.",
        "علامة ؟ في أي صفحة بتفتح شرح الصفحة اللي إنت فيها.",
      ],
      en: [
        "The top menu holds the sections: dashboard, accounting, purchasing, sales, review, reports and settings. Click a section to see its pages.",
        "The HR and Restaurant icons at the top next to the language button each open their own page with all its screens.",
        "The search box at the top looks through pages and data.",
        "EN/AR switches the language; the sun/moon switches light and dark mode.",
        "The ? mark on any page opens the guide for the page you are on.",
      ],
    },
  },
  {
    id: "chart-of-accounts", routes: ["/accounting/chart-of-accounts"], section: { ar: "المحاسبة", en: "Accounting" },
    title: { ar: "شجرة الحسابات", en: "Chart of accounts" },
    keywords: "حساب حسابات شجرة دليل account chart tree code",
    steps: {
      ar: [
        "افتح المحاسبة ثم شجرة الحسابات. بتشوف كل الحسابات متقسمة: أصول، خصوم، حقوق ملكية، إيرادات، مصروفات.",
        "لإضافة حساب: دوس حساب جديد، اكتب الكود والاسم واختار النوع والحساب الأب، ثم احفظ.",
        "الحساب الفرعي لازم يبقى من نفس نوع الحساب الأب.",
        "حساب عليه قيود مينفعش يتمسح، تقدر توقفه بس.",
        "لو عندك شجرة جاهزة في Excel، استخدم زر رفع شجرة من ملف. ولو غيّرت الشجرة وعايز تنقل رصيد حساب قديم، استخدم زر نقل رصيد لحساب جديد.",
      ],
      en: [
        "Open Accounting, then Chart of accounts. All accounts are grouped as assets, liabilities, equity, revenue and expenses.",
        "To add one: press New account, enter the code and name, choose the type and the parent account, then save.",
        "A child account must be of the same type as its parent.",
        "An account that has entries cannot be deleted; you can switch it off.",
        "If you have a chart ready in Excel, use Upload from a file. If you changed the chart and want to move an old account's balance, use Move a balance.",
      ],
    },
  },
  {
    id: "chart-import", routes: ["/accounting/chart-import"], section: { ar: "المحاسبة", en: "Accounting" },
    title: { ar: "رفع شجرة حسابات من Excel", en: "Upload a chart of accounts from Excel" },
    keywords: "رفع استيراد اكسل excel csv import upload شجرة استبدال replace",
    steps: {
      ar: [
        "دوس تحميل نموذج Excel، وعبّيه: الكود، الاسم بالعربي والإنجليزي، النوع، الحساب الأب، الطبيعة. أو ارفع ملفك الجاهز (Excel أو CSV).",
        "هتظهر الشجرة في جدول، وكل خانة تقدر تعدّلها. النظام بيكمّل نوع الحساب والحساب الأب لو ناقصين، ويكتب الأخطاء والتنبيهات جنب كل سطر.",
        "بعد التعديل دوس تحقق من التعديلات.",
        "اقرأ الملخص: كام حساب جديد، وكام هيتحدّث، وكام هيتمسح، وكام هيتوقف.",
        "لما الأخطاء تخلص، دوس اعتماد الرفع. الشجرة المرفوعة بتبقى هي الشغّالة.",
      ],
      en: [
        "Press Download the Excel template and fill it in: code, Arabic and English names, type, parent, nature. Or upload your own file (Excel or CSV).",
        "The chart appears in a table where every cell can be edited. The system fills in a missing type or parent and shows errors and warnings beside each line.",
        "After editing, press Check my edits.",
        "Read the summary: how many accounts are new, updated, removed or switched off.",
        "When no errors remain, press Approve. The uploaded chart becomes the working chart.",
      ],
    },
    tips: {
      ar: ["الحسابات القديمة اللي عليها قيود مش بتتمسح، بتفضل موجودة ومتوقفة عشان التقارير القديمة تفضل صح.", "الحسابات اللي النظام نفسه بيسجّل عليها (الصندوق، العملاء، المبيعات، المرتبات...) بتفضل زي ما هي.", "جرّب على حساب تجريبي الأول."],
      en: ["Old accounts that have entries are never deleted: they stay, switched off, so past reports stay correct.", "Accounts the system itself posts to (cash, customers, sales, payroll...) stay as they are.", "Try it on a test company first."],
    },
  },
  {
    id: "transfer-balance", routes: ["/accounting/transfer-balance"], section: { ar: "المحاسبة", en: "Accounting" },
    title: { ar: "نقل رصيد حساب قديم لحساب جديد", en: "Move an old account's balance to a new one" },
    keywords: "نقل رصيد تسوية قيد transfer balance reclass",
    steps: {
      ar: [
        "اختار الحساب القديم والحساب الجديد (من نفس النوع)، والتاريخ.",
        "دوس عرض القيد المقترح. هتشوف رصيد الحساب القديم وإيه اللي هيتسجّل مدين وإيه دائن.",
        "دوس اعتماد وتسجيل القيد. بيتعمل قيد تسوية واحد، والقيود الأصلية تفضل زي ما هي، ورصيد القديم يبقى صفر.",
      ],
      en: [
        "Choose the old account, the new account (of the same type) and the date.",
        "Press Show the proposed entry. You see the old account's balance and what will be debited and credited.",
        "Press Approve and post. One adjustment entry is posted, the original entries stay as they were, and the old account ends at zero.",
      ],
    },
    tips: { ar: ["بيشتغل مع الأصول والخصوم وحقوق الملكية. الإيرادات والمصروفات بتتنقل بقيد من المحاسب لأنها بتأثر على أرباح الفترة."], en: ["It works for assets, liabilities and equity. Income and expense balances are moved by an accountant's entry because they affect the period's profit."] },
  },
  {
    id: "journal-entries", routes: ["/accounting/journal-entries"], section: { ar: "المحاسبة", en: "Accounting" },
    title: { ar: "القيود اليومية", en: "Journal entries" },
    keywords: "قيد يومية journal entry عكس reverse",
    steps: {
      ar: [
        "افتح المحاسبة ثم القيود اليومية لتشوف كل القيود.",
        "لتسجيل قيد يدوي: أضف قيد جديد، واكتب سطور مدين ودائن. مجموع المدين لازم يساوي مجموع الدائن.",
        "قيد اتسجّل مينفعش يتعدّل أو يتمسح؛ لو فيه غلط بتعمل عليه قيد عكسي ثم تسجّل القيد الصح.",
      ],
      en: [
        "Open Accounting, then Journal entries to see all entries.",
        "To record a manual entry: add a new entry and write debit and credit lines. Total debit must equal total credit.",
        "A posted entry cannot be edited or deleted; if it is wrong you reverse it and post the correct one.",
      ],
    },
  },
  {
    id: "sales-invoices", routes: ["/sales/invoices", "/sales/customers"], section: { ar: "المبيعات", en: "Sales" },
    title: { ar: "فواتير المبيعات والعملاء", en: "Sales invoices and customers" },
    keywords: "فاتورة مبيعات عميل invoice sales customer ضريبة vat",
    steps: {
      ar: [
        "أضف العميل من المبيعات ثم العملاء (الاسم ورقم التليفون، والرقم الضريبي لو موجود).",
        "افتح المبيعات ثم الفواتير وأنشئ فاتورة جديدة: اختار العميل، وأضف الأصناف والكميات والأسعار.",
        "راجع الضريبة والإجمالي ثم احفظ. الفاتورة بتتسجّل في المحاسبة تلقائي.",
        "لتسجيل دفعة من العميل افتح الفاتورة وسجّل الدفع.",
        "تقرير أعمار الديون في المبيعات بيوريك مين متأخر في السداد.",
      ],
      en: [
        "Add the customer under Sales, then Customers (name, phone and tax number if any).",
        "Open Sales, then Invoices and create a new invoice: choose the customer and add the items, quantities and prices.",
        "Check the tax and the total, then save. The invoice is posted to the accounts automatically.",
        "To record a payment from the customer, open the invoice and record the payment.",
        "The aging report under Sales shows who is late paying.",
      ],
    },
  },
  {
    id: "purchase-invoices", routes: ["/purchasing"], section: { ar: "المشتريات", en: "Purchasing" },
    title: { ar: "فواتير المشتريات والموردين", en: "Purchase invoices and vendors" },
    keywords: "فاتورة مشتريات مورد purchase vendor ضريبة بند tax line",
    steps: {
      ar: [
        "أضف المورد من المشتريات ثم الموردين.",
        "افتح المشتريات ثم الفواتير وأنشئ فاتورة: اختار المورد وأضف البنود والمبالغ.",
        "في فاتورة المحاسبة تقدر تحدد نسبة الضريبة لكل بند لو البنود ضرائبها مختلفة.",
        "احفظ، والفاتورة بتتسجّل في المحاسبة. وسجّل الدفع لما تدفع للمورد.",
      ],
      en: [
        "Add the vendor under Purchasing, then Vendors.",
        "Open Purchasing, then Invoices and create an invoice: choose the vendor and add the lines and amounts.",
        "In an accounting invoice you can set the tax rate per line when the lines have different taxes.",
        "Save, and the invoice is posted to the accounts. Record the payment when you pay the vendor.",
      ],
    },
  },
  {
    id: "recurring-expenses", routes: ["/assistant/recurring"], section: { ar: "المصروفات", en: "Expenses" },
    title: { ar: "المصروفات الشهرية المتكررة", en: "Monthly recurring expenses" },
    keywords: "متكرر شهري ايجار مرتبات recurring monthly rent salaries",
    steps: {
      ar: [
        "افتح صفحة المصروفات المتكررة (المساعد الذكي ثم المصروفات المتكررة).",
        "أضف بند جديد: اكتب الاسم (مرتبات مثلاً) واختار حساب المصروف.",
        "اكتب المبلغ لو ثابت، أو سيبه فاضي لو بيتغير كل شهر.",
        "اكتب يوم الاستحقاق من الشهر. من 29 لـ31 معناها آخر يوم لو الشهر أقصر.",
        "طريقة الدفع: اختار نقدًا أو شبكة، أو سيبها يتحدد وقت الصرف.",
        "كل شهر يظهر المصروف جاهز في شاشة الاعتماد وإنت بتراجعه وبتوافق أو بتعدّله.",
      ],
      en: [
        "Open the Recurring expenses page (AI assistant, then Recurring expenses).",
        "Add a new item: write the name (for example Salaries) and choose the expense account.",
        "Enter the amount if it is fixed, or leave it empty if it changes every month.",
        "Enter the day of the month it falls due. 29 to 31 means the last day in a shorter month.",
        "Payment method: choose cash or card, or leave it to be decided when paying.",
        "Every month the expense appears ready in the approvals screen and you review it, approve it or edit it.",
      ],
    },
  },
  {
    id: "expense-approvals", routes: ["/assistant/approvals"], section: { ar: "المصروفات", en: "Expenses" },
    title: { ar: "اعتماد المصروفات", en: "Approving expenses" },
    keywords: "اعتماد موافقة مصروف approve expense approval",
    steps: {
      ar: [
        "افتح شاشة اعتماد المصروفات. هتلاقي المصروفات اللي مستنية موافقتك.",
        "راجع الوصف والحساب والمبلغ. لو المبلغ ناقص اكتبه.",
        "لو طريقة الدفع لسه ما اتحددتش، اختار نقدًا أو شبكة.",
        "دوس اعتماد وتسجيل، فيتسجّل في المحاسبة. أو دوس رفض.",
        "المصروف اللي بالشبكة بيستنى يتطابق مع كشف الحساب البنكي الأول.",
      ],
      en: [
        "Open the expense approvals screen. You see the expenses waiting for your approval.",
        "Check the description, account and amount. If the amount is missing, type it.",
        "If the payment method is not decided yet, choose cash or card.",
        "Press Approve and post, and it is posted to the accounts. Or press Reject.",
        "A card expense first waits to be matched with the bank statement.",
      ],
    },
  },
  {
    id: "cost-allocation", routes: ["/accounting/cost-allocation", "/accounting/branch-profit"], section: { ar: "التقارير", en: "Reports" },
    title: { ar: "توزيع المصروفات المشتركة وربح الفروع", en: "Shared costs and branch profit" },
    keywords: "توزيع مشتركة فروع ربح فرع allocation branch profit shared",
    steps: {
      ar: [
        "سجّل المصروف المشترك (إيجار المكتب الرئيسي، مرتبات الإدارة...) من غير ما تربطه بفرع.",
        "آخر الشهر افتح توزيع المصروفات المشتركة واختار السنة والشهر، ودوس عرض المقترح.",
        "المصروفات بتتوزع على الفروع بنسبة مبيعات كل فرع، وتقدر تعدّل النسب (مجموعها لازم 100).",
        "دوس اعتماد وتنفيذ التوزيع، فيتعمل قيد واحد.",
        "بعدها افتح ربح الفروع لتشوف الربح قبل التوزيع وبعده.",
      ],
      en: [
        "Record the shared cost (head-office rent, office payroll...) without tying it to a branch.",
        "At month end open Shared cost allocation, choose the year and month and press Show proposal.",
        "The costs are shared out on each branch's share of sales, and you can edit the shares (they must total 100).",
        "Press Approve and post the allocation, and one entry is posted.",
        "Then open Branch profit to see profit before and after the allocation.",
      ],
    },
  },
  {
    id: "payroll", routes: ["/hr/payroll", "/people/payroll", "/hr/employee-debts", "/people/employee-debts", "/hr/payroll-settings"], section: { ar: "الموارد البشرية", en: "HR" },
    title: { ar: "الرواتب والسلف والخصومات", en: "Payroll, advances and deductions" },
    keywords: "راتب مرتبات سلفة خصم جزاء payroll salary advance debt fine",
    steps: {
      ar: [
        "ظبط إعدادات الرواتب أولاً (الدولة وقواعد التأمينات والضرائب) من إعدادات الرواتب.",
        "أضف الموظفين وبنود الراتب من الموظفين وبنود الراتب.",
        "السلف والغرامات بتتسجّل من مديونيات الموظفين، وبتتخصم من الراتب حسب الخطة.",
        "افتح الرواتب وأنشئ مسير الشهر، وراجع الخصومات، ثم اعتمد.",
        "الاعتماد بيسجّل قيد المرتبات في المحاسبة.",
      ],
      en: [
        "Set up payroll settings first (the country and its insurance and tax rules).",
        "Add the employees and salary components.",
        "Advances and fines are recorded under Employee debts and are deducted from salary on the plan.",
        "Open Payroll, create the month's run, review the deductions and approve.",
        "Approval posts the payroll entry to the accounts.",
      ],
    },
  },
  {
    id: "inventory", routes: ["/inventory", "/restaurant-portal/inventory"], section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "المخزون: الأصناف والجرد والتسوية", en: "Inventory: items, counts and adjustments" },
    keywords: "مخزون صنف جرد تسوية حد أدنى inventory item count adjustment minimum stock",
    steps: {
      ar: [
        "أضف الأصناف والمخازن من الأصناف والمخازن.",
        "التحويل بين المخازن من تحويلات المخزون.",
        "الجرد الفعلي: سجّل الكميات الفعلية، وهتظهر الفروق. لا تتغيّر الأرصدة قبل اعتماد المحاسب أو صاحب الشغل.",
        "عند الاعتماد اختار مين يتحمل الفرق (الموظف أو المدير أو الفرع أو الشركة).",
        "تسوية المخزون: لتصحيح كمية صنف بقيد واضح وسبب.",
        "الحد الأدنى لكل فرع: حدد كمية دنيا لكل صنف في كل فرع، وهتتنبه لما المخزون يقل عنها.",
      ],
      en: [
        "Add items and warehouses under Items and Warehouses.",
        "Move stock between warehouses under Stock transfers.",
        "Physical count: enter the counted quantities and the differences appear. Balances do not change until the accountant or owner approves.",
        "On approval choose who bears the difference (the employee, manager, branch or company).",
        "Stock adjustment: correct an item's quantity with a clear entry and reason.",
        "Branch minimums: set a minimum quantity per item per branch and you are alerted when stock falls below it.",
      ],
    },
  },
  {
    id: "my-subscription", routes: ["/my-subscription", "/people/subscription"], section: { ar: "الاشتراك", en: "Subscription" },
    title: { ar: "اشتراكي والدفع", en: "My subscription and paying" },
    keywords: "اشتراك دفع انستاباي تحويل بنكي فاتورة subscription payment instapay bank transfer",
    steps: {
      ar: [
        "افتح اشتراكي لتشوف باقتك وحدودها (الفروع والمستخدمين) وفواتيرك.",
        "في مصر الدفع بـ InstaPay. خارج مصر الدفع بتحويل بنكي. بيانات الحساب بتظهر في قسم الدفع، وجنب كل بيان أيقونة نسخ.",
        "بعد التحويل افتح الفاتورة ودوس أبلغ عن الدفع، وهتتأكد من الدفع ويتفعّل الاشتراك.",
        "لو احتجت أكتر من فروع أو مستخدمين، كلمنا على support@romagroup.app.",
      ],
      en: [
        "Open My subscription to see your plan, its limits (branches and users) and your invoices.",
        "In Egypt you pay with InstaPay. Elsewhere you pay by bank transfer. The account details show in the payment section with a copy icon beside each.",
        "After transferring, open the invoice and press Report payment; the payment is confirmed and the subscription stays active.",
        "If you need more branches or users, write to support@romagroup.app.",
      ],
    },
  },
];

const norm = (s: string) => s.toLowerCase().replace(/[أإآ]/g, "ا").replace(/ة/g, "ه").replace(/ى/g, "ي");

/** The guide for the page the person is on: the longest matching route prefix, or the general one. */
export function topicForPath(pathname: string): HelpTopic {
  let best: HelpTopic | undefined;
  let bestLen = 0;
  for (const t of HELP_TOPICS) {
    for (const r of t.routes) {
      if (r !== "/" && pathname.startsWith(r) && r.length > bestLen) { best = t; bestLen = r.length; }
    }
  }
  return best ?? HELP_TOPICS[0];
}

export function searchTopics(query: string, lang: "ar" | "en"): HelpTopic[] {
  const q = norm(query.trim());
  if (!q) return HELP_TOPICS;
  const words = q.split(/\s+/).filter(Boolean);
  return HELP_TOPICS.filter((t) => {
    const hay = norm([t.title.ar, t.title.en, t.section.ar, t.section.en, t.keywords, ...t.steps[lang]].join(" "));
    return words.every((w) => hay.includes(w));
  });
}
