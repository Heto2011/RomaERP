import { useEffect, useMemo, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { ReservationsApi, RestaurantApi, WarehousesApi } from "../../api/services";
import {
  ReservationStatus,
  RestaurantOrderType,
  RestaurantTableStatus,
  type Reservation,
  type RestaurantTable,
  type Warehouse,
} from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { IconCalendar, IconGrid, IconBox } from "../../components/icons";

function todayIso() {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

// The server sends the restaurant's local time without a zone ("2026-10-21T20:00:00"); show it as written.
const timeOf = (iso: string) => iso.slice(11, 16);

export default function RestaurantReservations() {
  const { t } = useLanguage();
  const portal = useLocation().pathname.startsWith("/restaurant-portal");
  const posPath = "/restaurant/pos";

  const [day, setDay] = useState(todayIso());
  const [items, setItems] = useState<Reservation[]>([]);
  const [tables, setTables] = useState<RestaurantTable[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [editing, setEditing] = useState<Reservation | "new" | null>(null);
  const [form, setForm] = useState({ customerName: "", customerPhone: "", partySize: 2, time: "20:00", tableId: "", notes: "" });

  const [seating, setSeating] = useState<Reservation | null>(null);
  const [seatType, setSeatType] = useState<RestaurantOrderType>(RestaurantOrderType.DineIn);
  const [seatTableId, setSeatTableId] = useState("");
  const [warehouseId, setWarehouseId] = useState("");
  const [openedOrderId, setOpenedOrderId] = useState<string | null>(null);

  async function load(forDay = day) {
    const res = await ReservationsApi.getAll(forDay);
    setItems(res.data);
  }

  useEffect(() => {
    load().catch((e) => setError(getErrorMessage(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [day]);

  useEffect(() => {
    RestaurantApi.getTables().then((r) => setTables(r.data)).catch(() => {});
    WarehousesApi.getAll().then((r) => {
      setWarehouses(r.data);
      if (r.data.length > 0) setWarehouseId((cur) => cur || r.data[0].id);
    }).catch(() => {});
  }, []);

  const counts = useMemo(() => ({
    booked: items.filter((r) => r.status === ReservationStatus.Booked).length,
    seated: items.filter((r) => r.status === ReservationStatus.Seated).length,
    guests: items.filter((r) => r.status === ReservationStatus.Booked).reduce((sum, r) => sum + r.partySize, 0),
  }), [items]);

  const statusLabel: Record<ReservationStatus, string> = {
    [ReservationStatus.Booked]: t.restaurant.resBooked,
    [ReservationStatus.Seated]: t.restaurant.resSeated,
    [ReservationStatus.Cancelled]: t.restaurant.resCancelled,
    [ReservationStatus.NoShow]: t.restaurant.resNoShow,
  };
  const statusClass: Record<ReservationStatus, string> = {
    [ReservationStatus.Booked]: "badge badge-draft",
    [ReservationStatus.Seated]: "badge badge-posted",
    [ReservationStatus.Cancelled]: "badge badge-reversed",
    [ReservationStatus.NoShow]: "badge badge-reversed",
  };

  function openForm(r: Reservation | "new") {
    setError(null);
    setEditing(r);
    if (r === "new") {
      setForm({ customerName: "", customerPhone: "", partySize: 2, time: "20:00", tableId: "", notes: "" });
    } else {
      setForm({ customerName: r.customerName, customerPhone: r.customerPhone, partySize: r.partySize, time: timeOf(r.reservationAt), tableId: r.tableId ?? "", notes: r.notes ?? "" });
    }
  }

  async function run(action: () => Promise<unknown>, done?: string) {
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      await action();
      if (done) setNotice(done);
      await load();
    } catch (e) {
      setError(getErrorMessage(e));
      throw e;
    } finally {
      setBusy(false);
    }
  }

  async function saveForm(e: React.FormEvent) {
    e.preventDefault();
    const payload = {
      customerName: form.customerName,
      customerPhone: form.customerPhone,
      partySize: form.partySize,
      reservationAt: `${day}T${form.time}:00`,
      tableId: form.tableId || null,
      notes: form.notes || null,
    };
    try {
      await run(() => (editing === "new" ? ReservationsApi.create(payload) : ReservationsApi.update((editing as Reservation).id, payload)), t.restaurant.reservationSaved);
      setEditing(null);
    } catch {
      /* the error is already shown */
    }
  }

  function openSeat(r: Reservation) {
    setError(null);
    setSeating(r);
    setSeatType(RestaurantOrderType.DineIn);
    setSeatTableId(r.tableId ?? "");
    setOpenedOrderId(null);
  }

  async function confirmSeat() {
    if (!seating) return;
    try {
      await run(async () => {
        const res = await ReservationsApi.seat(seating.id, {
          orderType: seatType,
          tableId: seatType === RestaurantOrderType.DineIn ? seatTableId || null : null,
          warehouseId,
        });
        setOpenedOrderId(res.data.order.id);
        setNotice(`${t.restaurant.orderOpened}: ${res.data.order.orderNumber}`);
      });
    } catch {
      /* the error is already shown */
    }
  }

  const freeTables = tables.filter((tb) => tb.status === RestaurantTableStatus.Available || tb.id === seating?.tableId);

  return (
    <div>
      <div className="page-header">
        <h1><IconCalendar /> {t.restaurant.reservationsTitle}</h1>
        <button className="btn" onClick={() => openForm("new")}>{t.restaurant.newReservation}</button>
      </div>

      {error && <div className="alert-error">{error}</div>}
      {notice && <div className="alert-success">{notice}</div>}

      <div className="card" style={{ display: "flex", flexWrap: "wrap", gap: 24, alignItems: "end" }}>
        <div className="form-field" style={{ margin: 0 }}>
          <label>{t.restaurant.reservationDate}</label>
          <input type="date" value={day} onChange={(e) => setDay(e.target.value)} />
        </div>
        <div><div className="text-muted">{t.restaurant.resBooked}</div><strong style={{ fontSize: 22 }}>{counts.booked}</strong></div>
        <div><div className="text-muted">{t.restaurant.partySize}</div><strong style={{ fontSize: 22 }}>{counts.guests}</strong></div>
        <div><div className="text-muted">{t.restaurant.resSeated}</div><strong style={{ fontSize: 22 }}>{counts.seated}</strong></div>
      </div>

      <div className="card">
        {items.length === 0 ? (
          <div className="text-muted">{t.restaurant.noReservations}</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>{t.restaurant.reservationTime}</th>
                <th>{t.restaurant.customerNameLabel}</th>
                <th>{t.restaurant.partySize}</th>
                <th>{t.restaurant.tableNumber}</th>
                <th></th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id}>
                  <td><strong>{timeOf(r.reservationAt)}</strong></td>
                  <td>
                    {r.customerName}
                    <div className="text-muted" dir="ltr" style={{ textAlign: "start" }}>{r.customerPhone}</div>
                    {r.notes && <div className="text-muted">{r.notes}</div>}
                  </td>
                  <td>{r.partySize}</td>
                  <td>{r.tableNumber ?? "—"}</td>
                  <td>
                    <span className={statusClass[r.status]}>{statusLabel[r.status]}</span>
                    {r.orderNumber && <div className="text-muted">{t.restaurant.reservationOrder}: {r.orderNumber}</div>}
                  </td>
                  <td style={{ whiteSpace: "nowrap" }}>
                    {r.status === ReservationStatus.Booked && (
                      <>
                        <button className="btn btn-sm" onClick={() => openSeat(r)} disabled={busy}>{t.restaurant.guestArrived}</button>{" "}
                        <button className="btn btn-secondary btn-sm" onClick={() => openForm(r)} disabled={busy}>{t.common.edit}</button>{" "}
                        <button className="btn btn-secondary btn-sm" disabled={busy}
                          onClick={() => window.confirm(t.restaurant.noShowConfirm) && run(() => ReservationsApi.noShow(r.id)).catch(() => {})}>
                          {t.restaurant.markNoShow}
                        </button>{" "}
                        <button className="btn btn-secondary btn-sm" disabled={busy}
                          onClick={() => window.confirm(t.restaurant.cancelReservationConfirm) && run(() => ReservationsApi.cancel(r.id)).catch(() => {})}>
                          {t.restaurant.cancelReservation}
                        </button>
                      </>
                    )}
                    {r.status === ReservationStatus.Seated && r.restaurantOrderId && !portal && (
                      <Link className="btn btn-secondary btn-sm" to={`${posPath}?order=${r.restaurantOrderId}`}>{t.restaurant.openInPos}</Link>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editing && (
        <div className="modal-overlay" onClick={() => setEditing(null)}>
          <div className="card" style={{ maxWidth: 520, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ marginTop: 0 }}>{editing === "new" ? t.restaurant.newReservation : t.restaurant.editReservation}</h3>
            <form onSubmit={saveForm}>
              <div className="form-grid">
                <div className="form-field">
                  <label>{t.restaurant.customerNameLabel}</label>
                  <input value={form.customerName} maxLength={200} onChange={(e) => setForm({ ...form, customerName: e.target.value })} required />
                </div>
                <div className="form-field">
                  <label>{t.restaurant.customerPhoneLabel}</label>
                  <input value={form.customerPhone} maxLength={30} dir="ltr" onChange={(e) => setForm({ ...form, customerPhone: e.target.value })} required />
                </div>
                <div className="form-field">
                  <label>{t.restaurant.reservationDate}</label>
                  <input type="date" value={day} onChange={(e) => setDay(e.target.value)} required />
                </div>
                <div className="form-field">
                  <label>{t.restaurant.reservationTime}</label>
                  <input type="time" value={form.time} onChange={(e) => setForm({ ...form, time: e.target.value })} required />
                </div>
                <div className="form-field">
                  <label>{t.restaurant.partySize}</label>
                  <input type="number" min={1} max={500} value={form.partySize} onChange={(e) => setForm({ ...form, partySize: Number(e.target.value) })} required />
                </div>
                <div className="form-field">
                  <label>{t.restaurant.tableNumber}</label>
                  <select value={form.tableId} onChange={(e) => setForm({ ...form, tableId: e.target.value })}>
                    <option value="">{t.restaurant.anyTable}</option>
                    {tables.map((tb) => (
                      <option key={tb.id} value={tb.id}>{tb.number}{tb.sectionName ? ` — ${tb.sectionName}` : ""} ({tb.capacity})</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="form-field">
                <label>{t.restaurant.notesLabel}</label>
                <textarea value={form.notes} maxLength={1000} rows={2} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                <button className="btn" type="submit" disabled={busy}>{t.common.save}</button>
                <button className="btn btn-secondary" type="button" onClick={() => setEditing(null)}>{t.common.cancel}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {seating && (
        <div className="modal-overlay" onClick={() => setSeating(null)}>
          <div className="card" style={{ maxWidth: 480, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ marginTop: 0 }}>{t.restaurant.seatTitle}</h3>
            <p className="text-muted">{seating.customerName} — {seating.partySize} · {timeOf(seating.reservationAt)}</p>
            {openedOrderId ? (
              <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                {!portal && <Link className="btn" to={`${posPath}?order=${openedOrderId}`}>{t.restaurant.openInPos}</Link>}
                <button className="btn btn-secondary" onClick={() => setSeating(null)}>{t.common.close}</button>
              </div>
            ) : (
              <>
                <p>{t.restaurant.seatIntro}</p>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                  <button type="button" className={seatType === RestaurantOrderType.DineIn ? "btn" : "btn btn-secondary"}
                    onClick={() => setSeatType(RestaurantOrderType.DineIn)} style={{ padding: 16 }}>
                    <IconGrid /> {t.restaurant.seatDineIn}
                  </button>
                  <button type="button" className={seatType === RestaurantOrderType.Takeaway ? "btn" : "btn btn-secondary"}
                    onClick={() => setSeatType(RestaurantOrderType.Takeaway)} style={{ padding: 16 }}>
                    <IconBox /> {t.restaurant.seatTakeaway}
                  </button>
                </div>
                {seatType === RestaurantOrderType.DineIn && (
                  <div className="form-field" style={{ marginTop: 12 }}>
                    <label>{t.restaurant.tableNumber}</label>
                    <select value={seatTableId} onChange={(e) => setSeatTableId(e.target.value)} required>
                      <option value="">-</option>
                      {freeTables.map((tb) => (
                        <option key={tb.id} value={tb.id}>{tb.number}{tb.sectionName ? ` — ${tb.sectionName}` : ""} ({tb.capacity})</option>
                      ))}
                    </select>
                  </div>
                )}
                {warehouses.length > 1 && (
                  <div className="form-field">
                    <label>{t.sales.warehouse}</label>
                    <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
                      {warehouses.map((w) => <option key={w.id} value={w.id}>{w.nameAr}</option>)}
                    </select>
                  </div>
                )}
                <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
                  <button className="btn" onClick={confirmSeat} disabled={busy || !warehouseId || (seatType === RestaurantOrderType.DineIn && !seatTableId)}>
                    {t.restaurant.seatOpenOrder}
                  </button>
                  <button className="btn btn-secondary" onClick={() => setSeating(null)}>{t.common.cancel}</button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
