import { PaymentTerm, RestaurantOrderType, type RestaurantOrder } from "../api/types";

/** Settings of THIS device's receipt printer. Kept in the browser, because each till has its own printer. */
export type ReceiptSettings = {
  widthMm: 58 | 80;
  /** Print the receipt as soon as an order is billed. */
  autoPrint: boolean;
  /** Print a ticket for the kitchen when the cashier presses the kitchen button. */
  kitchenTickets: boolean;
  /** Shop name shown at the top (defaults to the company code). */
  header: string;
  footer: string;
};

const KEY = "roma.pos.print";
export const DEFAULT_RECEIPT_SETTINGS: ReceiptSettings = { widthMm: 80, autoPrint: false, kitchenTickets: true, header: "", footer: "" };

export function loadReceiptSettings(): ReceiptSettings {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) return { ...DEFAULT_RECEIPT_SETTINGS, ...JSON.parse(raw) };
  } catch { /* storage unavailable: use the defaults */ }
  return { ...DEFAULT_RECEIPT_SETTINGS };
}

export function saveReceiptSettings(s: ReceiptSettings) {
  try { localStorage.setItem(KEY, JSON.stringify(s)); } catch { /* ignore */ }
}

const esc = (v: unknown) => String(v ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]!));
const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const WORDS = {
  ar: { order: "طلب", table: "طاولة", dineIn: "صالة", takeaway: "تيك أواي", delivery: "توصيل", item: "الصنف", qty: "الكمية", total: "الإجمالي", subtotal: "المجموع", discount: "الخصم", vat: "الضريبة", grand: "الإجمالي المستحق", invoice: "فاتورة", paid: "الدفع", cash: "نقدًا", card: "شبكة", credit: "آجل", other: "أخرى", thanks: "شكرًا لزيارتكم", kitchen: "تذكرة المطبخ", customer: "العميل", address: "العنوان", note: "ملاحظة", loyalty: "خصم نقاط الولاء", date: "التاريخ" },
  en: { order: "Order", table: "Table", dineIn: "Dine-in", takeaway: "Takeaway", delivery: "Delivery", item: "Item", qty: "Qty", total: "Total", subtotal: "Subtotal", discount: "Discount", vat: "VAT", grand: "Amount due", invoice: "Invoice", paid: "Payment", cash: "Cash", card: "Card", credit: "On account", other: "Other", thanks: "Thank you for visiting", kitchen: "KITCHEN TICKET", customer: "Customer", address: "Address", note: "Note", loyalty: "Loyalty discount", date: "Date" },
};

function pageHtml(body: string, widthMm: number, rtl: boolean): string {
  const inner = widthMm - 6;
  return `<!doctype html><html dir="${rtl ? "rtl" : "ltr"}"><head><meta charset="utf-8"><title>Receipt</title><style>
@page { size: ${widthMm}mm auto; margin: 0; }
* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; background: #fff; }
body { width: ${widthMm}mm; padding: 3mm; color: #000; font: ${widthMm === 58 ? 11 : 12}px/1.45 "Courier New", ui-monospace, monospace; }
.c { text-align: center; } .b { font-weight: 700; } .big { font-size: 1.3em; }
hr { border: 0; border-top: 1px dashed #000; margin: 5px 0; }
table { width: ${inner}mm; border-collapse: collapse; }
td { vertical-align: top; padding: 1px 0; } td.n { text-align: end; white-space: nowrap; padding-inline-start: 4px; }
.row { display: flex; justify-content: space-between; gap: 6px; }
.note { font-size: .9em; padding-inline-start: 8px; }
</style></head><body>${body}</body></html>`;
}

/** Prints through a hidden frame so the till page stays as it is. The browser's print dialog (or the kiosk's silent print) does the rest. */
function printHtml(html: string) {
  const frame = document.createElement("iframe");
  frame.setAttribute("aria-hidden", "true");
  frame.style.cssText = "position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden";
  document.body.appendChild(frame);
  const doc = frame.contentDocument!;
  doc.open(); doc.write(html); doc.close();
  const run = () => {
    try { frame.contentWindow?.focus(); frame.contentWindow?.print(); } finally { setTimeout(() => frame.remove(), 60_000); }
  };
  if (doc.readyState === "complete") setTimeout(run, 50); else frame.onload = () => setTimeout(run, 50);
}

function typeLabel(o: RestaurantOrder, w: typeof WORDS.ar) {
  if (o.orderType === RestaurantOrderType.DineIn) return `${w.table} ${esc(o.tableNumber ?? "")}`;
  return o.orderType === RestaurantOrderType.Delivery ? w.delivery : w.takeaway;
}

export function receiptHtml(order: RestaurantOrder, opts: { lang: "ar" | "en"; settings: ReceiptSettings; invoiceNumber?: string | null; payment?: PaymentTerm | null; cashier?: string | null; currency?: string }): string {
  const w = WORDS[opts.lang];
  const s = opts.settings;
  const shop = esc(s.header.trim() || localStorage.getItem("companyCode") || "");
  const payLabel = opts.payment === PaymentTerm.Cash ? w.cash : opts.payment === PaymentTerm.Card ? w.card : opts.payment === PaymentTerm.Credit ? w.credit : opts.payment ? w.other : "";
  const lines = order.lines.map((l) => `<tr><td>${esc(l.itemName)}</td><td class="n">${l.quantity} × ${money(l.unitPrice)}</td><td class="n b">${money(l.lineTotal - (l.discountAmount ?? 0))}</td></tr>${l.notes ? `<tr><td colspan="3" class="note">${esc(w.note)}: ${esc(l.notes)}</td></tr>` : ""}`).join("");
  const row = (label: string, value: string, bold = false) => `<div class="row${bold ? " b big" : ""}"><span>${label}</span><span>${value}</span></div>`;
  const body = `
<div class="c b big">${shop}</div>
<div class="c">${esc(w.order)} ${esc(order.orderNumber)}${opts.invoiceNumber ? ` · ${esc(w.invoice)} ${esc(opts.invoiceNumber)}` : ""}</div>
<div class="c">${typeLabel(order, w)} · ${esc(new Date(order.orderDate).toLocaleString())}</div>
${opts.cashier ? `<div class="c">${esc(opts.cashier)}</div>` : ""}
${order.customerName || order.customerPhone ? `<div class="c">${esc(w.customer)}: ${esc(order.customerName ?? "")} ${esc(order.customerPhone ?? "")}</div>` : ""}
${order.deliveryAddress ? `<div class="c">${esc(w.address)}: ${esc(order.deliveryAddress)}</div>` : ""}
<hr><table>${lines}</table><hr>
${row(w.subtotal, money(order.grossSubTotal))}
${order.totalDiscount > 0 ? row(w.discount, "-" + money(order.totalDiscount)) : ""}
${order.loyaltyDiscountAmount > 0 ? row(w.loyalty, "-" + money(order.loyaltyDiscountAmount)) : ""}
${row(`${w.vat} ${(order.vatRate * 100).toFixed(0)}%`, money(order.vatAmount))}
<hr>${row(w.grand, `${money(order.totalAmount)} ${esc(opts.currency ?? "")}`, true)}
${payLabel ? row(w.paid, payLabel) : ""}
<hr><div class="c">${esc(s.footer.trim() || w.thanks)}</div>`;
  return pageHtml(body, s.widthMm, opts.lang === "ar");
}

export function kitchenTicketHtml(order: RestaurantOrder, opts: { lang: "ar" | "en"; settings: ReceiptSettings }): string {
  const w = WORDS[opts.lang];
  const lines = order.lines.map((l) => `<tr><td class="b big">${l.quantity} ×</td><td class="b big">${esc(l.itemName)}</td></tr>${l.notes ? `<tr><td></td><td class="note">${esc(w.note)}: ${esc(l.notes)}</td></tr>` : ""}`).join("");
  const body = `
<div class="c b big">${esc(w.kitchen)}</div>
<div class="c b big">${typeLabel(order, w)}</div>
<div class="c">${esc(w.order)} ${esc(order.orderNumber)} · ${esc(new Date().toLocaleTimeString())}</div>
<hr><table>${lines}</table><hr>`;
  return pageHtml(body, opts.settings.widthMm, opts.lang === "ar");
}

export function printReceipt(order: RestaurantOrder, opts: Parameters<typeof receiptHtml>[1]) { printHtml(receiptHtml(order, opts)); }
export function printKitchenTicket(order: RestaurantOrder, opts: Parameters<typeof kitchenTicketHtml>[1]) { printHtml(kitchenTicketHtml(order, opts)); }
