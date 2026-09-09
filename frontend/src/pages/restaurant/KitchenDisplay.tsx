import { useEffect, useState } from "react";
import { RestaurantApi } from "../../api/services";
import { KitchenLineStatus, RestaurantOrderType, type RestaurantOrder } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const POLL_INTERVAL_MS = 5000;

const nextStatus: Record<KitchenLineStatus, KitchenLineStatus | null> = {
  [KitchenLineStatus.Pending]: KitchenLineStatus.Preparing,
  [KitchenLineStatus.Preparing]: KitchenLineStatus.Ready,
  [KitchenLineStatus.Ready]: KitchenLineStatus.Served,
  [KitchenLineStatus.Served]: null,
};

const statusClass: Record<KitchenLineStatus, string> = {
  [KitchenLineStatus.Pending]: "badge-draft",
  [KitchenLineStatus.Preparing]: "badge-posted",
  [KitchenLineStatus.Ready]: "badge-posted",
  [KitchenLineStatus.Served]: "badge-reversed",
};

export default function KitchenDisplay() {
  const { t } = useLanguage();
  const [orders, setOrders] = useState<RestaurantOrder[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    try {
      const res = await RestaurantApi.getOrders(false);
      setOrders(res.data.filter((o) => o.lines.some((l) => l.kitchenStatus !== KitchenLineStatus.Served)));
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    load();
    const interval = setInterval(load, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, []);

  async function handleAdvance(orderId: string, lineId: string, status: KitchenLineStatus) {
    try {
      await RestaurantApi.setLineKitchenStatus(orderId, lineId, { status });
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const statusLabel: Record<KitchenLineStatus, string> = {
    [KitchenLineStatus.Pending]: t.restaurant.kitchenStatusPending,
    [KitchenLineStatus.Preparing]: t.restaurant.kitchenStatusPreparing,
    [KitchenLineStatus.Ready]: t.restaurant.kitchenStatusReady,
    [KitchenLineStatus.Served]: t.restaurant.kitchenStatusServed,
  };

  const orderTypeLabel: Record<RestaurantOrderType, string> = {
    [RestaurantOrderType.DineIn]: t.restaurant.dineIn,
    [RestaurantOrderType.Takeaway]: t.restaurant.takeaway,
    [RestaurantOrderType.Delivery]: t.restaurant.delivery,
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.restaurant.kitchenDisplayTitle}</h1>
      </div>
      <p className="text-muted">{t.restaurant.kitchenDisplayIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 16 }}>
        {orders.length === 0 && <div className="text-muted">{t.restaurant.kitchenNoOrders}</div>}
        {orders.map((order) => (
          <div key={order.id} className="card" style={{ margin: 0 }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", marginBottom: 8 }}>
              <strong>
                {order.orderType === RestaurantOrderType.DineIn
                  ? `${t.restaurant.table} ${order.tableNumber}`
                  : `${orderTypeLabel[order.orderType]}${order.customerName ? " - " + order.customerName : ""}`}
              </strong>
              <span className="text-muted">{order.orderNumber}</span>
            </div>
            {order.lines
              .filter((l) => l.kitchenStatus !== KitchenLineStatus.Served)
              .map((line) => {
                const advanceTo = nextStatus[line.kitchenStatus];
                return (
                  <div
                    key={line.id}
                    style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "8px 0", borderTop: "1px solid var(--border-color, #eee)" }}
                  >
                    <div>
                      <div>{line.quantity} × {line.itemName}</div>
                      {line.notes && <div className="text-muted" style={{ fontSize: 12 }}>{line.notes}</div>}
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <span className={`badge ${statusClass[line.kitchenStatus]}`}>{statusLabel[line.kitchenStatus]}</span>
                      {advanceTo !== null && (
                        <button className="btn btn-sm" onClick={() => handleAdvance(order.id, line.id, advanceTo)}>
                          {t.restaurant.kitchenAdvanceTo} {statusLabel[advanceTo]}
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
          </div>
        ))}
      </div>
    </div>
  );
}
