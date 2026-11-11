export interface HelpTopic { q: string; a: string }

/** Ready-made answers shown at the top of the Support page, so most questions (especially from free-plan companies) are
 *  answered without opening a ticket. Kept in plain data so adding a topic is one line. */
export const helpTopics: { ar: HelpTopic[]; en: HelpTopic[] } = {
  ar: [
    { q: "الشكل القديم لسه ظاهر بعد التحديث", a: "اضغط Ctrl + Shift + R مع بعض (على الموبايل: امسح بيانات الموقع من إعدادات المتصفح) عشان النسخة الجديدة تتحمّل." },
    { q: "نسيت كلمة السر", a: "من صفحة الدخول اضغط «نسيت كلمة السر» وهيوصلك رابط على الإيميل. لو ما وصلش شوف مجلد الرسائل المزعجة." },
    { q: "إزاي أضيف مستخدم أو كاشير؟", a: "من «المستخدمين» اضغط إضافة. الكاشير محتاج دور «موظف» ورقم سري (PIN) من «إعداد المطعم». عدد المستخدمين بيتحدد حسب باقتك من صفحة «اشتراكي»." },
    { q: "وصلت للحد الأقصى في النسخة المجانية", a: "كل نسخة مجانية ليها حدود (مستخدمين، فروع، موظفين). افتح «اشتراكي» وشوف استخدامك، ولو احتجت أكتر رقّي الباقة وكل بياناتك بتفضل زي ما هي." },
    { q: "إزاي أفتح وردية الكاشير؟", a: "من نقطة البيع اضغط «فتح وردية»، اكتب الرصيد الافتتاحي واتأكد إن الموظف مربوط بالحساب. أول مرة؟ ابدأ بمعالج إعداد المطعم." },
    { q: "إزاي أستورد المنيو من إكسل؟", a: "من «إعداد المطعم» ← «استيراد المنيو» نزّل النموذج، املاه، وارفعه. لو أي صف فيه خطأ هيتعرض وما بيتحفظش حاجة لحد ما تصلحه." },
    { q: "فاتورة المشتريات مستنية اعتماد", a: "فواتير المشتريات اللي بيسجلها غير المعتمدين بتستنى اعتماد الأدمن أو المحاسب. هيوصلهم إشعار، وتقدر تتابعها من «فواتير المشتريات»." },
    { q: "المساعد الذكي مش شغال", a: "المساعد خدمة إضافية بالرصيد في النسخ المجانية. شوف رصيدك في «اشتراكي»، ولو صفر تواصل معانا لشحنه أو رقّي الباقة." },
    { q: "إزاي أرقّي باقتي؟", a: "من «اشتراكي» اختار الباقة المناسبة. الترقية بتحافظ على كل بياناتك." },
  ],
  en: [
    { q: "I still see the old look after an update", a: "Press Ctrl + Shift + R together (on a phone, clear the site data in the browser settings) so the new version loads." },
    { q: "I forgot my password", a: "On the sign-in page choose \"Forgot password\" and a link is emailed to you. If it does not arrive, check your spam folder." },
    { q: "How do I add a user or a cashier?", a: "Go to Users and choose Add. A cashier needs the Employee role and a PIN from Restaurant setup. The number of users depends on your plan, see My subscription." },
    { q: "I reached the limit of the free plan", a: "Each free plan has limits (users, branches, employees). Open My subscription to see your usage, and upgrade if you need more; all your data stays as it is." },
    { q: "How do I open a cashier shift?", a: "In the POS press Open shift, enter the opening cash and make sure the employee is linked to the account. First time? Start with the restaurant setup wizard." },
    { q: "How do I import the menu from Excel?", a: "Restaurant setup, then Import menu: download the template, fill it in and upload it. If any row has an error it is shown and nothing is saved until you fix it." },
    { q: "A purchase invoice is waiting for approval", a: "Purchase invoices entered by users who cannot approve wait for an Admin or Accountant. They are notified, and you can follow it under Purchase invoices." },
    { q: "The AI assistant is not working", a: "On the free plans the assistant is an add-on paid by credits. Check your balance in My subscription; if it is zero, contact us to top up or upgrade." },
    { q: "How do I upgrade my plan?", a: "Choose the plan you need in My subscription. Upgrading keeps all your data." },
  ],
};
