import type { HelpTopic } from "../helpTypes";

const SEC = { ar: "الموارد البشرية", en: "Human resources" };
const SEC_ME = { ar: "خدماتي كموظف", en: "My self-service" };

export const TOPICS: HelpTopic[] = [
  {
    id: "hr-people-home",
    routes: ["/people"],
    section: SEC_ME,
    title: { ar: "الصفحة الرئيسية لبوابة الموظفين", en: "People portal home" },
    keywords: "رئيسية موظف إجازة رصيد مرضي لوحة home dashboard leave balance",
    steps: {
      ar: [
        "افتح الصفحة الرئيسية، هتلاقي ترحيب باسمك وتذكير بإجازتك الجاية (لو عندك إجازة موافق عليها).",
        "شوف بطاقة \"إجازة\" وفيها عدد الأيام المتبقية، وبطاقة \"مرضي\" وفيها الأيام المأخوذة.",
        "تحت \"الإجازات القادمة\" هتلاقي إجازاتك الموافق عليها القادمة.",
        "لو معاك صلاحية مدير موارد بشرية هتلاقي زر \"تقويم الفريق\" وقسم \"نظرة سريعة على الشركة\": موظفين نشطين، حاضرين النهاردة، في إجازة، وطلبات مستنية موافقتك.",
        "من قسم الطلبات المعلقة اضغط \"موافقة\" أو \"رفض\" مباشرة.",
      ],
      en: [
        "Open the home page: you will see a greeting and a reminder of your next approved leave.",
        "Check the \"Leave\" card for remaining days and the \"Sick\" card for days taken.",
        "\"Upcoming leave\" lists your approved leave that is still ahead.",
        "If you have HR manager access you also get a \"Team calendar\" button and a company overview: active employees, present today, on leave, and requests waiting for you.",
        "In the pending requests box press \"Approve\" or \"Reject\" right away.",
      ],
    },
  },
  {
    id: "hr-departments",
    routes: ["/hr/departments", "/people/departments"],
    section: SEC,
    title: { ar: "الأقسام", en: "Departments" },
    keywords: "قسم أقسام هيكل تنظيمي department structure",
    steps: {
      ar: [
        "اضغط \"+ قسم جديد\".",
        "الكود بيتكتب لك تلقائيًا (DEP-...) وتقدر تغيّره. اكتب \"الاسم بالعربي\" و\"الاسم بالإنجليزي\".",
        "لو القسم تابع لقسم أكبر اختار \"القسم الأب (اختياري)\"، وإلا سيبه \"بدون\".",
        "اضغط \"حفظ\".",
        "للتغيير استخدم \"تعديل\" جنب القسم، وللمسح \"حذف\".",
      ],
      en: [
        "Press \"+ New department\".",
        "The code is filled in for you (DEP-...); you can change it. Enter the Arabic and English names.",
        "If it belongs to a bigger department, pick it under \"Parent department (optional)\"; otherwise leave \"None\".",
        "Press \"Save\".",
        "Use \"Edit\" beside a department to change it, or \"Delete\" to remove it.",
      ],
    },
    tips: {
      ar: ["أنشئ الأقسام الأول لأنك محتاجها عند إضافة الوظائف والموظفين."],
      en: ["Create departments first; you need them when adding positions and employees."],
    },
  },
  {
    id: "hr-positions",
    routes: ["/hr/positions", "/people/positions"],
    section: SEC,
    title: { ar: "الوظائف", en: "Positions" },
    keywords: "وظيفة مسمى وظيفي job title position",
    steps: {
      ar: [
        "اضغط \"+ وظيفة جديدة\".",
        "الكود بيتكتب تلقائيًا (JOB-...). اكتب \"المسمى بالعربي\" و\"المسمى بالإنجليزي\".",
        "اختار \"القسم\" التابعة له الوظيفة (إجباري).",
        "اضغط \"حفظ\". هتظهر الوظيفة في الجدول مع قسمها.",
        "استخدم \"تعديل\" أو \"حذف\" للتغيير.",
      ],
      en: [
        "Press \"+ New position\".",
        "The code is filled in (JOB-...). Enter the Arabic and English titles.",
        "Choose the \"Department\" it belongs to (required).",
        "Press \"Save\". The position appears in the table with its department.",
        "Use \"Edit\" or \"Delete\" to change it.",
      ],
    },
  },
  {
    id: "hr-employees",
    routes: ["/hr/employees", "/people/employees"],
    section: SEC,
    title: { ar: "الموظفون", en: "Employees" },
    keywords: "موظف موظفين ملف راتب تعيين حساب دخول بدلات صورة وجه employee staff hire login",
    steps: {
      ar: [
        "اضغط \"+ موظف جديد\" واملأ: الكود، الاسم بالعربي والإنجليزي، النوع، الحالة الاجتماعية، تاريخ التعيين، القسم، الوظيفة، الراتب الأساسي، الإيميل والتليفون.",
        "اختار \"موقع العمل\" (لو هتستخدم الحضور بالموقع) و\"رصيد الإجازة السنوية (يوم)\" و\"الجنسية\"، واضغط \"حفظ\".",
        "من الجدول اضغط \"تعديل\" لتغيير البيانات، وفيه تغيّر \"الحالة الوظيفية\": على رأس العمل، في إجازة، أو منتهي الخدمة (مع تاريخ انتهاء الخدمة).",
        "اضغط \"بدلات وخصومات\" لإضافة بنود الراتب للموظف: اختار البند، اكتب \"القيمة\"، واضغط \"إضافة\".",
        "اضغط \"إنشاء حساب دخول\" واكتب الإيميل وكلمة السر المبدئية (8 حروف/أرقام على الأقل) عشان الموظف يسجل حضوره ويطلب إجازاته.",
        "اضغط \"رفع صورة مرجعية\" لتسجيل صورة وجه الموظف (العمود \"صورة الوجه المرجعية\" بيبين متسجلة ولا لأ).",
      ],
      en: [
        "Press \"+ New employee\" and fill in: code, Arabic and English names, gender, marital status, hire date, department, position, basic salary, email and phone.",
        "Pick a \"Work location\" (if you use location-based attendance), the annual leave days and the nationality, then press \"Save\".",
        "Use \"Edit\" on a row to change details, including the employment status: Active, On leave or Terminated (with a termination date).",
        "Press \"Allowances and deductions\" to attach salary items: choose the item, enter the value, press \"Add\".",
        "Press \"Create login\" and enter an email and an initial password (at least 8 characters) so the employee can check in and request leave.",
        "Press \"Upload reference photo\" to store the employee's face photo (the column shows whether it is set).",
      ],
    },
    tips: {
      ar: [
        "الراتب الأساسي والبدلات والخصومات هي اللي بتدخل في دورات الرواتب.",
        "بعد حذف الموظف مفيش رجوع، ولو سابو الشغل الأفضل تغيّر حالته لمنتهي الخدمة.",
      ],
      en: [
        "Basic salary plus the attached items feed the payroll runs.",
        "Deleting an employee cannot be undone; for someone who left, change the status to Terminated instead.",
      ],
    },
  },
  {
    id: "hr-contracts",
    routes: ["/hr/employee-contracts", "/people/employee-contracts"],
    section: SEC,
    title: { ar: "عقود الموظفين", en: "Employee contracts" },
    keywords: "عقد عقود تجديد انتهاء contract renew expiry pdf",
    steps: {
      ar: [
        "اضغط \"+ عقد جديد\".",
        "اختار الموظف و\"نوع العقد\": دائم، محدد المدة، أو جزئي.",
        "اكتب تاريخ البداية، ولو العقد محدد المدة اكتب تاريخ الانتهاء. تقدر تضيف سبب وترفع ملف العقد (PDF فقط، لحد 10 ميجا) ثم \"حفظ\".",
        "في الجدول شوف الحالة: نشط، مُجدَّد، أو منتهي. العقد اللي قرب ينتهي بيظهر فيه \"متبقي (يوم)\"، واللي انتهى بيظهر \"انتهى — محتاج تجديد\".",
        "استخدم \"عرض\" لفتح ملف العقد، \"رفع PDF\" لإضافة ملف، \"إنهاء\" لإنهاء العقد، أو \"حذف\" لمسح السجل.",
      ],
      en: [
        "Press \"+ New contract\".",
        "Choose the employee and the contract type: Permanent, Fixed term or Part time.",
        "Enter the start date and, for fixed term, the end date. You can add a reason and attach the contract file (PDF only, up to 10 MB), then \"Save\".",
        "In the table check the status: Active, Renewed or Terminated. Contracts close to expiry show \"Days left\"; expired ones show \"Expired - needs renewal\".",
        "Use \"View\" to open the file, \"Upload PDF\" to add one, \"Terminate\" to end the contract, or \"Delete\" to remove the record.",
      ],
    },
    tips: {
      ar: ["لما تعمل عقد جديد لموظف عنده عقد نشط، القديم بيتحول تلقائيًا لـ\"مُجدَّد\" وتاريخه يفضل محفوظ."],
      en: ["Creating a new contract for an employee with an active one turns the old one into \"Renewed\" automatically and keeps its dates."],
    },
  },
  {
    id: "hr-salary-components",
    routes: ["/hr/salary-components", "/people/salary-components"],
    section: SEC,
    title: { ar: "بنود الراتب", en: "Salary components" },
    keywords: "بدل مكافأة خصم بنود الراتب allowance bonus deduction salary component",
    steps: {
      ar: [
        "اضغط \"+ بند جديد\".",
        "الكود بيتكتب تلقائيًا (SC-...). اكتب الاسم بالعربي والإنجليزي.",
        "اختار \"نوع البند\": بدل / مكافأة أو خصم.",
        "اختار \"طريقة الحساب\": مبلغ ثابت أو نسبة من الأساسي، واكتب \"القيمة الافتراضية\".",
        "لو عايز ممكن تحدد \"خاضع للضريبة\" و\"الحساب المحاسبي المرتبط (اختياري)\" ثم \"حفظ\".",
        "بعد كده اربط البند بالموظف من صفحة الموظفين، زر \"بدلات وخصومات\".",
      ],
      en: [
        "Press \"+ New component\".",
        "The code is filled in (SC-...). Enter the Arabic and English names.",
        "Choose the type: Allowance / bonus or Deduction.",
        "Choose the calculation: Fixed amount or Percentage of basic, and enter the default value.",
        "Optionally tick \"Taxable\" and pick a linked account, then \"Save\".",
        "Then attach the item to employees from the Employees page, \"Allowances and deductions\" button.",
      ],
    },
    tips: {
      ar: ["البنود المربوطة بالموظف بتتحسب تلقائيًا مع كل دورة رواتب."],
      en: ["Items attached to an employee are calculated automatically in every payroll run."],
    },
  },
  {
    id: "hr-labor-report",
    routes: ["/hr/labor-report", "/people/labor-report"],
    section: SEC,
    title: { ar: "تقرير العمالة", en: "Labor report" },
    keywords: "تقرير عمالة تكلفة نسبة مبيعات الموظف labor cost report sales per employee",
    steps: {
      ar: [
        "اختار تاريخ \"من\" وتاريخ \"إلى\".",
        "اضغط \"عرض التقرير\".",
        "هتشوف إجمالي الإيرادات، \"إجمالي الرواتب\"، و\"تكلفة العمالة كنسبة من المبيعات\".",
        "تحتها جدول \"مبيعات كل موظف\" بعدد الطلبات والإجمالي.",
      ],
      en: [
        "Pick the \"From\" and \"To\" dates.",
        "Press \"View report\".",
        "You will see total revenue, \"Total payroll\" and \"Labor cost as % of sales\".",
        "Below it, the \"Sales by employee\" table shows orders handled and totals.",
      ],
    },
    tips: {
      ar: [
        "الرواتب محسوبة من دورات الرواتب المرحّلة فعليًا فقط.",
        "المبيعات بتتنسب للجرسون اللي أخد الطلب، أو الكاشير لو مفيش جرسون.",
      ],
      en: [
        "Payroll counts only payroll runs that were actually posted.",
        "Sales are credited to the waiter who took the order, or the cashier if no waiter is recorded.",
      ],
    },
  },
  {
    id: "hr-work-locations",
    routes: ["/hr/work-locations", "/people/work-locations"],
    section: SEC,
    title: { ar: "مواقع العمل", en: "Work locations" },
    keywords: "موقع GPS نطاق حضور فرع location geofence branch attendance",
    steps: {
      ar: [
        "اضغط \"+ موقع جديد\".",
        "اكتب \"اسم الموقع\".",
        "املأ \"خط العرض\" و\"خط الطول\"، أو لو إنت واقف في المكان اضغط \"استخدم موقعي الحالي\" (اسمح للمتصفح بالوصول لموقعك).",
        "حدد \"نطاق الحضور (متر)\" وهو المسافة المسموحة حول الموقع، وخلّي \"مفعّل\" معلّم.",
        "اضغط \"حفظ\". بعدها اختار الموقع للموظف من صفحة الموظفين (\"موقع العمل\").",
      ],
      en: [
        "Press \"+ New location\".",
        "Enter the location name.",
        "Fill in latitude and longitude, or, if you are standing at the place, press \"Use my current location\" (allow the browser to access your location).",
        "Set the \"Attendance radius (m)\", the allowed distance around the spot, and keep \"Active\" ticked.",
        "Press \"Save\". Then assign the location to employees on the Employees page (\"Work location\").",
      ],
    },
    tips: {
      ar: ["النظام بيستخدم الموقع ده عشان يتحقق إن الموظف فعلًا جنب المكان وقت الحضور والانصراف."],
      en: ["Attendance uses this to check the employee is really near the place when checking in or out."],
    },
  },
  {
    id: "hr-attendance",
    routes: ["/hr/attendance", "/people/attendance"],
    section: SEC_ME,
    title: { ar: "تسجيل الحضور والانصراف", en: "Attendance check-in and out" },
    keywords: "حضور انصراف بصمة موقع سيلفي موظف attendance check in check out clock selfie",
    steps: {
      ar: [
        "افتح الصفحة من موبايلك واسمح للمتصفح بالوصول لموقعك (والكاميرا لو طلبها).",
        "عند وصولك اضغط \"تسجيل حضور\". النظام هيحدد موقعك ويتحقق إنك داخل النطاق.",
        "لو طلب صورة، التقطها. ولو مش عايز اضغط \"تسجيل بدون صورة\".",
        "قبل ما تمشي اضغط \"تسجيل انصراف\" (بيشتغل بس لو عندك حضور مفتوح).",
        "في \"سجل حضوري\" شوف أوقاتك وهل كنت \"داخل النطاق\" أو \"خارج النطاق\" ونتيجة مطابقة الوجه.",
      ],
      en: [
        "Open the page on your phone and allow location access (and the camera if asked).",
        "When you arrive press \"Check in\". The system finds your location and checks you are inside the allowed area.",
        "If a photo is requested, take it; or press \"Skip photo\" to continue without one.",
        "Before leaving press \"Check out\" (it works only when you have an open check-in).",
        "\"My attendance history\" shows your times, whether you were inside or outside the area, and the face check result.",
      ],
    },
    tips: {
      ar: [
        "لو عندك حضور مفتوح لازم تسجل انصراف الأول قبل حضور جديد.",
        "لو الصفحة بتقول إن حسابك مش مربوط بملف موظف، اضغط \"أنشئ ملفي كموظف\" أو اطلب من الموارد البشرية.",
      ],
      en: [
        "With an open check-in you must check out before checking in again.",
        "If the page says your account is not linked to an employee file, press \"Create my employee file\" or ask HR.",
      ],
    },
  },
  {
    id: "hr-my-requests",
    routes: ["/hr/my-requests", "/people/my-requests"],
    section: SEC_ME,
    title: { ar: "طلباتي (إجازة وإذن)", en: "My requests (leave and permission)" },
    keywords: "طلب إجازة مرضي إذن رصيد leave request sick permission balance",
    steps: {
      ar: [
        "فوق هتلاقي رصيد إجازتك: الاستحقاق السنوي، المستخدم، والمتبقي.",
        "اضغط \"+ طلب جديد\".",
        "اختار \"نوع الطلب\": إجازة، مرضي، إذن، أو أخرى.",
        "اكتب \"من تاريخ\" و\"إلى تاريخ (اختياري)\" و\"السبب\"، واضغط \"حفظ\".",
        "تابع حالة الطلب في الجدول: قيد الانتظار، موافَق عليه، أو مرفوض.",
      ],
      en: [
        "At the top see your leave balance: annual entitlement, used and remaining.",
        "Press \"+ New request\".",
        "Choose the type: Leave, Sick, Permission or Other.",
        "Enter \"From date\", optional \"To date\" and the reason, then \"Save\".",
        "Follow the status in the table: Pending, Approved or Rejected.",
      ],
    },
    tips: {
      ar: ["الطلب بيوصل للموارد البشرية للموافقة أو الرفض."],
      en: ["The request goes to HR for a decision."],
    },
  },
  {
    id: "hr-employee-requests",
    routes: ["/hr/employee-requests", "/people/employee-requests"],
    section: SEC,
    title: { ar: "الطلبات والموافقات", en: "Requests and approvals" },
    keywords: "موافقة طلبات إجازة رفض اعتماد approve reject leave requests",
    steps: {
      ar: [
        "افتح الصفحة، هتلاقي \"طلبات في انتظار الموافقة\" وعددها.",
        "راجع اسم الموظف ونوع الطلب والتواريخ والسبب.",
        "اضغط \"موافقة\" أو \"رفض\" على كل طلب.",
        "الجدول التاني \"كل الطلبات\" فيه كل الطلبات وحالتها.",
      ],
      en: [
        "Open the page: \"Requests awaiting approval\" shows the count.",
        "Review the employee, request type, dates and reason.",
        "Press \"Approve\" or \"Reject\" on each request.",
        "The second table, \"All requests\", lists every request with its status.",
      ],
    },
    tips: {
      ar: ["الإجازات الموافق عليها بتظهر في تقويم الفريق وتتخصم من رصيد الموظف."],
      en: ["Approved leave shows on the team calendar and counts against the employee's balance."],
    },
  },
  {
    id: "hr-calendar",
    routes: ["/people/calendar"],
    section: SEC,
    title: { ar: "تقويم الفريق", en: "Team calendar" },
    keywords: "تقويم إجازات غياب مين غايب calendar absence who is out",
    steps: {
      ar: [
        "افتح \"تقويم الفريق\" لتشوف مين في إجازة أو مريض في أي يوم.",
        "استخدم أسهم \"الشهر اللي فات\" و\"الشهر الجاي\" للتنقل.",
        "كل اسم عليه علامة \"إجازة\" أو \"مرضي\".",
        "لو مفيش أحد غايب في الشهر هتلاقي رسالة بذلك.",
      ],
      en: [
        "Open \"Team calendar\" to see who is on leave or sick on any day.",
        "Use the previous / next month arrows to move around.",
        "Each name is marked \"Leave\" or \"Sick\".",
        "If nobody is away that month, a message tells you so.",
      ],
    },
  },
  {
    id: "hr-shifts",
    routes: ["/hr/shifts", "/people/shifts"],
    section: SEC,
    title: { ar: "الورديات وجدول الشغل", en: "Shifts and roster" },
    keywords: "وردية ورديات جدول شغل مناوبة shift roster schedule",
    steps: {
      ar: [
        "اضغط \"+ وردية جديدة\" واكتب \"اسم الوردية\" والبداية والنهاية واللون، ثم \"حفظ\". الوردية اللي بتعدي منتصف الليل بتتعلّم \"تعدّي منتصف الليل\".",
        "في الجدول تقدر \"تعديل\" الوردية، تعطلها (\"معطّلة\") أو \"حذف\".",
        "تحت هتلاقي جدول الأسبوع: صف لكل موظف وعمود لكل يوم. اختار الوردية لكل يوم، أو \"إجازة\".",
        "للتنقل استخدم \"الأسبوع اللي فات\" و\"الأسبوع الجاي\".",
        "لتوفير الوقت اضغط \"انسخ الأسبوع اللي فات\" وبعدين عدّل اللي محتاج تغيير.",
      ],
      en: [
        "Press \"+ New shift\", enter the shift name, start, end and colour, then \"Save\". A shift passing midnight is marked \"Crosses midnight\".",
        "In the list you can \"Edit\", deactivate or \"Delete\" a shift.",
        "Below is the weekly grid: a row per employee and a column per day. Pick a shift for each day, or \"Day off\".",
        "Use \"Previous week\" and \"Next week\" to move.",
        "To save time press \"Copy previous week\", then adjust what changes.",
      ],
    },
    tips: {
      ar: ["الموظف بيشوف جدوله في \"جدولي\"."],
      en: ["Employees see their schedule on \"My schedule\"."],
    },
  },
  {
    id: "hr-my-schedule",
    routes: ["/hr/my-schedule", "/people/my-schedule"],
    section: SEC_ME,
    title: { ar: "جدولي", en: "My schedule" },
    keywords: "جدول ورديات الموظف my schedule shifts roster",
    steps: {
      ar: [
        "افتح \"جدولي\".",
        "هتشوف ورديات الأسبوعين الجايين بأيامها ومواعيدها.",
        "لو الصفحة فاضية يبقى مفيش ورديات متسجلة ليك لسه، اسأل مديرك.",
      ],
      en: [
        "Open \"My schedule\".",
        "You will see your shifts for the next two weeks with days and times.",
        "If it is empty, no shifts are assigned to you yet; ask your manager.",
      ],
    },
  },
  {
    id: "hr-recruitment",
    routes: ["/hr/recruitment", "/people/recruitment"],
    section: SEC,
    title: { ar: "التوظيف", en: "Recruitment" },
    keywords: "توظيف وظيفة متقدم مقابلة سيرة ذاتية recruitment job candidate interview resume careers",
    steps: {
      ar: [
        "في تبويب \"الوظائف\" اضغط \"+ وظيفة جديدة\": اكتب مسمى الوظيفة والقسم والمكان ووصفها ومتطلباتها وآخر موعد للتقديم، واختار الحالة (مسودة، مفتوحة، مقفولة) ثم \"حفظ\".",
        "الوظائف \"المفتوحة\" بس بتظهر في صفحة وظائف شركتك. اضغط \"انسخ رابط صفحة الوظائف\" وشاركه مع المتقدمين.",
        "في تبويب \"المتقدمون\" هتلاقي كل من قدّم، وتقدر تضيف متقدم بنفسك بـ\"+ متقدم يدويًا\" (الاسم والسيرة الذاتية مطلوبين).",
        "اضغط على المتقدم لفتح بياناته: غيّر \"المرحلة\" (تقدّم، فرز، مقابلة، عرض، تم التعيين، مرفوض)، اكتب \"موعد المقابلة\" و\"ملاحظاتك\"، واضغط \"حفظ\".",
        "تقدر تضغط \"تقييم بالذكاء الاصطناعي\" لتشوف \"درجة التوافق\" من 100.",
      ],
      en: [
        "On the \"Jobs\" tab press \"+ New job\": enter the title, department, location, description, requirements and closing date, choose the status (Draft, Open, Closed), then \"Save\".",
        "Only \"Open\" jobs appear on your company careers page. Press \"Copy careers page link\" and share it with applicants.",
        "On the \"Candidates\" tab you see everyone who applied; add one yourself with \"+ Add candidate\" (name and resume are required).",
        "Click a candidate to open the details: change the \"Stage\" (Applied, Screening, Interview, Offer, Hired, Rejected), set the interview time and your notes, then \"Save\".",
        "You can press \"AI screening\" to get a match score out of 100.",
      ],
    },
    tips: {
      ar: ["تقييم الذكاء الاصطناعي رأي استشاري بس، راجع السيرة بنفسك قبل أي قرار."],
      en: ["The AI screening is advisory only; read the resume yourself before any decision."],
    },
  },
  {
    id: "hr-performance",
    routes: ["/hr/performance", "/people/performance"],
    section: SEC,
    title: { ar: "تقييم الأداء", en: "Performance reviews" },
    keywords: "تقييم أداء ٣٦٠ دورة تقييم performance review 360 cycle",
    steps: {
      ar: [
        "اضغط \"+ دورة جديدة\" واكتب \"اسم الدورة\" وتاريخ البداية والنهاية، وفي \"بنود التقييم\" اكتب بند في كل سطر، ثم \"حفظ\".",
        "اضغط على الدورة في الجدول. تحت \"ضيف مقيّمين\" اختار \"الموظف المُقيَّم\" وضيف المقيّمين وعلاقة كل واحد (ذاتي، مدير، زميل، مرؤوس)، ثم أضفهم. أو اضغط \"تقييم ذاتي لكل الموظفين\".",
        "لما تجهز اضغط \"ابدأ الدورة\" عشان تتفتح للمقيّمين (الدورة المسودة بس هي اللي تتعدّل).",
        "تابع \"التقييمات المطلوبة\" ومين سلّم، وبعدين شوف \"النتايج\": متوسط الآخرين والتقييم الذاتي.",
        "لما تخلص اضغط \"اقفل الدورة\".",
      ],
      en: [
        "Press \"+ New cycle\", enter the cycle name, start and end dates, and one competency per line, then \"Save\".",
        "Click the cycle in the table. Under \"Add reviewers\" choose the employee being reviewed, add reviewers and their relationship (self, manager, peer, subordinate), then add them. Or press \"Self-review for all employees\".",
        "When ready press \"Start cycle\" to open it to reviewers (only a draft cycle can be edited).",
        "Track the \"Requested reviews\" and who submitted, then read \"Results\": others' average and the self rating.",
        "When finished press \"Close cycle\".",
      ],
    },
    tips: {
      ar: ["تعليقات الزملاء بتظهر من غير أسماء."],
      en: ["Peer comments are shown without names."],
    },
  },
  {
    id: "hr-my-reviews",
    routes: ["/hr/my-reviews", "/people/my-reviews"],
    section: SEC_ME,
    title: { ar: "تقييماتي المطلوبة", en: "Reviews I need to give" },
    keywords: "تقييم موظف زميل مدير my reviews performance rating",
    steps: {
      ar: [
        "افتح \"تقييماتي المطلوبة\"، هتلاقي الأشخاص اللي مطلوب منك تقيّمهم والدورة وعلاقتك بيهم.",
        "اضغط الزر جنب الاسم لفتح التقييم.",
        "اختار درجة من 1 لـ5 لكل بند (1 = ضعيف ... 5 = ممتاز).",
        "اكتب تعليق لو حابب (اختياري).",
        "اضغط \"تسليم التقييم\". الزر مش هيشتغل إلا بعد تقييم كل البنود.",
      ],
      en: [
        "Open \"Reviews I need to give\" to see the people you must review, the cycle and your relationship to them.",
        "Press the button beside a name to open the review.",
        "Choose a rating from 1 to 5 for every item (1 = weak ... 5 = excellent).",
        "Write a comment if you wish (optional).",
        "Press \"Submit review\". It is enabled only after every item is rated.",
      ],
    },
  },
  {
    id: "hr-surveys",
    routes: ["/hr/surveys", "/people/surveys"],
    section: SEC,
    title: { ar: "الاستبيانات", en: "Surveys" },
    keywords: "استبيان استطلاع رأي موظفين مجهول survey poll anonymous feedback",
    steps: {
      ar: [
        "اضغط \"+ استبيان جديد\" واكتب \"عنوان الاستبيان\" والوصف.",
        "لو عايز الإجابات من غير أسماء علّم \"استبيان مجهول (من غير أسماء)\".",
        "ضيف الأسئلة بـ\"+ سؤال\" واختار نوع كل سؤال: تقييم ١–٥، نعم / لا، أو إجابة مكتوبة. ثم \"حفظ\".",
        "اضغط \"افتح للموظفين\" لنشر الاستبيان. وقت ما تحب اضغط \"اقفل\".",
        "اضغط \"النتايج\" لعرض عدد الردود والتوزيع لكل سؤال.",
      ],
      en: [
        "Press \"+ New survey\" and enter the title and description.",
        "Tick \"Anonymous survey (no names)\" if answers should not show names.",
        "Add questions with \"+ Question\" and choose each type: Rating 1-5, Yes / No, or Written answer. Then \"Save\".",
        "Press \"Open to employees\" to publish it, and \"Close\" whenever you want to stop it.",
        "Press \"Results\" to see the number of responses and the distribution per question.",
      ],
    },
    tips: {
      ar: [
        "الإجابات المكتوبة بتفضل مخفية لحد ما يجاوب ٣ أشخاص على الأقل.",
        "الاستبيان المسودة بس هو اللي يتعدّل.",
      ],
      en: [
        "Written answers stay hidden until at least 3 people have answered.",
        "Only a draft survey can be edited.",
      ],
    },
  },
  {
    id: "hr-my-surveys",
    routes: ["/hr/my-surveys", "/people/my-surveys"],
    section: SEC_ME,
    title: { ar: "الاستبيانات المفتوحة", en: "Open surveys" },
    keywords: "استبيان جاوب سؤال my surveys answer",
    steps: {
      ar: [
        "افتح \"الاستبيانات المفتوحة\" وشوف القائمة.",
        "الاستبيان اللي جاوبت عليه عليه علامة \"جاوبت ✓\".",
        "اضغط الزر جنب الاستبيان لفتحه، وجاوب على كل سؤال (تقييم، نعم / لا، أو كتابة).",
        "اضغط \"إرسال\". الزر بيشتغل لما تجاوب كل المطلوب.",
      ],
      en: [
        "Open \"Open surveys\" to see the list.",
        "A survey you already answered is marked \"Answered ✓\".",
        "Press the button beside a survey, then answer every question (rating, yes / no or text).",
        "Press \"Send\". It is enabled once everything required is answered.",
      ],
    },
    tips: {
      ar: ["لو الاستبيان مجهول، اسمك مش بيتسجل مع إجاباتك."],
      en: ["In an anonymous survey your name is not recorded with your answers."],
    },
  },
  {
    id: "hr-users",
    routes: ["/people/users"],
    section: SEC,
    title: { ar: "المستخدمون", en: "Users" },
    keywords: "مستخدم مستخدمين صلاحيات باسورد دور موظف pin user roles permissions password",
    steps: {
      ar: [
        "اضغط \"+ مستخدم جديد\" واكتب الاسم بالكامل والبريد وكلمة المرور، واختار \"الأدوار\" (مدير، محاسب، مدير موارد بشرية، موظف) ثم احفظ.",
        "في الجدول اضغط \"تعديل الأدوار\" لتغيير الأدوار ثم \"حفظ الأدوار\".",
        "اختار \"الموظف المرتبط\" من القائمة لربط المستخدم بملف موظف عشان يسجل حضوره.",
        "لتغيير كلمة المرور اضغط \"تغيير الباسورد\" واكتب الجديدة (8 أحرف على الأقل) ثم \"حفظ\".",
        "اضغط \"إيقاف\" لمنع مستخدم من الدخول، و\"تفعيل\" لإرجاعه.",
      ],
      en: [
        "Press \"+ New user\", enter the full name, email and password, choose the roles (Admin, Accountant, HR manager, Employee) and save.",
        "In the table press \"Edit roles\" to change them, then \"Save roles\".",
        "Pick the \"Linked employee\" to connect the user to an employee file so they can check in.",
        "To change a password press \"Reset password\", enter the new one (at least 8 characters) and \"Save\".",
        "Press \"Deactivate\" to stop a user from logging in, and \"Activate\" to restore access.",
      ],
    },
    tips: {
      ar: ["مسح المستخدم نهائي ومينفعش يترجع، الأفضل \"إيقاف\" بدل المسح."],
      en: ["Deleting a user is permanent; prefer \"Deactivate\"."],
    },
  },
];
