import type { HelpTopic } from "../helpTypes";

/** "Learn accounting": short plain-language lessons on the principles and standards the system follows, each tied to where it shows up in RomaERP.
 *  They have no route on purpose: the "?" button on a page keeps opening that page's own how-to guide, and these lessons appear when browsing or searching the help.
 *  The standards govern the system (see docs/accounting-principles.md); a lesson describes the principle, not legal advice. */
const SECTION = { ar: "تعلّم المحاسبة", en: "Learn accounting" };

export const TOPICS: HelpTopic[] = [
  {
    id: "learn-standards", routes: [], section: SECTION,
    title: { ar: "المعايير اللي السيستم بيمشي عليها", en: "The standards the system follows" },
    keywords: "معايير IFRS IAS معيار محاسبي دولي المصري standards accounting",
    steps: {
      ar: [
        "السيستم بيمشي على المعايير المحاسبية الدولية (IFRS وIAS)، وهي الأساس الحاكم لكل الدول بنفس الطريقة. مش بنبني محاسبة مختلفة لكل بلد.",
        "لو طلب من السيستم حاجة بتخالف معيار، المعيار هو اللي بيمشي، ومحدش بيغيّره بتعليمات.",
        "الحاجات اللي بتختلف من بلد لبلد (زي الضريبة والتأمينات والمرتبات) ليها قواعد منفصلة لكل بلد، ومش بتغيّر الأساس المحاسبي.",
        "مثال: المخزون بيتقيّم بالتكلفة أو بصافي القيمة البيعية أيهما أقل، ومفيش طريقة الداخل أخيراً يخرج أولاً (LIFO).",
        "شجرة الحسابات الأساسية بتتستخدم زي ما هي. تقدر تضيف حساباتك أنت، بس مش بيتضاف حساب لمجرد ميزة جديدة.",
        "أي حاجة بتتعمل أوتوماتيك (زي فروق الجرد أو خصم سلفة من المرتب) بتستنى موافقة المحاسب أو صاحب الشركة وتقدر تعدلها قبل ما تتسجل.",
      ],
      en: [
        "The system follows the international accounting standards (IFRS and IAS), which are the governing basis for every country in the same way. We do not build a different accounting basis for each country.",
        "If an instruction conflicts with a standard, the standard wins; it is not overridden by a setting.",
        "What differs from country to country (such as tax, social insurance and payroll) lives in separate per-country rules and does not change the accounting basis.",
        "Example: inventory is valued at the lower of cost and net realisable value, and the last-in-first-out method (LIFO) is not used.",
        "The base chart of accounts is used as it is. You can add your own accounts, but accounts are not added just for a new feature.",
        "Anything automatic (such as count differences or deducting an employee advance from pay) waits for the accountant's or owner's approval, and you can edit it before it is booked.",
      ],
    },
    tips: { ar: ["ده شرح للمبدأ، مش استشارة قانونية أو ضريبية. مراجعة محاسبك القانوني لسه مهمة."], en: ["This explains the principle; it is not legal or tax advice. Your own accountant's review still matters."] },
  },
  {
    id: "learn-double-entry", routes: [], section: SECTION,
    title: { ar: "القيد المزدوج", en: "Double entry" },
    keywords: "قيد مزدوج مدين دائن debit credit journal double entry",
    steps: {
      ar: [
        "كل عملية بتأثر على حسابين على الأقل: طرف مدين وطرف دائن، ومجموع المدين لازم يساوي مجموع الدائن.",
        "مثال: بعت بـ 1,000 نقداً. الصندوق (أصل) بيزيد، فهو مدين 1,000. والمبيعات (إيراد) بتزيد، فهي دائنة 1,000.",
        "مثال تاني: دفعت إيجار 5,000 من البنك. الإيجار (مصروف) مدين 5,000، والبنك دائن 5,000.",
        "السيستم بيعمل القيد لوحده من الفاتورة أو المصروف أو الراتب. ولو دخلت قيد يدوي، مش هيتسجل لو مش متوازن.",
        "تلاقي كل القيود في المحاسبة ثم القيود اليومية، وتقدر تفتح أي قيد وتشوف طرفيه.",
      ],
      en: [
        "Every transaction affects at least two accounts: a debit side and a credit side, and total debits must equal total credits.",
        "Example: you sell for 1,000 in cash. Cash (an asset) goes up, so it is debited 1,000. Sales (revenue) go up, so they are credited 1,000.",
        "Another: you pay 5,000 rent from the bank. Rent (an expense) is debited 5,000 and the bank is credited 5,000.",
        "The system makes the entry itself from an invoice, an expense or payroll. A manual entry that does not balance will not be posted.",
        "Find all entries under Accounting, then Journal entries, and open any entry to see both sides.",
      ],
    },
    tips: { ar: ["الأصول والمصروفات بتزيد بالمدين. الخصوم وحقوق الملكية والإيرادات بتزيد بالدائن."], en: ["Assets and expenses increase with a debit. Liabilities, equity and revenue increase with a credit."] },
  },
  {
    id: "learn-account-types", routes: [], section: SECTION,
    title: { ar: "أنواع الحسابات الخمسة", en: "The five kinds of accounts" },
    keywords: "اصول خصوم حقوق ملكية ايرادات مصروفات assets liabilities equity revenue expenses",
    steps: {
      ar: [
        "الأصول: اللي الشركة بتملكه، زي النقدية والبنك والعملاء (اللي لك عندهم) والمخزون والمعدات.",
        "الخصوم: اللي على الشركة، زي الموردين والقروض والضريبة المستحقة والمرتبات المستحقة.",
        "حقوق الملكية: حق صاحب الشركة: رأس المال والأرباح المحتجزة.",
        "الإيرادات: اللي الشركة كسبته من نشاطها، زي المبيعات.",
        "المصروفات: اللي الشركة صرفته عشان تكسب، زي الإيجار والمرتبات وتكلفة البضاعة.",
        "المعادلة الأساسية: الأصول = الخصوم + حقوق الملكية، والربح = الإيرادات - المصروفات.",
      ],
      en: [
        "Assets: what the company owns, such as cash, the bank, receivables (what customers owe you), inventory and equipment.",
        "Liabilities: what the company owes, such as suppliers, loans, tax payable and salaries payable.",
        "Equity: the owner's stake: capital and retained earnings.",
        "Revenue: what the company earned from its activity, such as sales.",
        "Expenses: what it spent to earn that, such as rent, salaries and the cost of goods.",
        "The basic equation: Assets = Liabilities + Equity, and Profit = Revenue - Expenses.",
      ],
    },
    tips: { ar: ["شجرة الحسابات بتتقسم بنفس الأنواع دي، وكل حساب فرعي لازم يبقى من نوع الحساب الأب."], en: ["The chart of accounts is split by these kinds, and a child account must be of its parent's kind."] },
  },
  {
    id: "learn-accrual", routes: [], section: SECTION,
    title: { ar: "أساس الاستحقاق", en: "Accrual basis" },
    keywords: "استحقاق نقدي accrual cash basis مبدأ",
    steps: {
      ar: [
        "المحاسبة بتسجّل العملية في وقت حدوثها، مش وقت ما الفلوس تتحرك. ده اسمه أساس الاستحقاق.",
        "بعت بضاعة النهارده والعميل هيدفع بعد شهر: الإيراد بيتسجل النهارده، والمبلغ بيتسجل عند العملاء لحد ما يدفع.",
        "استلمت فاتورة كهرباء بتخص الشهر ده وهتدفعها الشهر الجاي: المصروف بيتسجل في الشهر ده.",
        "عشان كده فاتورة المبيعات في السيستم بتعمل الإيراد والعميل، والتحصيل بعدها بيقفل المديونية بس.",
        "الفرق بين الربح والنقدية هو اللي بيفسّر ليه شركة رابحة ممكن تبقى معاها فلوس قليلة.",
      ],
      en: [
        "Accounting records a transaction when it happens, not when cash moves. This is the accrual basis.",
        "You sell goods today and the customer pays in a month: the revenue is recorded today, and the amount sits in receivables until it is paid.",
        "You receive an electricity bill for this month and pay it next month: the expense belongs to this month.",
        "That is why a sales invoice in the system creates the revenue and the receivable, and the later collection only closes the receivable.",
        "The gap between profit and cash is why a profitable company can still be short of cash.",
      ],
    },
  },
  {
    id: "learn-revenue", routes: [], section: SECTION,
    title: { ar: "متى يُسجَّل الإيراد؟ (IFRS 15)", en: "When is revenue recorded? (IFRS 15)" },
    keywords: "ايراد اعتراف ifrs 15 revenue recognition تسليم",
    steps: {
      ar: [
        "المعيار الدولي للإيراد (IFRS 15) بيقول: سجّل الإيراد لما العميل يستلم السيطرة على البضاعة أو الخدمة، مش لما تستلم الفلوس.",
        "بضاعة اتسلمت: الإيراد بيتسجل وقت التسليم أو الفاتورة. خدمة بتتقدم على مدة: الإيراد بيتوزع على المدة.",
        "لو العميل دفع مقدماً عن حاجة لسه ما اتسلمتش، المبلغ ده مش إيراد لسه، ده التزام عليك لحد ما تسلّم.",
        "لو بتستلم اشتراكات أو إيجارات مقدماً، استخدم صفحة التأجيلات عشان توزّعها على الشهور اللي بتخصها.",
      ],
      en: [
        "The international revenue standard (IFRS 15) says: record revenue when the customer gets control of the goods or service, not when you receive the cash.",
        "Goods delivered: revenue is recorded at delivery or invoice. A service given over a period: the revenue is spread over that period.",
        "If a customer pays in advance for something not yet delivered, that money is not revenue yet; it is an obligation until you deliver.",
        "If you receive subscriptions or rent in advance, use the Deferrals page to spread them over the months they belong to.",
      ],
    },
  },
  {
    id: "learn-deferrals", routes: [], section: SECTION,
    title: { ar: "المصروفات والإيرادات المؤجلة", en: "Prepayments and deferred income" },
    keywords: "مقدم مؤجل مستحق تأجيل prepaid deferred accrued matching مقابلة",
    steps: {
      ar: [
        "مبدأ المقابلة: المصروف يتسجل في نفس الفترة اللي كسب فيها الإيراد اللي بيخدمه.",
        "مصروف مدفوع مقدماً: دفعت تأمين سنة في يناير. ده أصل لحد ما يستهلك، وكل شهر 1/12 منه بيتحول لمصروف.",
        "إيراد مقبوض مقدماً: قبضت اشتراك سنة. ده التزام لحد ما تقدّم الخدمة، وكل شهر 1/12 منه بيتحول لإيراد.",
        "مصروف مستحق: استخدمت خدمة ولسه الفاتورة ما جاتش. بتتسجل في الشهر ده وبتتسدد بعدين.",
        "من المحاسبة ثم التأجيلات سجّل المبلغ وعدد الشهور، ومرة كل شهر دوس رحّل الشهور المستحقة والسيستم بيوزعه.",
      ],
      en: [
        "The matching principle: an expense is recorded in the same period as the revenue it helped earn.",
        "A prepaid expense: you pay a year of insurance in January. It is an asset until used, and each month 1/12 of it becomes an expense.",
        "Income received in advance: you collect a year's subscription. It is a liability until the service is given, and each month 1/12 of it becomes revenue.",
        "An accrued expense: you used a service and the bill has not arrived. It is recorded this month and paid later.",
        "Under Accounting, then Deferrals, enter the amount and the number of months, and once a month press Post due months and the system spreads it.",
      ],
    },
  },
  {
    id: "learn-inventory", routes: [], section: SECTION,
    title: { ar: "تقييم المخزون (IAS 2)", en: "Valuing inventory (IAS 2)" },
    keywords: "مخزون تكلفة ias 2 inventory متوسط ترجيح فيفو lifo nrv",
    steps: {
      ar: [
        "المعيار الدولي للمخزون (IAS 2): المخزون بيتقيّم بالتكلفة أو بصافي القيمة البيعية، أيهما أقل.",
        "التكلفة بتشمل سعر الشراء والمصاريف اللي لازم تتحمل عشان توصّل الصنف لمكانه وحالته. السيستم بيحسب متوسط التكلفة لكل صنف.",
        "طريقة الداخل أخيراً يخرج أولاً (LIFO) مش مسموحة في المعيار، فمش موجودة في السيستم.",
        "التصنيع الداخلي: تكلفة الخامات والعمالة والمصاريف الصناعية بتتحمل على المنتج، بشرط تتوزع على الطاقة الإنتاجية العادية.",
        "العجز والهالك غير الطبيعي والمصاريف الصناعية اللي ما اتوزعتش بتتحمل مصروف الفترة، ومش بتتضاف لتكلفة المخزون.",
        "لو سعر البيع الصافي نزل عن التكلفة، لازم تنزّل قيمة المخزون للقيمة الأقل وتسجّل الفرق خسارة.",
      ],
      en: [
        "The international inventory standard (IAS 2): inventory is valued at the lower of cost and net realisable value.",
        "Cost includes the purchase price and the costs needed to bring the item to its place and condition. The system calculates an average cost per item.",
        "Last-in-first-out (LIFO) is not allowed by the standard, so it does not exist in the system.",
        "In-house production: materials, labour and production overhead are charged to the product, with overhead spread on normal capacity.",
        "Shortages, abnormal waste and overhead that was not absorbed are expenses of the period; they are not added to the cost of inventory.",
        "If the net selling price falls below cost, inventory must be written down to the lower value and the difference recorded as a loss.",
      ],
    },
    tips: { ar: ["فروق الجرد بتستنى موافقتك وبتحدد مين يتحملها قبل ما تتسجل."], en: ["Count differences wait for your approval, and you choose who bears them, before they are booked."] },
  },
  {
    id: "learn-fixed-assets", routes: [], section: SECTION,
    title: { ar: "الأصول الثابتة والإهلاك (IAS 16)", en: "Fixed assets and depreciation (IAS 16)" },
    keywords: "اصول ثابتة اهلاك استهلاك ias 16 depreciation fixed assets قسط",
    steps: {
      ar: [
        "الأصل الثابت حاجة بتشتريها وتستخدمها أكتر من سنة، زي المعدات والسيارات. بتتسجل بتكلفتها، مش مصروف مرة واحدة.",
        "الإهلاك بيوزّع تكلفة الأصل على سنين عمره المتوقع، لأنه بيخدم أكتر من فترة (مبدأ المقابلة).",
        "طريقة القسط الثابت: (التكلفة - قيمة الخردة) ÷ العمر. وفي طريقة القسط المتناقص بتهلك نسبة من القيمة المتبقية كل سنة.",
        "من المحاسبة ثم الأصول الثابتة سجّل الأصل وعمره وطريقته، وتشغيل الإهلاك (من صفحة الإهلاك) بيعمل القيد.",
        "الأرض مش بتتهلك. والأصل اللي اتباع أو اتشال بيتخرج من السجل ويتسجل ربحه أو خسارته.",
      ],
      en: [
        "A fixed asset is something you buy and use for more than a year, such as equipment and vehicles. It is recorded at cost, not as an expense all at once.",
        "Depreciation spreads the cost over the asset's expected life, because it serves more than one period (the matching principle).",
        "Straight-line: (cost - scrap value) ÷ life. Declining balance charges a percentage of the remaining value each year.",
        "Under Accounting, then Fixed assets, record the asset, its life and its method; running depreciation (from the depreciation page) makes the entry.",
        "Land is not depreciated. An asset that is sold or removed leaves the register and the gain or loss is recorded.",
      ],
    },
  },
  {
    id: "learn-statements", routes: [], section: SECTION,
    title: { ar: "القوائم المالية الثلاث", en: "The three financial statements" },
    keywords: "ميزان مراجعة قائمة دخل ميزانية تدفقات trial balance income statement balance sheet",
    steps: {
      ar: [
        "ميزان المراجعة: قايمة بكل الحسابات وأرصدتها. مجموع المدين لازم يساوي مجموع الدائن، وده أول اختبار إن الدفاتر سليمة.",
        "قائمة الدخل: إيرادات - مصروفات = ربح أو خسارة عن فترة. بتقولك الشركة كسبت قد إيه في المدة دي.",
        "الميزانية: صورة الشركة في تاريخ معيّن: الأصول = الخصوم + حقوق الملكية. بتقولك الشركة بتملك إيه وعليها إيه.",
        "قائمة التدفقات النقدية: النقدية دخلت منين وخرجت فين. هي اللي بتفسر الفرق بين الربح والنقدية.",
        "كلها من المحاسبة ثم التقارير. ولازم تتأكد إن الفترة المحاسبية مقفولة أو القيود مرحّلة قبل ما تعتمد على الأرقام.",
        "تنبيه: معيار IFRS 18 هيحل محل IAS 1 ويغيّر شكل عرض قائمة الدخل (تقسيم بنودها لفئات تشغيل واستثمار وتمويل) للفترات اللي بتبدأ من 1 يناير 2027. الفكرة نفسها (إيرادات - مصروفات = ربح) مش بتتغير، لكن الشكل هيتغير. اسأل محاسبك عن موعد تطبيقه في بلدك.",
      ],
      en: [
        "Trial balance: a list of all accounts and their balances. Total debits must equal total credits, which is the first test that the books are sound.",
        "Income statement: revenue - expenses = profit or loss for a period. It tells you how much the company earned in that time.",
        "Balance sheet: a picture of the company on a given date: Assets = Liabilities + Equity. It tells you what it owns and what it owes.",
        "Cash flow statement: where cash came from and where it went. It explains the gap between profit and cash.",
        "All are under Accounting and Reports. Make sure entries are posted for the period before you rely on the numbers.",
        "Note: IFRS 18 will replace IAS 1 and change how the income statement is presented (its lines are grouped into operating, investing and financing categories) for periods beginning on or after 1 January 2027. The idea itself (revenue - expenses = profit) does not change, but the layout will. Ask your accountant when it applies in your country.",
      ],
    },
  },
  {
    id: "learn-closing", routes: [], section: SECTION,
    title: { ar: "الفترات المحاسبية وإقفال الشهر", en: "Fiscal periods and month-end close" },
    keywords: "اقفال فترة محاسبية شهر close period month-end ترحيل عكس قيد",
    steps: {
      ar: [
        "السنة المالية بتتقسم لفترات، غالباً شهر. كل قيد بيتسجل في فترة.",
        "قبل إقفال الشهر: راجع إن كل الفواتير اتسجلت، وإن الجرد والتسويات والإهلاك اتعملوا، وإن حساب البنك متطابق مع كشف البنك.",
        "قائمة إقفال الشهر في قسم المراجعة بتسألك الأسئلة دي واحدة واحدة.",
        "القيد اللي اترحّل مش بيتعدّل. لو فيه غلط، بتعمل قيد عكسي، فيفضل أثر واضح لكل تغيير.",
        "بعد إقفال الفترة مفيش قيد يتسجل فيها، وده بيحمي الأرقام اللي اتقدمت للإدارة أو للضرائب.",
      ],
      en: [
        "The financial year is split into periods, usually months. Every entry is recorded in a period.",
        "Before closing a month: check all invoices are recorded, that counts, adjustments and depreciation are done, and the bank account matches the bank statement.",
        "The Month-end close checklist in the Review section asks these questions one by one.",
        "A posted entry is not edited. If there is a mistake, you make a reversing entry, so every change leaves a clear trail.",
        "After a period is closed, no entry can be recorded in it, which protects the figures already given to management or the tax authority.",
      ],
    },
  },
  {
    id: "learn-vat", routes: [], section: SECTION,
    title: { ar: "ضريبة القيمة المضافة", en: "Value-added tax (VAT)" },
    keywords: "ضريبة قيمة مضافة vat مدخلات مخرجات tax",
    steps: {
      ar: [
        "ضريبة المخرجات: الضريبة اللي بتحصّلها من عملائك على مبيعاتك. دي مش إيرادك، دي أمانة للدولة.",
        "ضريبة المدخلات: الضريبة اللي دفعتها لمورديك على مشترياتك. ممكن تخصمها لو فاتورة المورد مستوفية.",
        "الضريبة المستحقة للدولة = ضريبة المخرجات - ضريبة المدخلات. لو المدخلات أكبر، ليك رصيد.",
        "نسبة الضريبة بتتحدد من إعدادات شركتك حسب بلدها. وفاتورة المبيعات بتحسبها لوحدها.",
        "صفحة ملخص الضريبة في المحاسبة بتجمعلك المخرجات والمدخلات والصافي عن أي فترة قبل ما تقدم إقرارك.",
      ],
      en: [
        "Output VAT: the tax you collect from customers on your sales. It is not your revenue; it is held for the state.",
        "Input VAT: the tax you paid suppliers on your purchases. You can deduct it if the supplier invoice qualifies.",
        "VAT due to the state = output VAT - input VAT. If input is larger, you have a credit.",
        "The VAT rate comes from your company settings for its country, and a sales invoice calculates it for you.",
        "The VAT summary page under Accounting totals output, input and the net for any period before you file your return.",
      ],
    },
    tips: { ar: ["قواعد الضريبة والتقديم بتختلف من بلد لبلد. اتأكد من محاسبك قبل التقديم."], en: ["Tax and filing rules differ by country. Check with your accountant before filing."] },
  },
  {
    id: "learn-fx", routes: [], section: SECTION,
    title: { ar: "العملات الأجنبية (IAS 21)", en: "Foreign currency (IAS 21)" },
    keywords: "عملة اجنبية سعر صرف فروق عملة ias 21 exchange rate foreign currency",
    steps: {
      ar: [
        "المعيار الدولي للعملات الأجنبية (IAS 21): العملية بالعملة الأجنبية بتتسجل بعملة الشركة بسعر الصرف وقت العملية.",
        "لو العميل دفع بعد كده بسعر صرف تاني، الفرق بيتسجل ربح أو خسارة فروق عملة.",
        "المعيار بيطلب إن الأرصدة الأجنبية المفتوحة في آخر الفترة تتقيّم بسعر آخر الفترة. ده المبدأ، وبيتعمل بقيد تسوية تسجله إنت أو محاسبك بالسعر اللي دخلته.",
        "أسعار الصرف بتتسجل من المحاسبة ثم أسعار الصرف. حدّثها بانتظام لو بتتعامل بعملات أجنبية.",
      ],
      en: [
        "The international standard on foreign currency (IAS 21): a transaction in a foreign currency is recorded in your company's currency at the rate on the day.",
        "If the customer pays later at a different rate, the difference is recorded as an exchange gain or loss.",
        "The standard requires open foreign balances at the end of a period to be revalued at the closing rate. That is the principle, and it is done with an adjusting entry that you or your accountant record at the rate you entered.",
        "Exchange rates are kept under Accounting, then Exchange rates. Update them regularly if you deal in foreign currencies.",
      ],
    },
  },
  {
    id: "learn-receivables", routes: [], section: SECTION,
    title: { ar: "العملاء والموردين وأعمار الديون", en: "Receivables, payables and aging" },
    keywords: "اعمار ديون عملاء موردين مديونية aging receivable payable تحصيل",
    steps: {
      ar: [
        "العملاء: فلوس ليك عند ناس بعتلهم بالآجل. الموردين: فلوس عليك لناس اشتريت منهم بالآجل.",
        "تقرير أعمار الديون بيقسّم المتأخرات حسب المدة: لسه ما استحقتش، متأخرة شهر، شهرين، وأكتر.",
        "كل ما الدين بيقدم بيقل احتمال تحصيله، فمتابعته بدري مهمة.",
        "من المبيعات تلاقي أعمار العملاء، ومن المشتريات أعمار الموردين، ومن المبيعات المتابعات تسجّل اتصالاتك بالعملاء.",
        "دين فقدت الأمل في تحصيله بيتحوّل في المحاسبة لمصروف اسمه ديون معدومة. ومعيار IFRS 9 كمان بيطلب تقدّر مقدماً الخسارة المتوقعة على العملاء وتحط لها مخصص، مش تستنى لحد ما الدين يضيع. استشر محاسبك في الاتنين.",
      ],
      en: [
        "Receivables: money owed to you by people you sold to on credit. Payables: money you owe suppliers you bought from on credit.",
        "The aging report splits overdue amounts by age: not yet due, one month late, two months, and more.",
        "The older a debt gets, the less likely it is to be collected, so follow up early.",
        "Under Sales you find customer aging, under Purchasing supplier aging, and under Sales follow-ups you log your calls to customers.",
        "A debt you no longer expect to collect is written off in accounting as an expense called bad debt. IFRS 9 also asks you to estimate expected losses on receivables in advance and keep an allowance for them, rather than wait until a debt is lost. Check both with your accountant.",
      ],
    },
  },
  {
    id: "learn-bank-rec", routes: [], section: SECTION,
    title: { ar: "تسوية البنك", en: "Bank reconciliation" },
    keywords: "تسوية بنك كشف حساب bank reconciliation statement مطابقة",
    steps: {
      ar: [
        "تسوية البنك بتقارن رصيد حساب البنك في دفاترك برصيد كشف حساب البنك.",
        "الفرق غالباً بسبب عمليات لسه ما ظهرتش في أي من الجهتين: شيك لسه ما اتصرفش، أو رسوم بنكية ما سجلتهاش، أو إيداع في الطريق.",
        "من المحاسبة ثم التسوية البنكية ارفع كشف الحساب (Excel أو CSV)، والسيستم بيطابق الحركات تلقائياً مع قيود الدفاتر، والباقي بتطابقه بإيدك.",
        "اللي مفيش له مقابل في دفاترك (زي الرسوم) بتسجّله، واللي مفيش له مقابل في الكشف بتفضل متابعه.",
        "اعملها كل شهر قبل إقفال الفترة. ده من أحسن الطرق لاكتشاف الأخطاء بدري.",
      ],
      en: [
        "Bank reconciliation compares the bank account balance in your books with the balance on the bank statement.",
        "The difference is usually down to items that have not shown up on one side: a cheque not yet cashed, a bank fee you have not recorded, or a deposit in transit.",
        "Under Accounting, then Bank reconciliation, upload the statement (Excel or CSV); the system matches the lines to your book entries automatically, and you match the rest by hand.",
        "What has no match in your books (such as fees) you record; what has no match on the statement you keep following up.",
        "Do it every month before closing the period. It is one of the best ways to catch mistakes early.",
      ],
    },
  },
  {
    id: "learn-budgets", routes: [], section: SECTION,
    title: { ar: "الموازنة والفعلي", en: "Budget against actual" },
    keywords: "موازنة ميزانية تقديرية فعلي انحراف budget variance actual",
    steps: {
      ar: [
        "الموازنة التقديرية خطتك: كام المفروض تكسب وتصرف في كل بند عن السنة.",
        "الفعلي هو اللي حصل فعلاً في الدفاتر. الفرق بينهم اسمه الانحراف.",
        "الانحراف مش دايماً غلط: مصروف أكتر من المتوقع ممكن يبقى لإن المبيعات زادت. المهم تعرف السبب.",
        "من المحاسبة ثم الموازنات سجّل خطتك وقارنها بالفعلي كل شهر.",
        "مقارنة شهرية بسيطة بتكشف مصروف خارج عن السيطرة قبل آخر السنة.",
      ],
      en: [
        "A budget is your plan: how much you expect to earn and spend on each item over the year.",
        "Actual is what really happened in the books. The difference between them is called the variance.",
        "A variance is not always bad: spending more than planned may simply be because sales grew. What matters is knowing why.",
        "Under Accounting, then Budgets, enter your plan and compare it with actual every month.",
        "A simple monthly comparison reveals spending that is getting out of hand before the year ends.",
      ],
    },
  },
  {
    id: "learn-opening", routes: [], section: SECTION,
    title: { ar: "الأرصدة الافتتاحية", en: "Opening balances" },
    keywords: "ارصدة افتتاحية بداية opening balances start migrate نقل",
    steps: {
      ar: [
        "لو شركتك شغالة قبل ما تستخدم السيستم، لازم تدخل أرصدتها في أول يوم تشتغل فيه.",
        "اجهز ميزان مراجعة آخر يوم في نظامك القديم: أرصدة النقدية والبنك والعملاء والموردين والمخزون والأصول والقروض وحقوق الملكية.",
        "من المحاسبة ثم الأرصدة الافتتاحية دخّلها، ومجموع المدين لازم يساوي مجموع الدائن.",
        "الأفضل تبدأ عند بداية شهر أو سنة مالية، عشان التقارير تبقى مقارنة بسهولة.",
        "بعد ما تدخلها، قارن ميزان المراجعة في السيستم بالقديم. لازم يتطابقوا.",
      ],
      en: [
        "If your company was running before you started with the system, you must enter its balances on the first day you work in it.",
        "Prepare a trial balance for the last day in your old system: cash, bank, customers, suppliers, inventory, assets, loans and equity.",
        "Under Accounting, then Opening balances, enter them; total debits must equal total credits.",
        "It is best to start at the beginning of a month or financial year, so reports are easy to compare.",
        "After entering them, compare the trial balance in the system with the old one. They must match.",
      ],
    },
  },
];
