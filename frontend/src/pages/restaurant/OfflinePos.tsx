import { useMemo, useState } from "react";
import { PaymentTerm, RestaurantOrderType, type MenuItem } from "../../api/types";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";
import { discardTicket, enqueueTicket, retryTicket, type OutboxTicket, type PosSnapshot } from "../../offline/posOffline";

/** Pending and refused tickets waiting on this device — shown both offline and after the connection returns. */
export function OfflineQueueNotice({ queue, syncing, onSyncNow, onChanged }: {
  queue: OutboxTicket[];
  syncing: boolean;
  onSyncNow: () => void;
  onChanged: () => void;
}) {
  const { t } = useLanguage();
  const pending = queue.filter((q) => q.status === "pending");
  const failed = queue.filter((q) => q.status === "failed");
  if (queue.length === 0) return null;

  return (
    <div className="card" style={{ marginBottom: 12 }}>
      {pending.length > 0 && (
        <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
          <strong>{t.restaurant.offlineQueued}: {pending.length}</strong>
          <button className="btn btn-secondary btn-sm" disabled={syncing} onClick={onSyncNow}>
            {syncing ? t.restaurant.offlineSyncing : t.restaurant.offlineSyncNow}
          </button>
        </div>
      )}
      {failed.length > 0 && (
        <div style={{ marginTop: pending.length > 0 ? 12 : 0 }}>
          <strong className="text-danger">{t.restaurant.offlineFailedTitle}</strong>
          {failed.map((ticket) => (
            <div key={ticket.clientId} style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap", padding: "6px 0", borderBottom: "1px solid var(--border-color, #eee)" }}>
              <span>#{ticket.localNumber} · {ticket.displayTotal.toLocaleString()}</span>
              <span className="text-danger" style={{ flex: 1 }}>{ticket.error}</span>
              <button className="btn btn-secondary btn-sm" onClick={async () => { await retryTicket(ticket); onChanged(); }}>
                {t.restaurant.offlineRetry}
              </button>
              <button
                className="btn btn-secondary btn-sm"
                onClick={async () => {
                  if (window.confirm(t.restaurant.offlineDiscardConfirm)) {
                    await discardTicket(ticket.clientId);
                    onChanged();
                  }
                }}
              >
                {t.restaurant.offlineDiscard}
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/**
 * The cashier's screen while there is no internet: pick items, take cash/card, save the ticket on the device.
 * It works from the snapshot saved the last time the POS loaded online and never talks to the server.
 */
export default function OfflinePos({ snapshot, queue, syncing, onSyncNow, onQueueChanged, exitButton }: {
  snapshot: PosSnapshot | null;
  queue: OutboxTicket[];
  syncing: boolean;
  onSyncNow: () => void;
  onQueueChanged: () => void;
  exitButton: React.ReactNode;
}) {
  const { t, lang } = useLanguage();
  const [orderType, setOrderType] = useState<RestaurantOrderType>(RestaurantOrderType.DineIn);
  const [tableId, setTableId] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [paymentTerm, setPaymentTerm] = useState<PaymentTerm>(PaymentTerm.Cash);
  const [cart, setCart] = useState<Record<string, number>>({});
  const [activeCategory, setActiveCategory] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const menuByCategory = useMemo(() => {
    const groups = new Map<string, MenuItem[]>();
    for (const item of snapshot?.menu ?? []) {
      const list = groups.get(item.categoryName) ?? [];
      list.push(item);
      groups.set(item.categoryName, list);
    }
    return Array.from(groups.entries());
  }, [snapshot]);

  const menuById = useMemo(() => new Map((snapshot?.menu ?? []).map((m) => [m.id, m])), [snapshot]);
  const category = menuByCategory.some(([c]) => c === activeCategory) ? activeCategory : menuByCategory[0]?.[0] ?? null;
  const activeItems = menuByCategory.find(([c]) => c === category)?.[1] ?? [];

  const cartLines = Object.entries(cart).filter(([, qty]) => qty > 0);
  const net = cartLines.reduce((sum, [id, qty]) => sum + (menuById.get(id)?.menuPrice ?? 0) * qty, 0);
  const total = net * (1 + (snapshot?.vatRate ?? 0));

  const today = new Date().toISOString().slice(0, 10);
  const period = snapshot?.periods.find((p) => !p.isClosed && p.startDate.slice(0, 10) <= today && p.endDate.slice(0, 10) >= today)
    ?? snapshot?.periods.find((p) => !p.isClosed);

  if (!snapshot || snapshot.menu.length === 0) {
    return (
      <div className="pos-page">
        <div className="page-header"><h1>{t.restaurant.offlineTitle}</h1>{exitButton}</div>
        <div className="alert-error">{t.restaurant.offlineNoData}</div>
      </div>
    );
  }

  function change(itemId: string, delta: number) {
    setMessage(null);
    setCart((prev) => ({ ...prev, [itemId]: Math.max(0, (prev[itemId] ?? 0) + delta) }));
  }

  async function handleSave() {
    if (!snapshot || !period || cartLines.length === 0) return;
    if (orderType === RestaurantOrderType.DineIn && !tableId) return;
    setSaving(true);
    try {
      const table = snapshot.tables.find((tb) => tb.id === tableId);
      const ticket = await enqueueTicket({
        orderType,
        tableId: orderType === RestaurantOrderType.DineIn ? tableId : null,
        customerName: customerName.trim() || null,
        customerPhone: null,
        deliveryAddress: null,
        warehouseId: snapshot.warehouses[0].id,
        paymentTerm,
        fiscalPeriodId: period.id,
        cashierShiftId: snapshot.activeShiftId,
        discountAmount: 0,
        notes: null,
        lines: cartLines.map(([itemId, quantity]) => ({ itemId, quantity })),
        displayTotal: total,
        tableLabel: table?.number ?? null,
      });
      setCart({});
      setCustomerName("");
      setMessage(t.restaurant.offlineSaved.replace("{n}", String(ticket.localNumber)));
      onQueueChanged();
    } finally {
      setSaving(false);
    }
  }

  const canSave = cartLines.length > 0 && !!period && snapshot.warehouses.length > 0
    && (orderType !== RestaurantOrderType.DineIn || !!tableId) && !saving;

  return (
    <div className="pos-page">
      <div className="page-header"><h1>{t.restaurant.offlineTitle}</h1>{exitButton}</div>
      <div className="alert-error" style={{ marginBottom: 8 }}>{t.restaurant.offlineBanner}</div>
      <p className="text-muted" style={{ marginTop: 0 }}>
        {t.restaurant.offlineLimits} · {t.restaurant.offlineLastSaved}: {new Date(snapshot.savedAt).toLocaleString(lang === "ar" ? "ar-EG" : "en-GB")}
      </p>
      {!period && <div className="alert-error">{t.restaurant.offlineNoPeriod}</div>}
      {!snapshot.activeShiftId && <div className="text-muted">{t.restaurant.offlineNoShift}</div>}
      {message && <div className="alert-success">{message}</div>}

      <OfflineQueueNotice queue={queue} syncing={syncing} onSyncNow={onSyncNow} onChanged={onQueueChanged} />

      <div className="pos-layout">
        <div className="pos-menu-area" style={{ flex: 2 }}>
          <div className="pos-category-tabs">
            {menuByCategory.map(([c]) => (
              <button key={c} type="button" className={"pos-category-tab" + (category === c ? " active" : "")} onClick={() => setActiveCategory(c)}>
                {c}
              </button>
            ))}
          </div>
          <div className="pos-menu-grid">
            {activeItems.map((item) => (
              <button key={item.id} type="button" className="pos-menu-item" onClick={() => change(item.id, 1)}>
                <span className="pos-menu-item-name">{bilingualName(item.nameAr, item.nameEn, lang)}</span>
                <span className="pos-menu-item-price">{item.menuPrice.toLocaleString()}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="pos-cart-panel">
          <div className="pos-cart-header">
            <div style={{ fontWeight: 700 }}>{t.restaurant.offlineCart}</div>
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 8, padding: "0 4px 8px" }}>
            <select value={orderType} onChange={(e) => setOrderType(Number(e.target.value) as RestaurantOrderType)}>
              <option value={RestaurantOrderType.DineIn}>{t.restaurant.dineIn}</option>
              <option value={RestaurantOrderType.Takeaway}>{t.restaurant.takeaway}</option>
            </select>
            {orderType === RestaurantOrderType.DineIn ? (
              <select value={tableId} onChange={(e) => setTableId(e.target.value)}>
                <option value="">{t.restaurant.table}…</option>
                {snapshot.tables.map((tb) => (
                  <option key={tb.id} value={tb.id}>{t.restaurant.table} {tb.number}</option>
                ))}
              </select>
            ) : (
              <input placeholder={t.restaurant.customerName} value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
            )}
          </div>

          <div className="pos-cart-lines">
            {cartLines.length === 0 && <div className="text-muted" style={{ padding: "12px 0" }}>{t.restaurant.offlineEmptyCart}</div>}
            {cartLines.map(([id, qty]) => {
              const item = menuById.get(id);
              if (!item) return null;
              return (
                <div key={id} className="pos-cart-line">
                  <div className="pos-cart-line-info"><div>{bilingualName(item.nameAr, item.nameEn, lang)}</div></div>
                  <div className="pos-cart-line-qty">
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => change(id, -1)}>-</button>
                    <span>{qty}</span>
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => change(id, 1)}>+</button>
                  </div>
                  <div className="pos-cart-line-total">{(item.menuPrice * qty).toLocaleString()}</div>
                </div>
              );
            })}
          </div>

          <div className="pos-cart-totals">
            <div className="pos-cart-total-grand"><span>{t.restaurant.offlineTotalEstimate}</span><span>{total.toLocaleString(undefined, { maximumFractionDigits: 2 })}</span></div>
          </div>
          <div style={{ display: "flex", gap: 8, padding: "8px 4px" }}>
            <button type="button" className={"btn " + (paymentTerm === PaymentTerm.Cash ? "" : "btn-secondary")} onClick={() => setPaymentTerm(PaymentTerm.Cash)}>💵 {t.paymentTerm.cash}</button>
            <button type="button" className={"btn " + (paymentTerm === PaymentTerm.Card ? "" : "btn-secondary")} onClick={() => setPaymentTerm(PaymentTerm.Card)}>💳 {t.paymentTerm.card}</button>
          </div>
          <button className="btn pos-bill-btn" disabled={!canSave} onClick={handleSave}>{t.restaurant.offlineSaveTicket}</button>
        </div>
      </div>
    </div>
  );
}
