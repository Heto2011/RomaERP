import type { HelpTopic } from "../helpTypes";

const SEC = { ar: "التحليل المالي", en: "Financial analysis" };
const REV = { ar: "المراجعة", en: "Review" };

export const TOPICS: HelpTopic[] = [
  {
    id: "acc-a-cash-flow",
    routes: ["/accounting/cash-flow"],
    section: SEC,
    title: { ar: "قائمة التدفقات النقدية", en: "Cash flow statement" },
    keywords: "تدفقات نقدية سيولة كاش نقدية cash flow cash in cash out",
    steps: {
      ar: [
        "افتح المحاسبة ثم «قائمة التدفقات النقدية».",
        "اختار «من تاريخ» و«إلى تاريخ» (الافتراضي من أول الشهر لحد النهارده).",
        "دوس «عرض التقرير».",
        "هتشوف الرصيد النقدي في بداية الفترة، ثم «التدفقات الداخلة» و«التدفقات الخارجة» لكل بند.",
        "في الآخر: إجمالي الداخل، إجمالي الخارج، «صافي التغير في النقدية»، و«الرصيد النقدي في نهاية الفترة».",
        "استخدم شريط التصدير فوق التقرير لو عايز تحفظه أو تطبعه.",
      ],
      en: [
        "Open Accounting, then \"Cash Flow Statement\".",
        "Pick the \"From\" and \"To\" dates (default is the start of this month until today).",
        "Click \"View Report\".",
        "You see the beginning cash balance, then \"Cash In\" and \"Cash Out\" by category.",
        "At the bottom: total in, total out, \"Net Change in Cash\" and the \"Ending Cash Balance\".",
        "Use the export bar above the report to save or print it.",
      ],
    },
  },
  {
    id: "acc-a-cash-flow-intelligence",
    routes: ["/accounting/cash-flow-intelligence"],
    section: SEC,
    title: { ar: "توقع السيولة (13 أسبوع)", en: "Cash flow forecast (13 weeks)" },
    keywords: "توقع سيولة رصيد نقدي أسابيع forecast liquidity cash projection",
    steps: {
      ar: [
        "افتح «توقع السيولة (13 أسبوع)». الصفحة بتتحمّل لوحدها من غير ما تدوس حاجة.",
        "شوف فوق: «الرصيد النقدي الحالي»، «متوسط صافي الحركة الأسبوعية»، و«عدد الأسابيع المستخدمة في الحساب».",
        "الرسم و«التوقع الأسبوعي» بيوروك الرصيد المتوقع كل أسبوع. الأحمر معناه إن الرصيد هيبقى سالب.",
        "لو ظهر تحذير أحمر، ده أول أسبوع الرصيد المتوقع فيه تحت الصفر. جهّز تمويل أو أجّل مصروفات قبله.",
        "الجدول تحت بيوضح لكل أسبوع «صافي الحركة المتوقع» و«الرصيد المتوقع».",
      ],
      en: [
        "Open \"Cash Flow Forecast (13 Weeks)\". It loads by itself, nothing to click.",
        "At the top see the \"Current Cash Balance\", \"Average Weekly Net Change\" and \"Historical Weeks Used\".",
        "The chart and the weekly projection show the expected balance for each week. Red means the balance will be negative.",
        "A red warning marks the first week the projected balance falls below zero. Plan funding or delay spending before it.",
        "The table below lists each week's \"Projected Net Change\" and \"Projected Ending Balance\".",
      ],
    },
    tips: {
      ar: [
        "ده توقع مبني على متوسط الأسابيع الماضية، مش ميزانية. لو البيانات أقل من 4 أسابيع هيظهر تنبيه إن الثقة قليلة.",
      ],
      en: [
        "This is a projection from past weekly averages, not a budget. With fewer than 4 weeks of data a low-confidence notice appears.",
      ],
    },
  },
  {
    id: "acc-a-money-flow",
    routes: ["/accounting/money-flow"],
    section: SEC,
    title: { ar: "فين الفلوس؟", en: "Where does the money go?" },
    keywords: "مصروفات نسبة المبيعات صافي الربح expenses percentage money flow",
    steps: {
      ar: [
        "افتح «فين الفلوس؟».",
        "اختار «من تاريخ» و«إلى تاريخ» ثم دوس «عرض التقرير».",
        "فوق هتلاقي إجمالي المبيعات للفترة.",
        "كل مصروف ظاهر كشريط بنسبته من المبيعات ومبلغه.",
        "آخر شريط هو «هامش صافي الربح»: اللي فاضل لك من كل جنيه مبيعات.",
      ],
      en: [
        "Open \"Where Does the Money Go?\".",
        "Pick \"From\" and \"To\" dates then click \"View Report\".",
        "At the top you see total sales for the period.",
        "Each expense shows as a bar with its percentage of sales and its amount.",
        "The last bar is the \"Net Profit Margin\": what is left from each unit of sales.",
      ],
    },
    tips: {
      ar: ["لو مفيش مبيعات في الفترة هتظهر رسالة «لا توجد بيانات»."],
      en: ["If there are no sales in the period you will see a \"no data\" message."],
    },
  },
  {
    id: "acc-a-item-profitability",
    routes: ["/accounting/item-profitability"],
    section: SEC,
    title: { ar: "ربحية الأصناف", en: "Item profitability" },
    keywords: "ربحية أصناف منتجات هامش مجمل الربح item profit margin winners losers",
    steps: {
      ar: [
        "افتح «ربحية الأصناف». التقرير بيتحمّل تلقائيًا لهذا الشهر.",
        "لو عايز فترة تانية، غيّر «من تاريخ» و«إلى تاريخ» ودوس «عرض التقرير».",
        "الجدول الأول «أكثر 10 أصناف ربحية» والتاني «أكثر 10 أصناف خسارة».",
        "الأعمدة: الكود، الوصف، الكمية المباعة، الإيراد، التكلفة، مجمل الربح، الهامش.",
        "الأحمر في مجمل الربح معناه إن الصنف بيتباع بخسارة.",
      ],
      en: [
        "Open \"Item Profitability\". It loads this month automatically.",
        "For another period change \"From\" and \"To\" then click \"View Report\".",
        "The first table is the \"Top 10 Most Profitable\", the second the \"Top 10 Least Profitable\".",
        "Columns: code, description, quantity sold, revenue, cost, gross profit, margin.",
        "Red gross profit means the item is selling at a loss.",
      ],
    },
    tips: {
      ar: ["التقرير بيحسب من فواتير الأصناف المرتبطة بالمخزون. أصناف المطعم اللي بتتباع بوصفة مش مشمولة لسه."],
      en: ["It is calculated from invoices of stock items. Restaurant recipe items are not included yet."],
    },
  },
  {
    id: "acc-a-customer-profitability",
    routes: ["/accounting/customer-profitability"],
    section: SEC,
    title: { ar: "ربحية العملاء", en: "Customer profitability" },
    keywords: "ربحية عملاء هامش customer profit best customers",
    steps: {
      ar: [
        "افتح «ربحية العملاء».",
        "اختار «من تاريخ» و«إلى تاريخ» (الافتراضي من أول الشهر).",
        "دوس «عرض التقرير».",
        "هتشوف أكتر 10 عملاء ربحية وأقل 10 عملاء ربحية.",
        "لكل عميل: الإيراد، التكلفة، مجمل الربح، والهامش.",
      ],
      en: [
        "Open \"Customer Profitability\".",
        "Pick \"From\" and \"To\" dates (default is the start of this month).",
        "Click \"View Report\".",
        "You see the 10 most profitable and the 10 least profitable customers.",
        "For each customer: revenue, cost, gross profit and margin.",
      ],
    },
    tips: {
      ar: ["التقرير بيحسب من فواتير الأصناف المرتبطة بالمخزون. فواتير المطعم بالوصفة مش مشمولة لسه."],
      en: ["It is based on invoices of stock items. Restaurant recipe invoices are not included yet."],
    },
  },
  {
    id: "acc-a-branch-profitability",
    routes: ["/accounting/branch-profitability"],
    section: SEC,
    title: { ar: "الربح حسب الفرع", en: "Profit by branch" },
    keywords: "ربح فرع فروع إدخال يدوي branch profit manual entry",
    steps: {
      ar: [
        "افتح «الربح حسب الفرع». الأرقام هنا بتتكتب يدويًا، مش بتتحسب من الفواتير.",
        "دوس «إضافة».",
        "اكتب «اسم الفرع» و«الشهر» و«الإيراد» و«التكلفة» ثم دوس «حفظ».",
        "النظام بيحسب «مجمل الربح» و«الهامش» لوحده.",
        "لتعديل سطر دوس «تعديل» ثم «حفظ». للحذف دوس «حذف» وأكّد.",
      ],
      en: [
        "Open \"Profit by Branch\". The numbers here are typed in by you, not calculated from invoices.",
        "Click \"Add\".",
        "Enter \"Branch Name\", the month, \"Revenue\" and \"Cost\" then click \"Save\".",
        "The system calculates gross profit and margin by itself.",
        "To change a row click \"Edit\" then \"Save\". To remove one click \"Delete\" and confirm.",
      ],
    },
    tips: {
      ar: ["لإيجاد فروق الفروع بين شهرين استخدم صفحة «المقارنات»."],
      en: ["To compare branches between two months use the \"Comparisons\" page."],
    },
  },
  {
    id: "acc-a-sales-channel-profitability",
    routes: ["/accounting/sales-channel-profitability"],
    section: SEC,
    title: { ar: "الربح حسب قناة البيع", en: "Profit by sales channel" },
    keywords: "قناة بيع صالة تيك أواي دليفري أونلاين جملة channel dine-in takeaway delivery",
    steps: {
      ar: [
        "افتح «الربح حسب قناة البيع».",
        "اختار «من تاريخ» و«إلى تاريخ» ودوس «عرض التقرير».",
        "الجدول بيوضح لكل قناة في المطعم (صالة / تيك أواي / دليفري): الإيراد، التكلفة، مجمل الربح، الهامش.",
        "تحت، في «قنوات إضافية (أونلاين، جملة، إلخ)»، دوس «إضافة» وسجّل «اسم القناة» والشهر والإيراد والتكلفة ثم «حفظ».",
      ],
      en: [
        "Open \"Profit by Sales Channel\".",
        "Pick \"From\" and \"To\" dates and click \"View Report\".",
        "The table shows each restaurant channel (dine-in / takeaway / delivery): revenue, cost, gross profit, margin.",
        "Below, under \"Other Channels (Online, Wholesale, etc.)\", click \"Add\" and enter \"Channel Name\", month, revenue and cost then \"Save\".",
      ],
    },
    tips: {
      ar: ["الجدول الأول بيغطي أوردرات المطعم بس. القنوات الإضافية أرقام بتدخلها بنفسك."],
      en: ["The first table covers restaurant orders only. Extra channels are numbers you enter yourself."],
    },
  },
  {
    id: "acc-a-margin-analysis",
    routes: ["/accounting/margin-analysis"],
    section: SEC,
    title: { ar: "تحليل الهوامش", en: "Margin analysis" },
    keywords: "هامش ربح إجمالي صافي مساهمة margin gross net contribution",
    steps: {
      ar: [
        "افتح «تحليل الهوامش».",
        "اختار «من تاريخ» و«إلى تاريخ» ودوس «عرض التقرير».",
        "هتشوف هامش الربح الإجمالي، هامش الربح الصافي، وهامش المساهمة كنسب مئوية.",
        "قسم «الربح الحقيقي بعد جميع التكاليف» بيمشي من الإيراد، ناقص التكلفة، يساوي مجمل الربح، ناقص المصروفات التشغيلية، يساوي صافي الربح.",
      ],
      en: [
        "Open \"Margin Analysis\".",
        "Pick \"From\" and \"To\" dates and click \"View Report\".",
        "You see gross margin, net margin and contribution margin as percentages.",
        "\"Real profit after all costs\" walks from revenue, minus cost, equals gross profit, minus operating expenses, equals net profit.",
      ],
    },
  },
  {
    id: "acc-a-break-even",
    routes: ["/accounting/break-even"],
    section: SEC,
    title: { ar: "كام لازم أبيع؟ (نقطة التعادل)", en: "How much do I need to sell? (break-even)" },
    keywords: "نقطة التعادل مبيعات مطلوبة ربح مستهدف break even target profit",
    steps: {
      ar: [
        "افتح «كام لازم أبيع؟».",
        "اختار «من تاريخ» و«إلى تاريخ» (فترة فيها مبيعات ومصروفات فعلية).",
        "اكتب «الربح المستهدف» لو عايز تحقق ربح معين (أو سيبه 0).",
        "دوس «عرض التقرير».",
        "هتشوف المصروفات الثابتة (تقريبية)، هامش المساهمة، «مبيعات نقطة التعادل»، و«المبيعات المطلوبة لتحقيق الربح المستهدف».",
        "التقسيم تحت بيوضح المطلوب أسبوعيًا ويوميًا، ومتوسط قيمة الفاتورة، وعدد الفواتير المطلوب يوميًا.",
      ],
      en: [
        "Open \"How Much Do I Need to Sell?\".",
        "Pick \"From\" and \"To\" dates (a period with real sales and expenses).",
        "Type a \"Target Profit\" if you want one (or leave 0).",
        "Click \"View Report\".",
        "You see approximate fixed costs, contribution margin, \"Break-even Sales\" and the sales needed for your target profit.",
        "The breakdown shows the weekly and daily need, average invoice value and invoices needed per day.",
      ],
    },
    tips: {
      ar: ["لو مفيش مبيعات أو مصروفات في الفترة هتظهر رسالة بتقولك تسجل فواتير مبيعات أو مصروفات الأول."],
      en: ["If the period has no sales or expenses, a message asks you to record sales invoices or expenses first."],
    },
  },
  {
    id: "acc-a-what-if",
    routes: ["/accounting/what-if"],
    section: SEC,
    title: { ar: "ماذا لو؟", en: "What if?" },
    keywords: "ماذا لو سيناريو تغيير سعر كمية تكلفة what if scenario simulation",
    steps: {
      ar: [
        "افتح «ماذا لو؟».",
        "اختار «من تاريخ» و«إلى تاريخ» ودوس «تحميل بيانات الفترة».",
        "في «الافتراضات» اكتب نسب التغيير: «تغيّر السعر %»، «تغيّر الكمية المباعة %»، «تغيّر نسبة تكلفة البضاعة %»، «تغيّر المصروفات الثابتة %».",
        "قارن في الجدول بين «الوضع الحالي» و«السيناريو الافتراضي» والتغير، شاملًا صافي الربح.",
      ],
      en: [
        "Open \"What If?\".",
        "Pick \"From\" and \"To\" dates and click \"Load Period Data\".",
        "Under the assumptions enter percentage changes: \"Price Change %\", \"Volume Sold Change %\", \"COGS Rate Change %\", \"Fixed Costs Change %\".",
        "Compare \"Baseline\" with \"What-If Scenario\" and the change in the table, including net income.",
      ],
    },
    tips: {
      ar: ["ده حساب مباشر على أرقام فترة حقيقية ومش بيحفظ حاجة ولا بيغيّر بياناتك."],
      en: ["This is a direct calculation on real period numbers. It saves nothing and changes none of your data."],
    },
  },
  {
    id: "acc-a-bottleneck",
    routes: ["/accounting/bottleneck"],
    section: SEC,
    title: { ar: "نقطة الاختناق", en: "Bottleneck analysis" },
    keywords: "اختناق سبب انخفاض الربح مقارنة فترة سابقة bottleneck drivers profit change",
    steps: {
      ar: [
        "افتح «نقطة الاختناق».",
        "اختار «من تاريخ» و«إلى تاريخ» للفترة الحالية ودوس «عرض التقرير».",
        "النظام بيقارنها بفترة سابقة بنفس الطول.",
        "شوف «الفترة السابقة» و«الفترة الحالية» و«التغير في الربح».",
        "جدول «أكبر الأسباب المؤثرة» بيرتب البنود (المبيعات والمصروفات) حسب تأثيرها على الربح.",
      ],
      en: [
        "Open \"Bottleneck Analysis\".",
        "Pick \"From\" and \"To\" for the current period and click \"View Report\".",
        "The system compares it with a previous period of the same length.",
        "See the prior period profit, current period profit and \"Change in Profit\".",
        "The \"Biggest Drivers\" table ranks sales and expense lines by their effect on profit.",
      ],
    },
  },
  {
    id: "acc-a-cost-center-analysis",
    routes: ["/accounting/cost-center-analysis"],
    section: SEC,
    title: { ar: "تحليل مراكز التكلفة", en: "Cost center analysis" },
    keywords: "مراكز تكلفة إيرادات مصروفات cost center revenue expense",
    steps: {
      ar: [
        "افتح «تحليل مراكز التكلفة».",
        "اختار «من تاريخ» و«إلى تاريخ» (الافتراضي من أول السنة).",
        "دوس «عرض التقرير».",
        "لكل مركز تكلفة هتشوف بنود الإيرادات وبنود المصروفات، ثم إجمالي الإيرادات والمصروفات و«الصافي».",
        "القيود اللي من غير مركز تكلفة بتظهر تحت «بدون مركز تكلفة».",
      ],
      en: [
        "Open \"Cost Center Analysis\".",
        "Pick \"From\" and \"To\" dates (default is the start of the year).",
        "Click \"View Report\".",
        "For each cost center you see revenue lines and expense lines, then total revenue, total expenses and \"Net\".",
        "Entries without a cost center appear under \"Unassigned\".",
      ],
    },
  },
  {
    id: "acc-a-hidden-profit",
    routes: ["/accounting/hidden-profit"],
    section: SEC,
    title: { ar: "الربح المخفي", en: "Hidden profit" },
    keywords: "ربح مخفي فرق جرد هالك تحت التكلفة hidden profit waste stock variance below cost",
    steps: {
      ar: [
        "افتح «الربح المخفي».",
        "اختار «من تاريخ» و«إلى تاريخ» ودوس «عرض التقرير».",
        "الرقم الكبير هو «إجمالي الربح المخفي» للفترة.",
        "الجدول بيفصّله حسب السبب: فرق الجرد، هالك، وأصناف تحت التكلفة، مع «الأثر على الربح».",
      ],
      en: [
        "Open \"Hidden Profit\".",
        "Pick \"From\" and \"To\" dates and click \"View Report\".",
        "The big number is the \"Total Hidden Profit\" for the period.",
        "The table breaks it down by reason: stock variance, waste and items sold below cost, with the \"Impact on Profit\".",
      ],
    },
    tips: {
      ar: ["الصفحة بتغطي الأسباب دي بس حاليًا، وأسباب تانية زي ارتفاع أسعار الشراء لسه مش متاحة."],
      en: ["Only these reasons are covered for now. Others, such as purchase price increases, are not available yet."],
    },
  },
  {
    id: "acc-a-executive-brief",
    routes: ["/accounting/executive-brief"],
    section: SEC,
    title: { ar: "التقرير التنفيذي", en: "Executive brief" },
    keywords: "تقرير تنفيذي ملخص مدير executive brief summary KPIs",
    steps: {
      ar: [
        "افتح «التقرير التنفيذي».",
        "اختار «من تاريخ» و«إلى تاريخ» ودوس «عرض التقرير».",
        "هتشوف بطاقات: إجمالي الإيرادات، صافي الربح (مع مقارنة بالفترة السابقة)، هامش الربح الإجمالي والصافي، تكلفة البضاعة والعمالة كنسبة من الإيرادات، الرصيد النقدي الحالي، والربح المخفي.",
        "تحتها جدولين: «أفضل الأصناف أداءً» و«أضعف الأصناف أداءً».",
        "لو ظهر تحذير أحمر فالرصيد النقدي المتوقع هينزل تحت الصفر في أسبوع معين.",
      ],
      en: [
        "Open \"Executive Brief\".",
        "Pick \"From\" and \"To\" dates and click \"View Report\".",
        "You see cards: total revenue, net income (compared with the prior period), gross and net margin, COGS and labor as a percentage of revenue, current cash balance and hidden profit.",
        "Below are two tables: \"Top Performing Items\" and \"Bottom Performing Items\".",
        "A red warning means projected cash will drop below zero in a given week.",
      ],
    },
    tips: {
      ar: ["كل الأرقام هنا مأخوذة من تقارير تانية في النظام، مفيش أرقام مستقلة."],
      en: ["All numbers come from other reports in the system, none are separate."],
    },
  },
  {
    id: "acc-a-comparisons",
    routes: ["/accounting/comparisons"],
    section: SEC,
    title: { ar: "المقارنات", en: "Comparisons" },
    keywords: "مقارنة فترتين فروع قنوات compare periods branches channels change",
    steps: {
      ar: [
        "افتح «المقارنات» واختار نوع المقارنة: «مقارنة فترتين (إجمالي الشركة)» أو «مقارنة الفروع أو القنوات».",
        "للفترتين: حدد تواريخ «الفترة الأولى» و«الفترة الثانية» ودوس «عرض التقرير».",
        "هتشوف الإيرادات والتكلفة وتكلفة العمالة ومجمل الربح وصافي الربح والهوامش للفترتين، مع «التغير».",
        "للفروع أو القنوات: اختار «المقارنة حسب»، ثم «الشهر الأول» و«الشهر الثاني»، ودوس «عرض التقرير».",
      ],
      en: [
        "Open \"Comparisons\" and choose the type: \"Compare two periods (whole company)\" or \"Compare branches or channels\".",
        "For periods: set the dates of \"Period A\" and \"Period B\" and click \"View Report\".",
        "You see revenue, COGS, labor cost, gross profit, net income and margins for both, with the \"Change\".",
        "For branches or channels: choose \"Compare by\", then \"Month A\" and \"Month B\", and click \"View Report\".",
      ],
    },
    tips: {
      ar: ["مقارنة الفروع والقنوات بتعتمد على الأرقام اللي سجلتها يدويًا في صفحات الربح حسب الفرع أو القناة."],
      en: ["Branch and channel comparison relies on the numbers you entered in the profit by branch / channel pages."],
    },
  },
  {
    id: "acc-a-smart-pricing",
    routes: ["/accounting/smart-pricing"],
    section: SEC,
    title: { ar: "الأسعار والتسعير الذكي", en: "Pricing & smart pricing" },
    keywords: "تسعير سعر مقترح هامش مستهدف تكلفة الوصفة pricing suggested price target margin",
    steps: {
      ar: [
        "افتح «الأسعار والتسعير الذكي».",
        "اكتب «هامش الربح المستهدف» بالنسبة المئوية (الافتراضي 30).",
        "الجدول بيعرض لكل صنف: الكود، الوصف، التكلفة، سعر البيع، الهامش الحالي.",
        "عمودا «السعر المقترح» و«الفرق عن السعر الحالي» بيتغيروا مع الهامش اللي كتبته.",
        "الهامش الأخضر وصل للمستهدف، والأحمر أقل منه.",
      ],
      en: [
        "Open \"Pricing & Smart Pricing\".",
        "Type your \"Target Margin %\" (default 30).",
        "The table lists each item: code, description, cost, selling price, current margin.",
        "\"Suggested Price\" and \"Gap vs Current Price\" update with the margin you typed.",
        "Green margin reached the target, red is below it.",
      ],
    },
    tips: {
      ar: ["الصفحة للعرض والاقتراح فقط، ومش بتغيّر أسعارك."],
      en: ["The page only suggests. It does not change your prices."],
    },
  },
  {
    id: "acc-a-forecast",
    routes: ["/accounting/forecast"],
    section: SEC,
    title: { ar: "توقعات المبيعات والأرباح", en: "Sales & profit forecast" },
    keywords: "توقعات مبيعات أرباح أسوأ أفضل حالة forecast revenue profit",
    steps: {
      ar: [
        "افتح «توقعات المبيعات والأرباح». الصفحة بتتحمّل لوحدها.",
        "فوق هتلاقي عدد «الشهور الفعلية المستخدمة» في الحساب.",
        "جدول «الإيراد» وجدول «صافي الربح» بيوروك لكل شهر قادم: «أسوأ حالة» و«المتوقع» و«أفضل حالة».",
        "الجدول الأخير بيعرض الشهور الفعلية: الإيراد والمصروفات وصافي الربح.",
      ],
      en: [
        "Open \"Sales & Profit Forecast\". It loads by itself.",
        "At the top see the number of \"Historical Months\" used.",
        "The revenue and net profit tables show each coming month as \"Worst Case\", \"Expected\" and \"Best Case\".",
        "The last table lists the actual months: revenue, expenses and net profit.",
      ],
    },
    tips: {
      ar: ["لو البيانات أقل من 3 شهور هيظهر تنبيه إن التقدير مبدئي جدًا. كل ما زادت الشهور الفعلية بقى التوقع أدق."],
      en: ["With under 3 months of data a notice says the estimate is very rough. More real months make it more accurate."],
    },
  },
  {
    id: "acc-a-general-ledger",
    routes: ["/accounting/general-ledger"],
    section: SEC,
    title: { ar: "دفتر الأستاذ", en: "General ledger" },
    keywords: "دفتر الأستاذ حساب حركة رصيد general ledger account movements",
    steps: {
      ar: [
        "افتح «دفتر الأستاذ».",
        "اختار الحساب من القائمة، ثم «من تاريخ» و«إلى تاريخ» (الافتراضي من أول السنة).",
        "دوس «عرض التقرير». الزر مش بيشتغل قبل ما تختار حساب.",
        "هتشوف رصيد أول المدة، ثم كل حركة بتاريخها ورقم القيد والبيان والمدين والدائن والرصيد.",
        "في الآخر الإجمالي ورصيد آخر المدة.",
      ],
      en: [
        "Open \"General Ledger\".",
        "Choose the account from the list, then \"From\" and \"To\" dates (default is the start of the year).",
        "Click \"View Report\". The button is disabled until you choose an account.",
        "You see the opening balance, then each movement with date, entry number, description, debit, credit and balance.",
        "At the end are the totals and the closing balance.",
      ],
    },
  },
  {
    id: "acc-a-review-reconciliation",
    routes: ["/review/reconciliation"],
    section: REV,
    title: { ar: "تسوية الحسابات مع التفاصيل", en: "Ledger against its detail" },
    keywords: "تسوية مطابقة عملاء موردين مخزون ميزان المراجعة reconciliation ledger match",
    steps: {
      ar: [
        "افتح «تسوية الحسابات مع التفاصيل». الصفحة بتتحمّل لوحدها.",
        "كل صف فحص: ميزان المراجعة، العملاء، الموردون، المخزون، عهد وسلف الموظفين.",
        "قارن «رصيد الدفاتر» بـ«رصيد التفاصيل» وشوف «الفرق».",
        "الحالة «مطابق» معناها تمام. «في فرق» (بالأحمر) معناها إن فرق أكتر من جنيه واحد ولازم تراجع التسجيل.",
      ],
      en: [
        "Open \"Ledger against its detail\". It loads by itself.",
        "Each row is a check: trial balance, receivables, payables, inventory, employee custody.",
        "Compare \"Ledger\" with \"Detail\" and see the \"Difference\".",
        "\"Matches\" is fine. \"Differs\" (red) means a gap over 1.00, so review what was recorded.",
      ],
    },
  },
  {
    id: "acc-a-review-close-checklist",
    routes: ["/review/close-checklist"],
    section: REV,
    title: { ar: "قائمة إقفال الشهر", en: "Month-end close checklist" },
    keywords: "إقفال الشهر قائمة فحص قيود مسودة إهلاك رواتب close checklist month end",
    steps: {
      ar: [
        "افتح «قائمة إقفال الشهر».",
        "اكتب «السنة» و«الشهر» ودوس «عرض».",
        "كل بند يظهر «تمام» أو «محتاج إجراء»، مثل قيود مسودة، فروق جرد، مشتريات منتظرة اعتماد، رواتب، إهلاك، ومصروفات مشتركة.",
        "دوس «افتح» جنب أي بند محتاج إجراء علشان تروح لصفحته وتخلصه.",
        "لما كل البنود تبقى تمام هتظهر رسالة «الشهر جاهز للإقفال».",
      ],
      en: [
        "Open \"Month-end close checklist\".",
        "Enter the \"Year\" and \"Month\" and click \"Show\".",
        "Each item shows \"Done\" or \"Needs action\", such as draft entries, count differences, purchases awaiting approval, payroll, depreciation and shared costs.",
        "Click \"Open\" beside an item that needs action to go to its page and finish it.",
        "When all items are done you see \"the month is ready to close\".",
      ],
    },
    tips: {
      ar: ["الصفحة بتفحص بس. الإقفال الفعلي بيتم من «إقفال الفترات المالية»."],
      en: ["This page only checks. The actual closing is done in the fiscal periods page."],
    },
  },
  {
    id: "acc-a-review-approvals",
    routes: ["/review/approvals"],
    section: REV,
    title: { ar: "سجل الاعتمادات", en: "Approvals log" },
    keywords: "اعتمادات رفض موافقة سجل approvals log audit",
    steps: {
      ar: [
        "افتح «سجل الاعتمادات». الصفحة بتتحمّل لوحدها.",
        "كل صف اعتماد أو رفض: الوقت، النوع (فرق جرد، خصم مديونية من راتب، فاتورة مشتريات، توزيع مصروفات)، التفاصيل، المبلغ.",
        "عمود «القرار» بيقول «اعتمد» أو «رفض»، و«بواسطة» بيوضح مين اتخذ القرار، مع «ملاحظة» لو كُتبت.",
      ],
      en: [
        "Open \"Approvals log\". It loads by itself.",
        "Each row is an approval or rejection: time, type (count difference, payroll debt deduction, purchase invoice, cost allocation), details, amount.",
        "\"Decision\" shows Approved or Rejected, \"By\" shows who decided, and a note if one was written.",
      ],
    },
    tips: {
      ar: ["الصفحة للاطلاع فقط ومفيهاش أزرار تغيير."],
      en: ["The page is read-only and has no change buttons."],
    },
  },
  {
    id: "acc-a-review-unusual-entries",
    routes: ["/review/unusual-entries"],
    section: REV,
    title: { ar: "القيود غير المعتادة", en: "Unusual entries" },
    keywords: "قيود غير معتادة مراجعة مبالغ مدورة معكوس unusual entries audit review",
    steps: {
      ar: [
        "افتح «القيود غير المعتادة». بتتحمّل تلقائيًا لآخر 90 يوم.",
        "غيّر «من» و«إلى» ودوس «عرض» لو عايز فترة تانية.",
        "كل قيد معاه رقمه وتاريخه ومين سجله والوصف والإجمالي.",
        "عمود «السبب» بيوضح ليه القيد ظهر: مبلغ مدوّر كبير، كبير جدًا عن المعتاد، تاريخ قديم اتسجل متأخر، قيد معكوس، أو على حقوق الملكية.",
      ],
      en: [
        "Open \"Unusual entries\". It loads the last 90 days automatically.",
        "Change \"From\" and \"To\" and click \"Show\" for another period.",
        "Each entry shows its number, date, who recorded it, description and total.",
        "\"Why\" explains the flag: large round amount, far above usual, old date recorded late, reversal, or on equity.",
      ],
    },
    tips: {
      ar: ["ده تنبيه للمراجعة مش اتهام. القيد ممكن يكون سليم تمامًا."],
      en: ["This is a prompt to review, not an accusation. The entry may be perfectly valid."],
    },
  },
  {
    id: "acc-a-review-number-gaps",
    routes: ["/review/number-gaps"],
    section: REV,
    title: { ar: "ثغرات الترقيم", en: "Numbering gaps" },
    keywords: "ثغرات ترقيم أرقام ناقصة فواتير قيود number gaps missing sequence",
    steps: {
      ar: [
        "افتح «ثغرات الترقيم». الصفحة بتتحمّل لوحدها.",
        "كل صف تسلسل: فواتير المبيعات، فواتير المشتريات، أو القيود.",
        "شوف «العدد» و«أول رقم» و«آخر رقم».",
        "عمود «ناقص» بيقول «مفيش ثغرات» أو بيعرض الأرقام الناقصة (مثلًا 106).",
      ],
      en: [
        "Open \"Numbering gaps\". It loads by itself.",
        "Each row is a sequence: sales invoices, purchase invoices or journal entries.",
        "See the count, first number and last number.",
        "\"Missing\" says \"No gaps\" or lists the missing numbers (e.g. 106).",
      ],
    },
    tips: {
      ar: ["الأرقام الناقصة ممكن تكون محذوفة أو اتعملت في اختبار. جهّز تفسير لها لأن المراجعة الضريبية بتسأل عنها."],
      en: ["Missing numbers may be deleted or test documents. Be ready to explain them, since tax reviews ask about them."],
    },
  },
];
