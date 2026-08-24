import { useEffect, useMemo, useState } from "react";
import { LookupsApi, RestaurantApi, SalesApi, WarehousesApi } from "../../api/services";
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
} from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function RestaurantPOS() {
  const { t } = useLanguage();
  const [orders, setOrders] = useState<RestaurantOrder[]>([]);
  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [menu, setMenu] = useState<MenuItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);

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

  const selectedOrder = useMemo(() => orders.find((o) => o.id === selectedOrderId) ?? null, [orders, selectedOrderId]);
  const availableTables = tables.filter((tb) => tb.status === RestaurantTableStatus.Available);

  async function load() {
    const [ordersRes, tablesRes, menuRes, warehousesRes, periodsRes] = await Promise.all([
      RestaurantApi.getOrders(false),
      RestaurantApi.getTables(),
      RestaurantApi.getMenu(),
      WarehousesApi.getAll(),
      LookupsApi.fiscalPeriods(),
    ]);
    setOrders(ordersRes.data);
    setTables(tablesRes.data);
    setMenu(menuRes.data);
    setWarehouses(warehousesRes.data);
    setPeriods(periodsRes.data);
  }

  useEffect(() => {
    load();
  }, []);

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

  function openBillDialog() {
    setBillPaymentTerm(PaymentTerm.Cash);
    setBillFiscalPeriodId(periods.find((p) => !p.isClosed)?.id ?? "");
    setShowBillDialog(true);
  }

  async function handleBillOrder(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedOrder) return;
    setError(null);
    try {
      const res = await RestaurantApi.billOrder(selectedOrder.id, { paymentTerm: billPaymentTerm, fiscalPeriodId: billFiscalPeriodId });
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

  const orderTypeLabel: Record<RestaurantOrderType, string> = {
    [RestaurantOrderType.DineIn]: t.restaurant.dineIn,
    [RestaurantOrderType.Takeaway]: t.restaurant.takeaway,
    [RestaurantOrderType.Delivery]: t.restaurant.delivery,
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

  return (
    <div>
      <div className="page-header">
        <h1>{t.restaurant.posTitle}</h1>
        <button className="btn" onClick={() => setShowNewOrder((v) => !v)}>
          {showNewOrder ? t.common.cancel : t.restaurant.newOrder}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showNewOrder && (
        <div className="card">
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
                    <option key={w.id} value={w.id}>{w.code} - {w.nameAr}</option>
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
            <button className="btn" type="submit" style={{ marginTop: 14 }}>{t.common.save}</button>
          </form>
        </div>
      )}

      <div style={{ display: "flex", gap: 16, alignItems: "flex-start" }}>
        <div className="card" style={{ width: 280, flexShrink: 0 }}>
          <h3>{t.restaurant.openOrders}</h3>
          {orders.length === 0 && <div className="text-muted">{t.common.noData}</div>}
          {orders.map((o) => (
            <div
              key={o.id}
              onClick={() => setSelectedOrderId(o.id)}
              style={{
                padding: "8px 10px",
                borderRadius: 8,
                cursor: "pointer",
                marginBottom: 6,
                background: selectedOrderId === o.id ? "#e6f2f6" : "#f5f6f7",
              }}
            >
              <div style={{ fontWeight: 600 }}>
                {o.orderType === RestaurantOrderType.DineIn ? `🍽️ ${t.restaurant.table} ${o.tableNumber}` : `${orderTypeLabel[o.orderType]}${o.customerName ? " - " + o.customerName : ""}`}
              </div>
              <div className="text-muted">{o.orderNumber} · {o.totalAmount.toLocaleString()}</div>
            </div>
          ))}
        </div>

        <div className="card" style={{ flex: 1 }}>
          {!selectedOrder && <div className="text-muted">{t.restaurant.selectOrderHint}</div>}

          {selectedOrder && (
            <>
              <div className="page-header">
                <h3>
                  {selectedOrder.orderType === RestaurantOrderType.DineIn
                    ? `🍽️ ${t.restaurant.table} ${selectedOrder.tableNumber}`
                    : `${orderTypeLabel[selectedOrder.orderType]}${selectedOrder.customerName ? " - " + selectedOrder.customerName : ""}`}
                  {" "}({selectedOrder.orderNumber})
                </h3>
                {selectedOrder.status === RestaurantOrderStatus.Open && (
                  <div style={{ display: "flex", gap: 8 }}>
                    <button className="btn btn-secondary btn-sm" onClick={handleCancelOrder}>{t.restaurant.cancelOrder}</button>
                    <button className="btn btn-sm" onClick={openBillDialog} disabled={selectedOrder.lines.length === 0}>
                      {t.restaurant.billOrder}
                    </button>
                  </div>
                )}
              </div>

              <table>
                <thead>
                  <tr>
                    <th>{t.common.description}</th>
                    <th>{t.common.quantity}</th>
                    <th>{t.common.unitPrice}</th>
                    <th>{t.common.total}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {selectedOrder.lines.length === 0 && (
                    <tr><td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 14 }}>{t.restaurant.emptyCart}</td></tr>
                  )}
                  {selectedOrder.lines.map((line) => (
                    <tr key={line.id}>
                      <td>{line.itemName}</td>
                      <td>
                        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                          <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleLineQuantity(line.id, line.quantity - 1)}>-</button>
                          {line.quantity}
                          <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleLineQuantity(line.id, line.quantity + 1)}>+</button>
                        </div>
                      </td>
                      <td>{line.unitPrice.toLocaleString()}</td>
                      <td>{line.lineTotal.toLocaleString()}</td>
                      <td>
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleRemoveLine(line.id)}>{t.common.delete}</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 10 }}>
                <table style={{ width: 260 }}>
                  <tbody>
                    <tr><td>{t.common.subtotal}</td><td style={{ textAlign: "end" }}>{selectedOrder.subTotal.toLocaleString()}</td></tr>
                    <tr><td>{t.common.vat} ({(selectedOrder.vatRate * 100).toFixed(0)}%)</td><td style={{ textAlign: "end" }}>{selectedOrder.vatAmount.toLocaleString()}</td></tr>
                    <tr><td><strong>{t.common.total}</strong></td><td style={{ textAlign: "end" }}><strong>{selectedOrder.totalAmount.toLocaleString()}</strong></td></tr>
                  </tbody>
                </table>
              </div>

              {selectedOrder.status === RestaurantOrderStatus.Open && (
                <div style={{ marginTop: 20 }}>
                  <h4>{t.restaurant.menu}</h4>
                  {menuByCategory.map(([category, categoryItems]) => (
                    <div key={category} style={{ marginBottom: 12 }}>
                      <div className="text-muted" style={{ marginBottom: 6 }}>{category}</div>
                      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))", gap: 8 }}>
                        {categoryItems.map((item) => (
                          <button
                            key={item.id}
                            type="button"
                            className="btn btn-secondary"
                            style={{ padding: "10px 8px", textAlign: "center" }}
                            onClick={() => handleAddItem(item.id)}
                          >
                            <div>{item.nameAr}</div>
                            <div className="text-muted">{item.menuPrice.toLocaleString()}</div>
                          </button>
                        ))}
                      </div>
                    </div>
                  ))}
                  {menu.length === 0 && <div className="text-muted">{t.restaurant.noMenuItemsHint}</div>}
                </div>
              )}
            </>
          )}
        </div>
      </div>

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
                </select>
              </div>
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
    </div>
  );
}
