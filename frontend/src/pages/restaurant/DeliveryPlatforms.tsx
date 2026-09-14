import { useEffect, useState } from "react";
import { DeliveryPlatformsApi, ItemsApi } from "../../api/services";
import { DeliveryWebhookEventStatus, type DeliveryPlatformItemMapping, type DeliveryPlatformStatus, type DeliveryWebhookEvent, type Item } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

const statusLabelKey = {
  [DeliveryWebhookEventStatus.Received]: "webhookStatusReceived",
  [DeliveryWebhookEventStatus.Processed]: "webhookStatusProcessed",
  [DeliveryWebhookEventStatus.Failed]: "webhookStatusFailed",
} as const;
const statusBadgeClass = {
  [DeliveryWebhookEventStatus.Received]: "badge-draft",
  [DeliveryWebhookEventStatus.Processed]: "badge-posted",
  [DeliveryWebhookEventStatus.Failed]: "badge-reversed",
} as const;

export default function DeliveryPlatforms() {
  const { t, lang } = useLanguage();
  const [statuses, setStatuses] = useState<DeliveryPlatformStatus[]>([]);
  const [mappings, setMappings] = useState<DeliveryPlatformItemMapping[]>([]);
  const [events, setEvents] = useState<DeliveryWebhookEvent[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [platformName, setPlatformName] = useState("");
  const [externalItemId, setExternalItemId] = useState("");
  const [externalItemName, setExternalItemName] = useState("");
  const [itemId, setItemId] = useState("");

  async function load() {
    const [statusRes, mappingRes, eventRes, itemRes] = await Promise.all([
      DeliveryPlatformsApi.getStatus(),
      DeliveryPlatformsApi.getItemMappings(),
      DeliveryPlatformsApi.getEvents(),
      ItemsApi.getAll(),
    ]);
    setStatuses(statusRes.data);
    setMappings(mappingRes.data);
    setEvents(eventRes.data);
    setItems(itemRes.data);
    if (!platformName && statusRes.data.length > 0) setPlatformName(statusRes.data[0].name);
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleAddMapping(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await DeliveryPlatformsApi.setItemMapping({ platformName, externalItemId, externalItemName: externalItemName || null, itemId });
      setExternalItemId("");
      setExternalItemName("");
      setItemId("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDeleteMapping(id: string) {
    setError(null);
    try {
      await DeliveryPlatformsApi.deleteItemMapping(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleRetry(id: string) {
    setError(null);
    try {
      await DeliveryPlatformsApi.retryEvent(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.restaurant.deliveryPlatformsTitle}</h1>
      </div>
      <p className="text-muted">{t.restaurant.deliveryPlatformsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.restaurant.platformStatus}</h3>
        <div className="toolbar">
          {statuses.map((s) => (
            <span key={s.name} className={`badge ${s.isConfigured ? "badge-posted" : "badge-draft"}`}>
              {s.name}: {s.isConfigured ? t.restaurant.connected : t.restaurant.notConnected}
            </span>
          ))}
        </div>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.restaurant.itemMappingsTitle}</h3>
        <p className="text-muted" style={{ marginTop: 0 }}>{t.restaurant.itemMappingsIntro}</p>
        <form onSubmit={handleAddMapping} className="form-grid">
          <div className="form-field">
            <label>{t.restaurant.platformLabel}</label>
            <select value={platformName} onChange={(e) => setPlatformName(e.target.value)} required>
              {statuses.map((s) => (
                <option key={s.name} value={s.name}>{s.name}</option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label>{t.restaurant.externalItemId}</label>
            <input value={externalItemId} onChange={(e) => setExternalItemId(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.restaurant.externalItemName}</label>
            <input value={externalItemName} onChange={(e) => setExternalItemName(e.target.value)} />
          </div>
          <div className="form-field">
            <label>{t.restaurant.mappedItem}</label>
            <select value={itemId} onChange={(e) => setItemId(e.target.value)} required>
              <option value="">{t.restaurant.selectItem}</option>
              {items.map((i) => (
                <option key={i.id} value={i.id}>{i.code} - {bilingualName(i.nameAr, i.nameEn, lang)}</option>
              ))}
            </select>
          </div>
          <button className="btn" type="submit" style={{ alignSelf: "flex-end" }}>{t.restaurant.addMapping}</button>
        </form>

        <table style={{ marginTop: 16 }}>
          <thead>
            <tr>
              <th>{t.restaurant.platformLabel}</th>
              <th>{t.restaurant.externalItemId}</th>
              <th>{t.restaurant.externalItemName}</th>
              <th>{t.restaurant.mappedItem}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {mappings.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 20 }}>{t.common.noData}</td>
              </tr>
            )}
            {mappings.map((m) => (
              <tr key={m.id}>
                <td>{m.platformName}</td>
                <td>{m.externalItemId}</td>
                <td>{m.externalItemName ?? "-"}</td>
                <td>{m.itemName}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDeleteMapping(m.id)}>{t.common.delete}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.restaurant.webhookLogTitle} ({events.length})</h3>
        <table>
          <thead>
            <tr>
              <th>{t.restaurant.platformLabel}</th>
              <th>{t.common.date}</th>
              <th>{t.restaurant.createdOrder}</th>
              <th>{t.common.status}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {events.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 20 }}>{t.common.noData}</td>
              </tr>
            )}
            {events.map((e) => (
              <tr key={e.id}>
                <td>{e.platformName}</td>
                <td>{new Date(e.receivedAtUtc).toLocaleString()}</td>
                <td>{e.createdOrderNumber ?? "-"}</td>
                <td>
                  <span className={`badge ${statusBadgeClass[e.status]}`}>{t.restaurant[statusLabelKey[e.status]]}</span>
                  {e.errorMessage && <div className="text-muted" style={{ fontSize: 12, marginTop: 4 }}>{e.errorMessage}</div>}
                </td>
                <td>
                  {e.status === DeliveryWebhookEventStatus.Failed && e.isSignatureVerified && (
                    <button className="btn btn-secondary btn-sm" onClick={() => handleRetry(e.id)}>{t.restaurant.retryWebhook}</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
