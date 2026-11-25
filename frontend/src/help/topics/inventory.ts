import type { HelpTopic } from "../helpTypes";

export const TOPICS: HelpTopic[] = [
  {
    id: "inv-items",
    routes: ["/inventory/items", "/inventory-portal", "/inventory-portal/items", "/restaurant-portal/inventory/items"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "الأصناف", en: "Items" },
    keywords: "صنف أصناف خامة تصنيف وحدة قياس حد إعادة الطلب دفعة صلاحية item category unit reorder level lot expiry",
    steps: {
      ar: [
        "اضغط «+ تصنيف» لو محتاج تصنيف جديد، واكتب كود التصنيف والاسم بالعربي والإنجليزي، ثم «حفظ التصنيف». لازم يكون في تصنيف قبل ما تضيف الأصناف.",
        "اضغط «+ صنف جديد». الكود بيتكتب تلقائي. اكتب الاسم بالعربي والإنجليزي، وحدة القياس (مثال: قطعة، كجم)، واختر التصنيف.",
        "اكتب «حد إعادة الطلب» (لما الرصيد ينزل عنه الصنف يظهر باللون الأحمر) و«مدة التوريد (أيام)».",
        "لو الصنف له تاريخ صلاحية، فعّل «تتبع بالدُفعات وتاريخ الصلاحية». أي استلام للصنف ده هيطلب رقم دُفعة وتاريخ صلاحية.",
        "اضغط «حفظ». عشان تضيف أصناف كتير مرة واحدة اضغط «إضافة سريعة»، واختر التصنيف والوحدة، والصق الأسماء اسم في كل سطر.",
        "من الجدول تقدر تشوف الرصيد الحالي ومتوسط التكلفة، وتضغط «تعديل» أو «حذف» على أي صنف.",
        "الباركود: اكتب باركود الصنف في خانة «الباركود»، أو وجّه قارئ الباركود (أو زر 📷 كاميرا على الموبايل) عليه وهيتكتب لوحده. مينفعش باركود يتكرر على صنفين. بعد كده تقدر تضيف الصنف في فاتورة المبيعات أو المشتريات أو الجرد بمسح الباركود بس، ولو مسحته تاني بتزيد الكمية واحد، وتبحث عن الصنف في القائمة بالباركود.",
      ],
      en: [
        "Press “+ تصنيف” (new category) if you need one: enter the category code and the Arabic and English names, then “Save category”. You need a category before adding items.",
        "Press “New item”. The code is filled in automatically. Enter the Arabic and English names, the unit of measure (for example piece, kg) and pick the category.",
        "Enter the “Reorder level” (an item at or below it shows in red) and the “Lead time (days)”.",
        "If the item has an expiry date, turn on “Track by lot and expiry date”. Every receipt of that item will then ask for a lot number and expiry date.",
        "Press “Save”. To add many items at once press “Quick add”, choose the category and unit, and paste the names one per line.",
        "The table shows quantity on hand and average cost, with “Edit” and “Delete” on every item.",
        "Barcode: type the item's barcode in the “Barcode” box, or point a barcode scanner (or the 📷 camera button on a phone) at it and it fills in by itself. Two items cannot share a barcode. After that you can add the item to a sales invoice, a purchase invoice or a stock count just by scanning it; scanning it again adds one to the quantity; and you can find an item in the list by its barcode.",
      ],
    },
    tips: {
      ar: ["كود الصنف مبيتغيرش بعد الحفظ. لو الصنف عليه حركات مخزون ممكن الحذف يترفض."],
      en: ["The item code cannot be changed after saving. Deleting an item that already has stock movements may be refused."],
    },
  },
  {
    id: "inv-warehouses",
    routes: ["/inventory/warehouses", "/inventory-portal/warehouses", "/restaurant-portal/inventory/warehouses"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "المخازن", en: "Warehouses" },
    keywords: "مخزن مخازن فرع warehouse branch store location",
    steps: {
      ar: [
        "اضغط «+ مخزن جديد».",
        "كود المخزن بيتكتب تلقائي. اكتب الاسم بالعربي والإنجليزي.",
        "اضغط «حفظ» وهيظهر المخزن في القائمة.",
        "لو عايز تمسح مخزن اضغط «حذف» قدامه.",
      ],
      en: [
        "Press “New warehouse”.",
        "The warehouse code is filled in automatically. Enter the Arabic and English names.",
        "Press “Save” and the warehouse appears in the list.",
        "To remove a warehouse press “Delete” next to it.",
      ],
    },
    tips: {
      ar: ["المخزن اللي عليه حركات مخزون ممكن النظام يرفض حذفه."],
      en: ["The system may refuse to delete a warehouse that already has stock movements."],
    },
  },
  {
    id: "inv-transfers",
    routes: ["/inventory/transfers", "/restaurant-portal/inventory/transfers"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "تحويل بين الفروع", en: "Branch transfers" },
    keywords: "تحويل نقل مخزون فرع إرسال استلام transfer branch send receive in transit",
    steps: {
      ar: [
        "اضغط «تحويل جديد». اختار «الفرع المرسل (Out)» و«الفرع المستلم (In)» والتاريخ. لازم يكونوا فرعين مختلفين.",
        "أضف الأصناف: اختار الصنف واكتب الكمية (هتشوف المتاح في الفرع المرسل). استخدم «إضافة صنف» لصنف تاني. للأصناف المتتبعة بالدُفعات اكتب رقم الدُفعة.",
        "اضغط «إرسال (Out)». التحويل بيبقى «في الطريق» والكمية بتخرج من الفرع المرسل.",
        "لما البضاعة توصل، الفرع المستلم يفتح التحويل ويضغط «استلام (In)»، ويكتب «الكمية المستلمة فعلًا»، ثم «تأكيد الاستلام».",
        "لو لسه مبعتش وعايز تلغي، استخدم «إلغاء التحويل» والكمية بترجع للفرع المرسل.",
        "استخدم الفلاتر (في الطريق، تم الاستلام، ملغي، الكل) عشان تتابع التحويلات.",
      ],
      en: [
        "Press “New transfer”. Choose the “Sending branch (Out)”, the “Receiving branch (In)” and the date. The two branches must be different.",
        "Add items: choose the item and enter the quantity (the quantity available at the sending branch is shown). Use “Add item” for more. For lot-tracked items enter the lot number.",
        "Press “Send (Out)”. The transfer becomes “In transit” and the quantity leaves the sending branch.",
        "When the goods arrive, the receiving branch opens the transfer, presses “Receive (In)”, enters the “Quantity actually received”, then “Confirm receipt”.",
        "To cancel a transfer use “Cancel transfer”; the quantity goes back to the sending branch.",
        "Use the filters (In transit, Received, Cancelled, All) to follow your transfers.",
      ],
    },
    tips: {
      ar: [
        "التحويل بينقل الكمية بنفس التكلفة ومبيأثرش على الأرباح.",
        "لو المستلم أقل من المبعوت، الفرق بيتسجل هالك على الشركة تلقائي بقيد محاسبي.",
      ],
      en: [
        "A transfer moves the quantity at the same cost and does not affect profit.",
        "If less is received than was sent, the difference is automatically written off as waste on the company with a journal entry.",
      ],
    },
  },
  {
    id: "inv-adjustments",
    routes: ["/inventory/adjustments", "/restaurant-portal/inventory/adjustments"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "تسوية مخزون", en: "Stock adjustment" },
    keywords: "تسوية زيادة نقص رصيد افتتاحي جرد adjustment increase decrease opening stock",
    steps: {
      ar: [
        "اضغط «تسوية جديدة».",
        "اختار «الفرع» و«الصنف» و«نوع التسوية»: زيادة (موجودة ومش مسجلة) أو نقص (ناقصة).",
        "اختار «السبب»: رصيد افتتاحي أو زيادة جرد أو نقص جرد أو أخرى. اكتب «الكمية» و«التاريخ».",
        "في الزيادة تقدر تكتب «تكلفة الوحدة»، ولو سيبتها فاضية النظام يستخدم متوسط تكلفة الصنف. للصنف المتتبع بالدُفعات اكتب رقم الدُفعة وتاريخ الانتهاء.",
        "اضغط «تسجيل التسوية». هيتسجل قيد محاسبي تلقائي وتظهر التسوية في القائمة.",
      ],
      en: [
        "Press “New adjustment”.",
        "Choose the “Branch”, the “Item” and the “Type”: increase (there but not recorded) or decrease (missing).",
        "Choose the “Reason”: Opening stock, Count gain, Count loss or Other. Enter the “Quantity” and “Date”.",
        "For an increase you may enter the “Unit cost”; if left empty the item's average cost is used. For lot-tracked items enter the lot number and expiry date.",
        "Press “Record adjustment”. A journal entry is posted automatically and the adjustment appears in the list.",
      ],
    },
    tips: {
      ar: ["الزيادة مبتتسجلش على مورد، والنقص بيتحمّل على تكلفة البضاعة."],
      en: ["An increase is not booked against a supplier; a decrease is charged to cost of goods."],
    },
  },
  {
    id: "inv-warehouse-stock",
    routes: ["/inventory/warehouse-stock", "/restaurant-portal/inventory/warehouse-stock"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "رصيد الفروع", en: "Branch stock" },
    keywords: "رصيد فروع مخزن كمية branch stock quantity warehouse enforce block sales",
    steps: {
      ar: [
        "افتح الصفحة، هتلاقي جدول: كل صف صنف وكل عمود فرع، وفيه الكمية الموجودة في الفرع ده.",
        "لو في بضاعة مبعوتة لفرع ولسه ماتستلمتش بتظهر جنب الكمية مكتوب «قادم».",
        "اكتب في خانة «ابحث بالكود أو الاسم» عشان تلاقي صنف بسرعة.",
        "لو انت المدير (Admin) هتلاقي خيار «منع البيع والصرف لو رصيد الفرع مش كفاية». لما يشتغل، أي بيع أو صرف من فرع بيتأكد إن الفرع نفسه عنده الكمية.",
      ],
      en: [
        "Open the page: each row is an item and each column a branch, showing the quantity held there.",
        "Stock sent to a branch and not yet received shows beside the quantity as “incoming”.",
        "Type in “Search by code or name” to find an item quickly.",
        "If you are an Admin you will see the option “Block sales and issues when the branch does not hold enough”. When on, any sale or issue from a branch checks that the branch itself holds the quantity.",
      ],
    },
    tips: {
      ar: ["لو فرع ناقص، حوّل له كمية من صفحة «تحويل بين الفروع» الأول."],
      en: ["If a branch is short, transfer stock to it first from “Branch transfers”."],
    },
  },
  {
    id: "inv-branch-minimums",
    routes: ["/inventory/branch-minimums", "/restaurant-portal/inventory/branch-minimums"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "الحد الأدنى للمخزون لكل فرع", en: "Minimum stock per branch" },
    keywords: "حد أدنى فرع تنبيه نقص minimum branch alert shortage",
    steps: {
      ar: [
        "تحت «تحديد حد أدنى» اختار «الفرع» و«الصنف» واكتب «الحد الأدنى» اللي المفروض يفضل في الفرع.",
        "اضغط «حفظ».",
        "لما فرع ينزل تحت الحد، بيظهر في جدول «فروع تحت الحد الأدنى» ومعاه الموجود والناقص و«في الطريق».",
        "في عمود «التغطية» هتلاقي اقتراح «حوّل من» فرع عنده زيادة، أو «اشتري» لو مفيش. اضغط «افتح صفحة التحويل» لتنفيذ التحويل.",
        "تحت «الحدود الحالية» تقدر تشوف كل الحدود وتضغط «إزالة» (أو اكتب 0) لإلغاء حد.",
      ],
      en: [
        "Under “Set a minimum”, choose the “Branch” and the “Item” and enter the “Minimum” the branch should keep.",
        "Press “Save”.",
        "When a branch falls below its minimum it appears in “Branches below their minimum” with what is on hand, the shortage and what is on its way.",
        "The “Cover” column suggests “Transfer from” a branch holding more, or “Buy” if none can cover. Press “Open transfers” to make the transfer.",
        "Under “Current minimums” you can see every limit and press “Remove” (or enter 0) to cancel one.",
      ],
    },
    tips: {
      ar: ["ده تنبيه بس، مفيش حاجة بتتنفذ لوحدها. الصنف من غير حد أدنى مفيهوش تنبيه."],
      en: ["This is an alert only; nothing happens by itself. An item with no minimum raises no alert."],
    },
  },
  {
    id: "inv-movements",
    routes: ["/inventory/movements", "/inventory-portal/movements", "/restaurant-portal/inventory/movements"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "حركات المخزون (استلام وصرف)", en: "Stock movements (receipt and issue)" },
    keywords: "حركة مخزون استلام صرف receipt issue movement stock in out",
    steps: {
      ar: [
        "اضغط «+ استلام مخزون» لما بضاعة تدخل، أو «+ صرف مخزون» لما بضاعة تخرج.",
        "اختار «التاريخ» و«الفترة المحاسبية» و«الصنف» و«المخزن».",
        "اكتب «الكمية» (هتشوف المتاح جنبها). في الاستلام اكتب كمان «تكلفة الوحدة».",
        "لو الصنف متتبع بالدُفعات هيطلب رقم الدُفعة وتاريخ الصلاحية. تقدر تضيف «مركز التكلفة» و«مرجع» اختياريين.",
        "اضغط «حفظ». الحركة بتظهر في الجدول برقمها ونوعها (استلام أو صرف) وتكلفتها.",
      ],
      en: [
        "Press “+ Stock receipt” when goods come in, or “+ Stock issue” when goods go out.",
        "Choose the “Date”, the “Fiscal period”, the “Item” and the “Warehouse”.",
        "Enter the “Quantity” (the available quantity is shown beside it). For a receipt also enter the “Unit cost”.",
        "A lot-tracked item asks for a lot number and expiry date. You can add an optional “Cost center” and “Reference”.",
        "Press “Save”. The movement appears in the table with its number, type (receipt or issue) and cost.",
      ],
    },
    tips: {
      ar: ["الصرف بياخد من أقرب دُفعة لانتهاء الصلاحية الأول للأصناف المتتبعة بالدُفعات."],
      en: ["For lot-tracked items, issues take from the lot closest to expiry first."],
    },
  },
  {
    id: "inv-physical-counts",
    routes: ["/inventory/physical-stock-counts", "/inventory-portal/physical-stock-counts", "/restaurant-portal/inventory/physical-stock-counts"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "الجرد الفعلي", en: "Physical stock count" },
    keywords: "جرد عد فرق نقص زيادة اعتماد تسوية مديونية موظف stock count variance approve who bears shortage",
    steps: {
      ar: [
        "اضغط «+ جرد جديد». اختار التاريخ والفرع (أو «بدون فرع (إجمالي الشركة)») والصنف، واكتب «الكمية المعدودة» (هتشوف رصيد النظام جنبها). اضغط «حفظ».",
        "النظام بيحسب «الفرق» بين رصيد النظام والمعدود وقيمته. الجرد ده لوحده مبيغيّرش الرصيد.",
        "الفروقات بتفضل «منتظر الاعتماد». المالك أو المحاسب بس هم اللي يقدروا يضغطوا «مراجعة واعتماد».",
        "لو في نقص، اختار «مين يتحمّل النقص؟»: الفرع، أو الشركة، أو موظف، أو مدير الفرع. لو اخترت موظف أو مدير لازم تختار الشخص، والنقص بيتسجل مديونية عليه.",
        "اكتب «ملاحظة القرار» ثم «اعتماد وعمل التسوية» (بيتعمل قيد محاسبي وتسوية مخزون)، أو «رفض (من غير تسوية)».",
        "تحت «فروقات الجرد حسب الفرع والصنف» هتلاقي ملخص النقص والزيادة وقيمتهم، والأكبر خسارة فوق.",
      ],
      en: [
        "Press “+ New count”. Choose the date, the branch (or “No branch (company total)”) and the item, and enter the “Counted quantity” (the system balance is shown beside it). Press “Save”.",
        "The system calculates the “Difference” between the system balance and the count, and its value. The count by itself does not change the stock balance.",
        "Differences stay “Awaiting approval”. Only the owner or accountant can press “Review and decide”.",
        "For a shortage, choose “Who bears the shortage?”: the branch, the company, an employee, or the branch manager. If you pick a person you must choose who, and the shortage is recorded as a debt on them.",
        "Add a “Decision note” then “Approve and make the adjustment” (a journal entry and stock adjustment are posted), or “Reject (no adjustment)”.",
        "Under “Count differences by branch and item” you see total shortage and surplus with values, biggest losses first.",
      ],
    },
    tips: {
      ar: [
        "مديونية الموظف بتتخصم من الراتب بإيدك من صفحة الرواتب، مش تلقائي.",
        "الفرق بالزيادة بيتسجل زيادة جرد على الفرع.",
        "بعد الاعتماد مفيش تراجع، فراجع الأرقام الأول.",
      ],
      en: [
        "An employee debt is deducted from pay by you in payroll; it is not automatic.",
        "A surplus is recorded as a count gain for the branch.",
        "Approval cannot be undone, so check the numbers first.",
      ],
    },
  },
  {
    id: "inv-waste-entries",
    routes: ["/inventory/waste-entries", "/inventory-portal/waste-entries", "/restaurant-portal/inventory/waste-entries"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "الهالك والتالف", en: "Waste and spoilage" },
    keywords: "هالك تالف منتهي الصلاحية waste damaged expired spoilage",
    steps: {
      ar: [
        "اضغط «+ تسجيل هالك».",
        "اختار «التاريخ» و«الفترة المحاسبية» و«الصنف» و«المخزن».",
        "اكتب «الكمية» (المتاح ظاهر جنبها) واختار «السبب»: هالك عام، منتهي الصلاحية، تالف، هالك تصنيع، زيادة تقديم، أو غير معروف.",
        "اكتب ملاحظات لو حابب ثم «حفظ».",
        "الهالك بيظهر في الجدول بتكلفته. تحليل الهالك بيتشاف من تقرير «تحليل الهالك».",
      ],
      en: [
        "Press “+ Record waste”.",
        "Choose the “Date”, “Fiscal period”, “Item” and “Warehouse”.",
        "Enter the “Quantity” (available quantity shown beside it) and choose the “Reason”: general waste, expired, damaged, production waste, over-portion or unknown.",
        "Add notes if you like, then “Save”.",
        "The waste appears in the table with its cost. See the “Waste analysis” report for the analysis.",
      ],
    },
    tips: {
      ar: ["تسجيل الهالك بيعمل حركة صرف مخزون حقيقية بتترحل في الحسابات زي أي صرف عادي."],
      en: ["Recording waste creates a real stock issue that posts to the accounts like any normal issue."],
    },
  },
  {
    id: "inv-manufacturing",
    routes: ["/inventory/manufacturing", "/inventory-portal/manufacturing", "/restaurant-portal/inventory/manufacturing"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "التصنيع الداخلي", en: "In-house manufacturing" },
    keywords: "تصنيع وصفة باتش إنتاج تكلفة تشغيل مصروفات ثابتة manufacturing recipe BOM batch production conversion cost overhead",
    steps: {
      ar: [
        "في «وصفات التصنيع» اضغط «+ وصفة تصنيع جديدة». اختار «الصنف الناتج» (زي صلصة أو خلطة).",
        "اكتب «كمية الباتش»، وأضف «المكوّنات الخام» بالكمية لكل باتش (اختار الخامة واكتب الكمية ثم «إضافة سطر»).",
        "اكتب في خانة الأجور المباشرة ومصروفات التشغيل المتغيرة للباتش (عمالة، طاقة، تغليف) لو عايز تكلفة التشغيل تدخل في تكلفة المنتج. اضغط «حفظ».",
        "في «أوامر الإنتاج» اضغط «+ تشغيل إنتاج جديد». اختار الوصفة والمخزن والتاريخ واكتب «الكمية المنتجة فعليًا» ثم «حفظ». لو الصنف متتبع بالدُفعات اكتب رقم الدُفعة وتاريخ الصلاحية.",
        "الجدول بيوريك لكل أمر: الخامات، التشغيل، التكلفة الثابتة المحمّلة، والإجمالي.",
      ],
      en: [
        "Under “Manufacturing recipes” press “+ New manufacturing recipe”. Choose the “Output item” (such as a sauce or mix).",
        "Enter the “Batch yield” and add the “Raw ingredients” with the quantity per batch (pick the material, enter the quantity, then add the line).",
        "Enter the direct labour and variable running cost per batch (labour, energy, packaging) if you want it included in the product cost. Press “Save”.",
        "Under “Production orders” press “+ New production run”. Choose the recipe, warehouse and date, enter the “Quantity actually produced”, then “Save”. For a lot-tracked item enter the lot number and expiry date.",
        "The table shows for every order the materials, conversion cost, fixed overhead allocated, and the total.",
      ],
    },
    tips: {
      ar: [
        "لازم تعرّف وصفة تصنيع الأول قبل ما تقدر تشغّل أمر إنتاج.",
        "التصنيع عملية داخلية ومفيش قيد محاسبي بيتعمل وقتها. المنتج الناتج بيتستخدم كمكوّن في وصفات المنيو.",
      ],
      en: [
        "You must define a recipe before you can run a production order.",
        "Manufacturing is an internal process and no journal entry is posted at that time. The output can be used as an ingredient in menu recipes.",
      ],
    },
  },
  {
    id: "inv-expiring-stock",
    routes: ["/inventory/expiring-stock", "/inventory-portal/expiring-stock", "/restaurant-portal/inventory/expiring-stock"],
    section: { ar: "المخزون", en: "Inventory" },
    title: { ar: "أصناف قريبة الصلاحية", en: "Expiring stock" },
    keywords: "صلاحية انتهاء دُفعة قريبة منتهية expiry expiring lot expired",
    steps: {
      ar: [
        "افتح الصفحة، هتلاقي الدُفعات اللي هتنتهي صلاحيتها قريب أو انتهت ولسه في المخزون.",
        "في خانة «خلال (أيام)» اكتب عدد الأيام اللي عايز تراقبها واضغط «تحديث».",
        "اقرأ الجدول: الصنف والمخزن ورقم الدُفعة والكمية وتاريخ الصلاحية والحالة (يوم متبقي أو «منتهية الصلاحية») و«القيمة المعرّضة للخطر».",
        "الدُفعات المنتهية سجّلها كهالك من صفحة «الهالك والتالف» بسبب «منتهي الصلاحية».",
      ],
      en: [
        "Open the page to see lots that will expire soon or have expired and are still in stock.",
        "In “Within (days)” enter how many days ahead you want to watch and press “Refresh”.",
        "Read the table: item, warehouse, lot number, quantity, expiry date, status (days left or “Expired”) and “Value at risk”.",
        "Record expired lots as waste from “Waste and spoilage” with the reason “Expired”.",
      ],
    },
    tips: {
      ar: ["الصفحة بتظهر بس الأصناف المفعّل لها «تتبع بالدُفعات وتاريخ الصلاحية»."],
      en: ["Only items with “Track by lot and expiry date” turned on appear here."],
    },
  },
  {
    id: "inv-report-valuation",
    routes: ["/inventory/reports/stock-valuation", "/inventory-portal/reports/stock-valuation", "/restaurant-portal/inventory/reports/stock-valuation"],
    section: { ar: "تقارير المخزون", en: "Inventory reports" },
    title: { ar: "تقييم المخزون", en: "Stock valuation" },
    keywords: "تقييم قيمة المخزون متوسط التكلفة valuation stock value average cost",
    steps: {
      ar: [
        "افتح التقرير وهيظهر مباشرة بدون اختيار فلاتر.",
        "كل صف صنف بيوريك رصيده الحالي ومتوسط التكلفة و«القيمة».",
        "القيمة = الرصيد الحالي × متوسط التكلفة. الإجمالي في آخر الجدول.",
      ],
      en: [
        "Open the report; it loads straight away with no filters.",
        "Each row shows an item's current quantity, average cost and “Value”.",
        "Value = current quantity × average cost. The total is at the bottom.",
      ],
    },
  },
  {
    id: "inv-report-reorder",
    routes: ["/inventory/reports/reorder-suggestions", "/inventory-portal/reports/reorder-suggestions", "/restaurant-portal/inventory/reports/reorder-suggestions"],
    section: { ar: "تقارير المخزون", en: "Inventory reports" },
    title: { ar: "اقتراحات إعادة الطلب", en: "Reorder suggestions" },
    keywords: "إعادة طلب اشتري نقص استهلاك مورد reorder suggestions buy consumption supplier",
    steps: {
      ar: [
        "اكتب «فترة حساب الاستهلاك (يوم)» (من 7 لـ 180).",
        "اضغط «عرض التقرير».",
        "النظام بيحسب الاستهلاك اليومي لكل صنف من الصرف الفعلي ويقارنه بالرصيد ومدة التوريد.",
        "الجدول بيوريك الأصناف اللي لازم تتطلب مع «الكمية المقترحة» و«التكلفة التقديرية» و«آخر مورد»، وحالة «عاجل: هيخلص قبل التوريد» أو «اطلب دلوقتي».",
      ],
      en: [
        "Enter the “Consumption lookback (days)” (7 to 180).",
        "Press “View report”.",
        "The system works out each item's daily consumption from actual issues and compares it with stock and lead time.",
        "The table lists items to order with the “Suggested quantity”, “Estimated cost”, “Last vendor”, and a status of “Urgent: will run out before delivery” or “Order now”.",
      ],
    },
    tips: {
      ar: ["لو كل الأصناف رصيدها كافي هتظهر رسالة بكده."],
      en: ["If all items have enough stock a message tells you so."],
    },
  },
  {
    id: "inv-report-movement",
    routes: ["/inventory/reports/movement-analysis", "/inventory-portal/reports/movement-analysis", "/restaurant-portal/inventory/reports/movement-analysis"],
    section: { ar: "تقارير المخزون", en: "Inventory reports" },
    title: { ar: "تحليل حركة المخزون", en: "Inventory movement analysis" },
    keywords: "حركة راكد بطيء سريع فائض دوران movement dead stock slow fast excess turnover",
    steps: {
      ar: [
        "اختار تاريخ «من» وتاريخ «إلى».",
        "اضغط «عرض التقرير».",
        "استخدم الأزرار: الكل، سريعة الحركة، بطيئة الحركة، راكدة، معرضة للنفاد، فائض مخزون.",
        "الجدول بيوريك الكمية المنصرفة، تكلفة البضاعة المنصرفة، أيام تغطية المخزون ومعدل الدوران لكل صنف.",
      ],
      en: [
        "Choose the “From” and “To” dates.",
        "Press “View report”.",
        "Use the buttons: All, Fast moving, Slow moving, Dead stock, At risk of stockout, Excess stock.",
        "The table shows quantity issued, cost of goods issued, days of stock cover and turnover rate per item.",
      ],
    },
  },
  {
    id: "inv-report-waste",
    routes: ["/inventory/reports/waste-analysis", "/inventory-portal/reports/waste-analysis", "/restaurant-portal/inventory/reports/waste-analysis"],
    section: { ar: "تقارير المخزون", en: "Inventory reports" },
    title: { ar: "تحليل الهالك", en: "Waste analysis" },
    keywords: "هالك تحليل سبب اتجاه waste analysis reason trend",
    steps: {
      ar: [
        "اختار «من» و«إلى» ثم «عرض التقرير».",
        "فوق هتلاقي «إجمالي تكلفة الهالك» و«إجمالي الكمية المهلكة» و«الهالك كنسبة من تكلفة البضاعة المصروفة».",
        "«الاتجاه الأسبوعي للهالك» بيوريك الهالك كل أسبوع.",
        "«أكتر الأصناف هالكًا» و«الهالك حسب السبب» بيساعدوك تعرف فين المشكلة.",
      ],
      en: [
        "Choose “From” and “To”, then “View report”.",
        "At the top you see “Total waste cost”, “Total wasted quantity” and “Waste as a share of cost of goods issued”.",
        "“Weekly waste trend” shows waste week by week.",
        "“Top wasted items” and “Waste by reason” show where the problem is.",
      ],
    },
    tips: {
      ar: ["التقرير بيعتمد على اللي سجلته في صفحة «الهالك والتالف»."],
      en: ["The report is based on what you recorded in “Waste and spoilage”."],
    },
  },
  {
    id: "inv-report-price-variance",
    routes: ["/inventory/reports/purchase-price-variance", "/inventory-portal/reports/purchase-price-variance"],
    section: { ar: "تقارير المخزون", en: "Inventory reports" },
    title: { ar: "فرق سعر الشراء", en: "Purchase price variance" },
    keywords: "فرق سعر شراء غلاء ارتفاع price variance purchase price change",
    steps: {
      ar: [
        "اختار «من» و«إلى» ثم «عرض التقرير».",
        "الجدول بيقارن «السعر الأخير» لكل صنف بـ«السعر السابق»، ويوريك «الفرق» و«نسبة التغير».",
        "راجع الأصناف اللي سعرها زاد عشان تتفاوض مع المورد.",
      ],
      en: [
        "Choose “From” and “To”, then “View report”.",
        "The table compares each item's “Latest price” with its “Previous price” and shows the “Change” amount and percent.",
        "Review items whose price went up so you can negotiate with the supplier.",
      ],
    },
    tips: {
      ar: ["الصنف اللي استلمته مرة واحدة بس مش هيظهر لأنه مفيش سعر يتقارن بيه."],
      en: ["An item received only once does not appear, because there is no earlier price to compare."],
    },
  },
  {
    id: "inv-report-recipe-cost",
    routes: ["/inventory/reports/recipe-cost", "/inventory-portal/reports/recipe-cost"],
    section: { ar: "تقارير المخزون", en: "Inventory reports" },
    title: { ar: "تكلفة الوصفة", en: "Recipe cost" },
    keywords: "تكلفة وصفة منيو سعر البيع recipe cost menu item margin",
    steps: {
      ar: [
        "افتح التقرير وهيظهر مباشرة.",
        "كل صف صنف منيو بيوريك «له وصفة؟» وتكلفته الحقيقية محسوبة من مكونات الوصفة وأسعارها الحالية، مقابل «سعر البيع».",
        "الأصناف اللي ملهاش وصفة محتاجة تضيف لها وصفة عشان تتحسب تكلفتها.",
      ],
      en: [
        "Open the report; it loads straight away.",
        "Each row is a menu item showing “Has recipe?” and its real cost, calculated from the recipe ingredients at current prices, against its “Selling price”.",
        "Items without a recipe need a recipe added so their cost can be calculated.",
      ],
    },
  },
];
