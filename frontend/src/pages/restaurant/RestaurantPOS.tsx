import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CashierShiftsApi, EmployeesApi, LookupsApi, LoyaltyApi, RestaurantApi, SalesApi, WarehousesApi } from "../../api/services";
import {
  PaymentTerm,
  RestaurantOrderType,
  RestaurantOrderStatus,
  RestaurantTableStatus,
  type RestaurantOrder,
  type RestaurantTable,
  type MenuItem,
  type Warehouse,
  type FiscalPeriod,
  type CashierShift,
  type LoyaltyMember,
} from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";
import { useAuth } from "../../context/AuthContext";
import { IconGrid, IconBox, IconTruck } from "../../components/icons";
import OfflinePos, { OfflineQueueNotice } from "./OfflinePos";
import { useOnlineStatus } from "../../offline/useOnlineStatus";
import { listOutbox, loadSnapshot, saveSnapshot, syncOutbox, type OutboxTicket, type PosSnapshot } from "../../offline/posOffline";

export default function RestaurantPOS() {
  const { t, lang } = useLanguage();
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [orders, setOrders] = useState<RestaurantOrder[]>([]);
  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [menu, setMenu] = useState<MenuItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeError, setEmployeeError] = useState<string | null>(null);
  const [shiftLoading, setShiftLoading] = useState(true);
  const [activeShift, setActiveShift] = useState<CashierShift | null>(null);
  const [openingFloat, setOpeningFloat] = useState(0);
  const [showCloseShift, setShowCloseShift] = useState(false);
  const [closingCountedCash, setClosingCountedCash] = useState(0);
  const [closedShiftSummary, setClosedShiftSummary] = useState<CashierShift | null>(null);

  // A reservation that was just turned into an order opens straight on it (…/pos?order=<id>).
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(() => new URLSearchParams(window.location.search).get("order"));
  const [activeCategory, setActiveCategory] = useState<string | null>(null);

  const [showNewOrder, setShowNewOrder] = useState(false);
  const [orderType, setOrderType] = useState<RestaurantOrderType>(RestaurantOrderType.DineIn);
  const [tableId, setTableId] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [customerPhone, setCustomerPhone] = useState("");
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [warehouseId, setWarehouseId] = useState("");

  const [showBillDialog, setShowBillDialog] = useState(false);
  const [billPaymentTerm, setBillPaymentTerm] = useState<PaymentTerm>(PaymentTerm.Cash);
  const [billFiscalPeriodId, setBillFiscalPeriodId] = useState("");
  const [billDeliveryPlatformName, setBillDeliveryPlatformName] = useState("");

  const [showSplitDialog, setShowSplitDialog] = useState(false);
  const [splitLineIds, setSplitLineIds] = useState<Set<string>>(new Set());

  const { online } = useOnlineStatus();
  const [vatRate, setVatRate] = useState(0);
  const [snapshot, setSnapshot] = useState<PosSnapshot | null>(null);
  const [queue, setQueue] = useState<OutboxTicket[]>([]);
  const [syncing, setSyncing] = useState(false);
  const [syncMessage, setSyncMessage] = useState<string | null>(null);

  const [loyaltyEnabled, setLoyaltyEnabled] = useState(false);
  const [showLoyaltyDialog, setShowLoyaltyDialog] = useState(false);
  const [loyaltyPhone, setLoyaltyPhone] = useState("");
  const [loyaltyName, setLoyaltyName] = useState("");
  const [loyaltyRedeem, setLoyaltyRedeem] = useState(0);
  const [loyaltyFound, setLoyaltyFound] = useState<LoyaltyMember | null>(null);

  const [showMoveDialog, setShowMoveDialog] = useState(false);
  const [moveTargetTableId, setMoveTargetTableId] = useState("");
  const [showMergeDialog, setShowMergeDialog] = useState(false);
  const [mergeSourceOrderId, setMergeSourceOrderId] = useState("");

  const selectedOrder = useMemo(() => orders.find((o) => o.id === selectedOrderId) ?? null, [orders, selectedOrderId]);
  const availableTables = tables.filter((tb) => tb.status === RestaurantTableStatus.Available);

  async function load() {
    const [ordersRes, tablesRes, menuRes, warehousesRes, periodsRes, settingsRes] = await Promise.all([
      RestaurantApi.getOrders(false),
      RestaurantApi.getTables(),
      RestaurantApi.getMenu(),
      WarehousesApi.getAll(),
      LookupsApi.fiscalPeriods(),
      LookupsApi.companySettings(),
    ]);
    setOrders(ordersRes.data);
    setTables(tablesRes.data);
    setMenu(menuRes.data);
    setWarehouses(warehousesRes.data);
    setPeriods(periodsRes.data);
    setVatRate(settingsRes.data.vatRate);
  }

  useEffect(() => {
    if (!online) return;
    load().catch(() => {});
    LoyaltyApi.getSettings().then((r) => setLoyaltyEnabled(r.data.enabled)).catch(() => setLoyaltyEnabled(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [online]);

  useEffect(() => {
    // Started with no internet: wait for the connection before asking who the cashier is and which shift is open.
    if (!online || employeeId) return;
    setEmployeeError(null);
    EmployeesApi.getMyProfile()
      .then((res) => {
        setEmployeeId(res.data.id);
        return CashierShiftsApi.getActive(res.data.id);
      })
      .then((res) => res && setActiveShift(res.data))
      .catch(() => setEmployeeError(t.restaurant.noEmployeeLinkedError))
      .finally(() => setShiftLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [online]);

  const refreshQueue = useCallback(async () => setQueue(await listOutbox()), []);

  useEffect(() => {
    loadSnapshot().then(setSnapshot);
    refreshQueue();
  }, [refreshQueue]);

  // Keep a copy of everything the offline screen needs, refreshed whenever the live data changes.
  useEffect(() => {
    if (menu.length === 0 || warehouses.length === 0) return;
    const fresh: PosSnapshot = {
      savedAt: new Date().toISOString(),
      menu, tables, warehouses, periods,
      activeShiftId: activeShift?.id ?? null,
      vatRate,
    };
    setSnapshot(fresh);
    saveSnapshot(fresh);
  }, [menu, tables, warehouses, periods, activeShift, vatRate]);

  const runSync = useCallback(async () => {
    setSyncing(true);
    try {
      const summary = await syncOutbox();
      await refreshQueue();
      if (summary.synced > 0) {
        setSyncMessage(t.restaurant.offlineSyncedOk.replace("{n}", String(summary.synced)));
        await load();
      }
      if (summary.failed > 0) setSyncMessage((m) => `${m ? m + " " : ""}${t.restaurant.offlineSyncFailed.replace("{n}", String(summary.failed))}`);
    } finally {
      setSyncing(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [refreshQueue, t]);

  // The moment the connection is back, send whatever was rung up offline.
  useEffect(() => {
    if (online && queue.some((q) => q.status === "pending")) runSync();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [online, queue.length]);

  async function handleOpenShift(e: React.FormEvent) {
    e.preventDefault();
    if (!employeeId) return;
    setError(null);
    try {
      const res = await CashierShiftsApi.open({ employeeId, openingFloat });
      setActiveShift(res.data);
      setClosedShiftSummary(null);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCloseShift(e: React.FormEvent) {
    e.preventDefault();
    if (!activeShift) return;
    setError(null);
    try {
      const res = await CashierShiftsApi.close(activeShift.id, { closingCountedCash });
      setClosedShiftSummary(res.data);
      setActiveShift(null);
      setShowCloseShift(false);
      setSelectedOrderId(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCreateOrder(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const res = await RestaurantApi.createOrder({
        orderType,
        tableId: orderType === RestaurantOrderType.DineIn ? tableId || null : null,
        customerName: orderType === RestaurantOrderType.DineIn ? null : customerName || null,
        customerPhone: orderType === RestaurantOrderType.DineIn ? null : customerPhone || null,
        deliveryAddress: orderType === RestaurantOrderType.Delivery ? deliveryAddress || null : null,
        warehouseId,
      });
      setShowNewOrder(false);
      setTableId("");
      setCustomerName("");
      setCustomerPhone("");
      setDeliveryAddress("");
      setSelectedOrderId(res.data.id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleAddItem(itemId: string) {
    if (!selectedOrder) return;
    setError(null);
    try {
      const existingLine = selectedOrder.lines.find((l) => l.itemId === itemId);
      if (existingLine) {
        await RestaurantApi.updateLineQuantity(selectedOrder.id, existingLine.id, existingLine.quantity + 1);
      } else {
        await RestaurantApi.addLine(selectedOrder.id, { itemId, quantity: 1 });
      }
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleLineQuantity(lineId: string, quantity: number) {
    if (!selectedOrder) return;
    setError(null);
    try {
      await RestaurantApi.updateLineQuantity(selectedOrder.id, lineId, quantity);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleRemoveLine(lineId: string) {
    if (!selectedOrder) return;
    setError(null);
    try {
      await RestaurantApi.removeLine(selectedOrder.id, lineId);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleLineDiscount(lineId: string) {
    if (!selectedOrder) return;
    const current = selectedOrder.lines.find((l) => l.id === lineId);
    const input = window.prompt(t.restaurant.discountPrompt, String(current?.discountAmount ?? 0));
    if (input === null) return;
    const discountAmount = Number(input);
    if (Number.isNaN(discountAmount) || discountAmount < 0) return;
    setError(null);
    try {
      await RestaurantApi.setLineDiscount(selectedOrder.id, lineId, { discountAmount });
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleOrderDiscount() {
    if (!selectedOrder) return;
    const input = window.prompt(t.restaurant.orderDiscountPrompt, String(selectedOrder.discountAmount));
    if (input === null) return;
    const discountAmount = Number(input);
    if (Number.isNaN(discountAmount) || discountAmount < 0) return;
    setError(null);
    try {
      await RestaurantApi.setOrderDiscount(selectedOrder.id, { discountAmount });
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCancelOrder() {
    if (!selectedOrder) return;
    setError(null);
    try {
      await RestaurantApi.cancelOrder(selectedOrder.id);
      setSelectedOrderId(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openSplitDialog() {
    setSplitLineIds(new Set());
    setShowSplitDialog(true);
  }

  function toggleSplitLine(lineId: string) {
    setSplitLineIds((prev) => {
      const next = new Set(prev);
      if (next.has(lineId)) next.delete(lineId);
      else next.add(lineId);
      return next;
    });
  }

  async function handleSplitOrder(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedOrder || splitLineIds.size === 0) return;
    setError(null);
    try {
      const res = await RestaurantApi.splitOrder(selectedOrder.id, { lineIds: Array.from(splitLineIds) });
      setShowSplitDialog(false);
      setSelectedOrderId(res.data.originalOrder.id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openLoyaltyDialog() {
    setLoyaltyPhone(selectedOrder?.loyaltyMemberPhone ?? selectedOrder?.customerPhone ?? "");
    setLoyaltyName(selectedOrder?.loyaltyMemberName ?? selectedOrder?.customerName ?? "");
    setLoyaltyRedeem(selectedOrder?.loyaltyPointsToRedeem ?? 0);
    setLoyaltyFound(null);
    setShowLoyaltyDialog(true);
  }

  // Look the customer up as the cashier types, so the balance and name show up before anything is saved.
  useEffect(() => {
    if (!showLoyaltyDialog) return;
    const digits = loyaltyPhone.replace(/\D/g, "");
    if (digits.length < 6) {
      setLoyaltyFound(null);
      return;
    }
    const handle = window.setTimeout(() => {
      LoyaltyApi.findByPhone(loyaltyPhone)
        .then((r) => {
          setLoyaltyFound(r.data);
          setLoyaltyName((n) => n || r.data.name);
        })
        .catch(() => setLoyaltyFound(null));
    }, 350);
    return () => window.clearTimeout(handle);
  }, [loyaltyPhone, showLoyaltyDialog]);

  async function handleSetLoyalty(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedOrder) return;
    setError(null);
    try {
      await RestaurantApi.setOrderLoyalty(selectedOrder.id, { phone: loyaltyPhone, name: loyaltyName || null, pointsToRedeem: loyaltyRedeem });
      setShowLoyaltyDialog(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleClearLoyalty() {
    if (!selectedOrder) return;
    setError(null);
    try {
      await RestaurantApi.clearOrderLoyalty(selectedOrder.id);
      setShowLoyaltyDialog(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openMoveDialog() {
    setMoveTargetTableId("");
    setShowMoveDialog(true);
  }

  async function handleMoveTable(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedOrder || !moveTargetTableId) return;
    setError(null);
    try {
      await RestaurantApi.moveOrderTable(selectedOrder.id, { targetTableId: moveTargetTableId });
      setShowMoveDialog(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const mergeCandidates = selectedOrder
    ? orders.filter(
        (o) =>
          o.id !== selectedOrder.id &&
          o.status === RestaurantOrderStatus.Open &&
          o.orderType === RestaurantOrderType.DineIn &&
          o.warehouseId === selectedOrder.warehouseId
      )
    : [];

  function openMergeDialog() {
    setMergeSourceOrderId("");
    setShowMergeDialog(true);
  }

  async function handleMergeOrders(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedOrder || !mergeSourceOrderId) return;
    setError(null);
    try {
      await RestaurantApi.mergeOrders(selectedOrder.id, { sourceOrderId: mergeSourceOrderId });
      setShowMergeDialog(false);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function openBillDialog() {
    setBillPaymentTerm(PaymentTerm.Cash);
    setBillFiscalPeriodId(periods.find((p) => !p.isClosed)?.id ?? "");
    setBillDeliveryPlatformName("");
    setShowBillDialog(true);
  }

  async function handleBillOrder(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedOrder) return;
    setError(null);
    try {
      const res = await RestaurantApi.billOrder(selectedOrder.id, {
        paymentTerm: billPaymentTerm,
        fiscalPeriodId: billFiscalPeriodId,
        cashierShiftId: activeShift?.id ?? null,
        deliveryPlatformName: billPaymentTerm === PaymentTerm.Credit ? billDeliveryPlatformName : null,
      });
      setShowBillDialog(false);
      if (res.data.salesInvoiceId) {
        const pdfRes = await SalesApi.downloadInvoicePdf(res.data.salesInvoiceId);
        const url = window.URL.createObjectURL(pdfRes.data as Blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `${res.data.salesInvoiceNumber ?? "invoice"}.pdf`;
        link.click();
        window.URL.revokeObjectURL(url);
      }
      setSelectedOrderId(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  /// Voids the billed order (reverses revenue/VAT/settlement/AR and inventory/COGS on the backend), then
  /// immediately reopens a fresh editable order pre-filled with the same lines — covers a plain void
  /// (cashier just doesn't touch the new order) and an exchange (cashier edits it before re-billing) with
  /// one action, matching how the feature was originally asked for.
  async function handleVoidOrder() {
    if (!selectedOrder) return;
    const reason = window.prompt(t.restaurant.voidOrderPrompt);
    if (!reason || !reason.trim()) return;
    setError(null);
    try {
      await RestaurantApi.voidOrder(selectedOrder.id, { reason: reason.trim() });

      const replacement = await RestaurantApi.createOrder({
        orderType: selectedOrder.orderType,
        tableId: selectedOrder.orderType === RestaurantOrderType.DineIn ? selectedOrder.tableId : null,
        customerName: selectedOrder.orderType !== RestaurantOrderType.DineIn ? selectedOrder.customerName : null,
        customerPhone: selectedOrder.orderType !== RestaurantOrderType.DineIn ? selectedOrder.customerPhone : null,
        deliveryAddress: selectedOrder.orderType === RestaurantOrderType.Delivery ? selectedOrder.deliveryAddress : null,
        warehouseId: selectedOrder.warehouseId,
      });
      for (const line of selectedOrder.lines) {
        await RestaurantApi.addLine(replacement.data.id, { itemId: line.itemId, quantity: line.quantity });
      }

      setSelectedOrderId(replacement.data.id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const orderTypeLabel: Record<RestaurantOrderType, string> = {
    [RestaurantOrderType.DineIn]: t.restaurant.dineIn,
    [RestaurantOrderType.Takeaway]: t.restaurant.takeaway,
    [RestaurantOrderType.Delivery]: t.restaurant.delivery,
  };

  const orderTypeIcon: Record<RestaurantOrderType, React.ReactNode> = {
    [RestaurantOrderType.DineIn]: <IconGrid />,
    [RestaurantOrderType.Takeaway]: <IconBox />,
    [RestaurantOrderType.Delivery]: <IconTruck />,
  };

  const menuByCategory = useMemo(() => {
    const groups = new Map<string, MenuItem[]>();
    for (const item of menu) {
      const list = groups.get(item.categoryName) ?? [];
      list.push(item);
      groups.set(item.categoryName, list);
    }
    return Array.from(groups.entries());
  }, [menu]);

  useEffect(() => {
    if (menuByCategory.length > 0 && !menuByCategory.some(([cat]) => cat === activeCategory)) {
      setActiveCategory(menuByCategory[0][0]);
    }
  }, [menuByCategory, activeCategory]);

  const activeItems = menuByCategory.find(([cat]) => cat === activeCategory)?.[1] ?? [];

  const exitButton = (
    <button
      type="button"
      className="btn btn-secondary"
      onClick={() => {
        logout();
        navigate("/pos-login");
      }}
    >
      {t.restaurant.exitPos}
    </button>
  );

  if (!online) {
    return (
      <OfflinePos
        snapshot={snapshot}
        queue={queue}
        syncing={syncing}
        onSyncNow={runSync}
        onQueueChanged={refreshQueue}
        exitButton={exitButton}
      />
    );
  }

  if (shiftLoading) {
    return (
      <div className="pos-page">
        <div className="page-header"><h1>{t.restaurant.posTitle}</h1>{exitButton}</div>
        <div className="text-muted">{t.common.loading}</div>
      </div>
    );
  }

  if (employeeError) {
    return (
      <div className="pos-page">
        <div className="page-header"><h1>{t.restaurant.posTitle}</h1>{exitButton}</div>
        <div className="alert-error">{employeeError}</div>
      </div>
    );
  }

  if (!activeShift) {
    return (
      <div className="pos-page">
        <div className="page-header"><h1>{t.restaurant.openShiftTitle}</h1>{exitButton}</div>
        {error && <div className="alert-error">{error}</div>}
        {closedShiftSummary && (
          <div className="card">
            <h3 style={{ marginTop: 0 }}>{t.restaurant.closeShiftTitle}</h3>
            <table style={{ maxWidth: 420 }}>
              <tbody>
                <tr><td>{t.restaurant.expectedCashLabel}</td><td style={{ textAlign: "end" }}>{closedShiftSummary.expectedCash?.toLocaleString()}</td></tr>
                <tr><td>{t.restaurant.closingCountedCash}</td><td style={{ textAlign: "end" }}>{closedShiftSummary.closingCountedCash?.toLocaleString()}</td></tr>
                <tr>
                  <td><strong>{t.restaurant.cashVarianceLabel}</strong></td>
                  <td style={{ textAlign: "end" }} className={(closedShiftSummary.cashVariance ?? 0) >= 0 ? "text-success" : "text-danger"}>
                    <strong>{closedShiftSummary.cashVariance?.toLocaleString()}</strong>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        )}
        <div className="card" style={{ maxWidth: 420 }}>
          <p className="text-muted" style={{ marginTop: 0 }}>{t.restaurant.openShiftIntro}</p>
          <form onSubmit={handleOpenShift}>
            <div className="form-field">
              <label>{t.restaurant.openingFloat}</label>
              <input type="number" min={0} step="0.01" value={openingFloat} onChange={(e) => setOpeningFloat(Number(e.target.value))} required />
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>{t.restaurant.openShiftButton}</button>
          </form>
        </div>
      </div>
    );
  }

  return (
    <div className="pos-page">
      <div className="page-header">
        <h1>{t.restaurant.posTitle}</h1>
        <div style={{ display: "flex", gap: 10 }}>
          {exitButton}
          <button className="btn btn-secondary" onClick={() => { setClosingCountedCash(0); setShowCloseShift(true); }}>
            {t.restaurant.closeShift}
          </button>
          <button className="btn" onClick={() => setShowNewOrder(true)}>
            {t.restaurant.newOrder}
          </button>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}
      {syncMessage && <div className="alert-success">{syncMessage}</div>}
      <OfflineQueueNotice queue={queue} syncing={syncing} onSyncNow={runSync} onChanged={refreshQueue} />

      <div className="pos-layout">
        <div className="pos-orders-rail">
          <div className="pos-orders-rail-title">{t.restaurant.openOrders}</div>
          {orders.length === 0 && <div className="text-muted" style={{ padding: "0 4px" }}>{t.common.noData}</div>}
          {orders.map((o) => (
            <button
              key={o.id}
              type="button"
              className={"pos-order-chip" + (selectedOrderId === o.id ? " active" : "")}
              onClick={() => setSelectedOrderId(o.id)}
            >
              <span className="pos-order-chip-icon">{orderTypeIcon[o.orderType]}</span>
              <span className="pos-order-chip-body">
                <span className="pos-order-chip-title">
                  {o.orderType === RestaurantOrderType.DineIn
                    ? `${t.restaurant.table} ${o.tableNumber}`
                    : `${orderTypeLabel[o.orderType]}${o.customerName ? " - " + o.customerName : ""}`}
                </span>
                <span className="pos-order-chip-sub">{o.orderNumber} · {o.totalAmount.toLocaleString()}</span>
              </span>
            </button>
          ))}
        </div>

        <div className="pos-menu-area">
          {!selectedOrder && <div className="text-muted" style={{ padding: 24 }}>{t.restaurant.selectOrderHint}</div>}
          {selectedOrder && selectedOrder.status === RestaurantOrderStatus.Open && (
            <>
              <div className="pos-category-tabs">
                {menuByCategory.map(([category]) => (
                  <button
                    key={category}
                    type="button"
                    className={"pos-category-tab" + (activeCategory === category ? " active" : "")}
                    onClick={() => setActiveCategory(category)}
                  >
                    {category}
                  </button>
                ))}
              </div>
              <div className="pos-menu-grid">
                {activeItems.map((item) => (
                  <button key={item.id} type="button" className="pos-menu-item" onClick={() => handleAddItem(item.id)}>
                    <span className="pos-menu-item-name">{bilingualName(item.nameAr, item.nameEn, lang)}</span>
                    <span className="pos-menu-item-price">{item.menuPrice.toLocaleString()}</span>
                  </button>
                ))}
                {menu.length === 0 && <div className="text-muted">{t.restaurant.noMenuItemsHint}</div>}
              </div>
            </>
          )}
          {selectedOrder && selectedOrder.status === RestaurantOrderStatus.Billed && (
            <div style={{ padding: 24, textAlign: "center" }}>
              <div className="text-muted" style={{ marginBottom: 14 }}>{selectedOrder.orderNumber}</div>
              <button className="btn btn-secondary" onClick={handleVoidOrder}>
                ↩️ {t.restaurant.voidOrder}
              </button>
              <p className="text-muted" style={{ marginTop: 10, maxWidth: 320, marginInline: "auto" }}>{t.restaurant.voidOrderHint}</p>
            </div>
          )}
          {selectedOrder && (selectedOrder.status === RestaurantOrderStatus.Cancelled || selectedOrder.status === RestaurantOrderStatus.Voided) && (
            <div className="text-muted" style={{ padding: 24 }}>{selectedOrder.orderNumber}</div>
          )}
        </div>

        <div className="pos-cart-panel">
          {!selectedOrder && <div className="text-muted" style={{ padding: 16 }}>{t.restaurant.selectOrderHint}</div>}
          {selectedOrder && (
            <>
              <div className="pos-cart-header">
                <div>
                  <div style={{ fontWeight: 700 }}>
                    {selectedOrder.orderType === RestaurantOrderType.DineIn
                      ? `${t.restaurant.table} ${selectedOrder.tableNumber}`
                      : `${orderTypeLabel[selectedOrder.orderType]}${selectedOrder.customerName ? " - " + selectedOrder.customerName : ""}`}
                  </div>
                  <div className="text-muted">{selectedOrder.orderNumber}</div>
                </div>
                <div style={{ display: "flex", gap: 8 }}>
                  {selectedOrder.status === RestaurantOrderStatus.Open && selectedOrder.lines.length >= 2 && (
                    <button className="btn btn-secondary btn-sm" onClick={openSplitDialog} title={t.restaurant.splitBill}>
                      ⑂
                    </button>
                  )}
                  {selectedOrder.status === RestaurantOrderStatus.Open && selectedOrder.orderType === RestaurantOrderType.DineIn && (
                    <>
                      <button className="btn btn-secondary btn-sm" onClick={openMoveDialog} title={t.restaurant.moveTable}>
                        ⇄
                      </button>
                      <button className="btn btn-secondary btn-sm" onClick={openMergeDialog} title={t.restaurant.mergeOrders}>
                        ⊕
                      </button>
                    </>
                  )}
                  {selectedOrder.status === RestaurantOrderStatus.Open && (
                    <button className="btn btn-secondary btn-sm" onClick={handleCancelOrder} title={t.restaurant.cancelOrder}>
                      ✕
                    </button>
                  )}
                </div>
              </div>

              <div className="pos-cart-lines">
                {selectedOrder.lines.length === 0 && <div className="text-muted" style={{ padding: "12px 0" }}>{t.restaurant.emptyCart}</div>}
                {selectedOrder.lines.map((line) => (
                  <div key={line.id} className="pos-cart-line">
                    <div className="pos-cart-line-info">
                      <div className="pos-cart-line-name">{line.itemName}</div>
                      <div className="text-muted">
                        {line.unitPrice.toLocaleString()}
                        {line.discountAmount > 0 && ` — ${t.restaurant.discount} ${line.discountAmount.toLocaleString()}`}
                      </div>
                    </div>
                    <div className="pos-cart-line-qty">
                      <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleLineQuantity(line.id, line.quantity - 1)}>-</button>
                      <span>{line.quantity}</span>
                      <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleLineQuantity(line.id, line.quantity + 1)}>+</button>
                    </div>
                    <div className="pos-cart-line-total">{line.lineTotal.toLocaleString()}</div>
                    {selectedOrder.status === RestaurantOrderStatus.Open && (
                      <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleLineDiscount(line.id)} title={t.restaurant.discount}>%</button>
                    )}
                    <button type="button" className="pos-cart-line-remove" onClick={() => handleRemoveLine(line.id)} title={t.common.delete}>✕</button>
                  </div>
                ))}
              </div>

              {loyaltyEnabled && selectedOrder.status === RestaurantOrderStatus.Open && (
                <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", padding: "6px 0" }}>
                  <button type="button" className="btn btn-secondary btn-sm" onClick={openLoyaltyDialog}>
                    {selectedOrder.loyaltyMemberId ? `⭐ ${selectedOrder.loyaltyMemberName}` : t.restaurant.loyaltyAttach}
                  </button>
                  {selectedOrder.loyaltyMemberId && (
                    <span className="text-muted">
                      {t.restaurant.loyaltyBalance}: {selectedOrder.loyaltyPointsBalance} · {t.restaurant.loyaltyWillEarn}: +{selectedOrder.loyaltyPointsEarned}
                    </span>
                  )}
                </div>
              )}

              <div className="pos-cart-totals">
                <div><span>{t.common.subtotal}</span><span>{selectedOrder.grossSubTotal.toLocaleString()}</span></div>
                <div>
                  <span>
                    {t.restaurant.discount}
                    {selectedOrder.status === RestaurantOrderStatus.Open && (
                      <button type="button" className="btn btn-secondary btn-sm" style={{ marginInlineStart: 8 }} onClick={handleOrderDiscount}>
                        {t.common.edit}
                      </button>
                    )}
                  </span>
                  <span>{selectedOrder.totalDiscount.toLocaleString()}</span>
                </div>
                {selectedOrder.loyaltyDiscountAmount > 0 && (
                  <div>
                    <span>{t.restaurant.loyaltyDiscountRow} ({selectedOrder.loyaltyPointsToRedeem})</span>
                    <span>-{selectedOrder.loyaltyDiscountAmount.toLocaleString()}</span>
                  </div>
                )}
                <div><span>{t.common.vat} ({(selectedOrder.vatRate * 100).toFixed(0)}%)</span><span>{selectedOrder.vatAmount.toLocaleString()}</span></div>
                <div className="pos-cart-total-grand"><span>{t.common.total}</span><span>{selectedOrder.totalAmount.toLocaleString()}</span></div>
              </div>

              {selectedOrder.status === RestaurantOrderStatus.Open && (
                <button className="btn pos-bill-btn" onClick={openBillDialog} disabled={selectedOrder.lines.length === 0}>
                  {t.restaurant.billOrder}
                </button>
              )}
            </>
          )}
        </div>
      </div>

      {showNewOrder && (
        <div className="modal-overlay" onClick={() => setShowNewOrder(false)}>
          <div className="card" style={{ maxWidth: 460, margin: "8% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.restaurant.newOrder}</h3>
            <form onSubmit={handleCreateOrder}>
              <div className="form-grid">
                <div className="form-field">
                  <label>{t.restaurant.orderType}</label>
                  <select value={orderType} onChange={(e) => setOrderType(Number(e.target.value) as RestaurantOrderType)}>
                    <option value={RestaurantOrderType.DineIn}>{t.restaurant.dineIn}</option>
                    <option value={RestaurantOrderType.Takeaway}>{t.restaurant.takeaway}</option>
                    <option value={RestaurantOrderType.Delivery}>{t.restaurant.delivery}</option>
                  </select>
                </div>
                <div className="form-field">
                  <label>{t.sales.warehouse}</label>
                  <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                    <option value="" disabled>-</option>
                    {warehouses.map((w) => (
                      <option key={w.id} value={w.id}>{w.code} - {bilingualName(w.nameAr, w.nameEn, lang)}</option>
                    ))}
                  </select>
                </div>
                {orderType === RestaurantOrderType.DineIn && (
                  <div className="form-field">
                    <label>{t.restaurant.table}</label>
                    <select value={tableId} onChange={(e) => setTableId(e.target.value)} required>
                      <option value="" disabled>-</option>
                      {availableTables.map((tb) => (
                        <option key={tb.id} value={tb.id}>{tb.number}{tb.sectionName ? ` - ${tb.sectionName}` : ""}</option>
                      ))}
                    </select>
                  </div>
                )}
                {orderType !== RestaurantOrderType.DineIn && (
                  <>
                    <div className="form-field">
                      <label>{t.restaurant.customerName}</label>
                      <input value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
                    </div>
                    <div className="form-field">
                      <label>{t.restaurant.customerPhone}</label>
                      <input value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} />
                    </div>
                  </>
                )}
                {orderType === RestaurantOrderType.Delivery && (
                  <div className="form-field">
                    <label>{t.restaurant.deliveryAddress}</label>
                    <input value={deliveryAddress} onChange={(e) => setDeliveryAddress(e.target.value)} />
                  </div>
                )}
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit">{t.common.save}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setShowNewOrder(false)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showBillDialog && selectedOrder && (
        <div className="modal-overlay" onClick={() => setShowBillDialog(false)}>
          <div className="card" style={{ maxWidth: 420, margin: "10% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.restaurant.billOrder} — {selectedOrder.orderNumber}</h3>
            <form onSubmit={handleBillOrder}>
              <div className="form-field">
                <label>{t.sales.paymentTerm}</label>
                <select value={billPaymentTerm} onChange={(e) => setBillPaymentTerm(Number(e.target.value) as PaymentTerm)}>
                  <option value={PaymentTerm.Cash}>💵 {t.paymentTerm.cash}</option>
                  <option value={PaymentTerm.Card}>💳 {t.paymentTerm.card}</option>
                  {selectedOrder.orderType === RestaurantOrderType.Delivery && (
                    <option value={PaymentTerm.Credit}>🛵 {t.restaurant.deliveryPlatformCredit}</option>
                  )}
                </select>
              </div>
              {billPaymentTerm === PaymentTerm.Credit && (
                <div className="form-field">
                  <label>{t.restaurant.deliveryPlatformName}</label>
                  <input
                    value={billDeliveryPlatformName}
                    onChange={(e) => setBillDeliveryPlatformName(e.target.value)}
                    placeholder={t.restaurant.deliveryPlatformNamePlaceholder}
                    required
                  />
                </div>
              )}
              <div className="form-field">
                <label>{t.common.fiscalPeriod}</label>
                <select value={billFiscalPeriodId} onChange={(e) => setBillFiscalPeriodId(e.target.value)} required>
                  <option value="" disabled>-</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id}>{p.name}</option>
                  ))}
                </select>
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit">{t.restaurant.confirmBill}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setShowBillDialog(false)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showSplitDialog && selectedOrder && (
        <div className="modal-overlay" onClick={() => setShowSplitDialog(false)}>
          <div className="card" style={{ maxWidth: 460, margin: "8% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.restaurant.splitBill} — {selectedOrder.orderNumber}</h3>
            <p className="text-muted" style={{ marginTop: 0 }}>{t.restaurant.splitBillHint}</p>
            <form onSubmit={handleSplitOrder}>
              <div style={{ display: "flex", flexDirection: "column", gap: 8, maxHeight: 320, overflowY: "auto" }}>
                {selectedOrder.lines.map((line) => (
                  <label key={line.id} style={{ display: "flex", alignItems: "center", gap: 10, padding: "6px 0", borderBottom: "1px solid var(--border-color, #eee)" }}>
                    <input type="checkbox" checked={splitLineIds.has(line.id)} onChange={() => toggleSplitLine(line.id)} />
                    <span style={{ flex: 1 }}>{line.quantity} × {line.itemName}</span>
                    <span>{line.lineTotal.toLocaleString()}</span>
                  </label>
                ))}
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit" disabled={splitLineIds.size === 0 || splitLineIds.size === selectedOrder.lines.length}>
                  {t.restaurant.splitBillConfirm}
                </button>
                <button className="btn btn-secondary" type="button" onClick={() => setShowSplitDialog(false)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showLoyaltyDialog && selectedOrder && (
        <div className="modal-overlay" onClick={() => setShowLoyaltyDialog(false)}>
          <div className="card" style={{ maxWidth: 420, margin: "8% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.restaurant.loyaltyAttachTitle} — {selectedOrder.orderNumber}</h3>
            <form onSubmit={handleSetLoyalty}>
              <div className="form-field">
                <label>{t.restaurant.loyaltyPhone}</label>
                <input value={loyaltyPhone} onChange={(e) => setLoyaltyPhone(e.target.value)} inputMode="tel" dir="ltr" autoFocus required />
              </div>
              {loyaltyPhone.replace(/\D/g, "").length >= 6 && (
                <p className={loyaltyFound ? "text-success" : "text-muted"} style={{ margin: "4px 0" }}>
                  {loyaltyFound
                    ? `${t.restaurant.loyaltyFound}: ${loyaltyFound.name} — ${t.restaurant.loyaltyBalance}: ${loyaltyFound.pointsBalance}`
                    : t.restaurant.loyaltyNewCustomer}
                </p>
              )}
              <div className="form-field">
                <label>{t.restaurant.loyaltyMember}</label>
                <input value={loyaltyName} onChange={(e) => setLoyaltyName(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.restaurant.loyaltyRedeemPoints}</label>
                <input
                  type="number" min={0} step="1" value={loyaltyRedeem}
                  max={loyaltyFound?.pointsBalance ?? 0}
                  onChange={(e) => setLoyaltyRedeem(Number(e.target.value) || 0)}
                  disabled={!loyaltyFound || loyaltyFound.pointsBalance === 0}
                />
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 14, flexWrap: "wrap" }}>
                <button className="btn" type="submit">{t.common.save}</button>
                {selectedOrder.loyaltyMemberId && (
                  <button className="btn btn-secondary" type="button" onClick={handleClearLoyalty}>{t.restaurant.loyaltyRemove}</button>
                )}
                <button className="btn btn-secondary" type="button" onClick={() => setShowLoyaltyDialog(false)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showMoveDialog && selectedOrder && (
        <div className="modal-overlay" onClick={() => setShowMoveDialog(false)}>
          <div className="card" style={{ maxWidth: 420, margin: "10% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.restaurant.moveTable} — {selectedOrder.orderNumber}</h3>
            <p className="text-muted" style={{ marginTop: 0 }}>{t.restaurant.moveTableHint}</p>
            <form onSubmit={handleMoveTable}>
              {availableTables.length === 0 ? (
                <div className="text-muted">{t.restaurant.moveTableNoFree}</div>
              ) : (
                <select value={moveTargetTableId} onChange={(e) => setMoveTargetTableId(e.target.value)} required>
                  <option value="">—</option>
                  {availableTables.map((tb) => (
                    <option key={tb.id} value={tb.id}>{t.restaurant.table} {tb.number}</option>
                  ))}
                </select>
              )}
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit" disabled={!moveTargetTableId}>{t.restaurant.moveTableConfirm}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setShowMoveDialog(false)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showMergeDialog && selectedOrder && (
        <div className="modal-overlay" onClick={() => setShowMergeDialog(false)}>
          <div className="card" style={{ maxWidth: 420, margin: "10% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.restaurant.mergeOrders} — {selectedOrder.orderNumber}</h3>
            <p className="text-muted" style={{ marginTop: 0 }}>{t.restaurant.mergeOrdersHint}</p>
            <form onSubmit={handleMergeOrders}>
              {mergeCandidates.length === 0 ? (
                <div className="text-muted">{t.restaurant.mergeOrdersNone}</div>
              ) : (
                <select value={mergeSourceOrderId} onChange={(e) => setMergeSourceOrderId(e.target.value)} required>
                  <option value="">—</option>
                  {mergeCandidates.map((o) => (
                    <option key={o.id} value={o.id}>
                      {t.restaurant.table} {o.tableNumber} — {o.orderNumber} ({o.totalAmount.toLocaleString()})
                    </option>
                  ))}
                </select>
              )}
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit" disabled={!mergeSourceOrderId}>{t.restaurant.mergeOrdersConfirm}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setShowMergeDialog(false)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showCloseShift && (
        <div className="modal-overlay" onClick={() => setShowCloseShift(false)}>
          <div className="card" style={{ maxWidth: 420, margin: "10% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.restaurant.closeShiftTitle}</h3>
            <p className="text-muted">{t.restaurant.closeShiftIntro}</p>
            <form onSubmit={handleCloseShift}>
              <div className="form-field">
                <label>{t.restaurant.closingCountedCash}</label>
                <input type="number" min={0} step="0.01" value={closingCountedCash} onChange={(e) => setClosingCountedCash(Number(e.target.value))} required />
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit">{t.restaurant.closeShift}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setShowCloseShift(false)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
