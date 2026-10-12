import { useEffect, useState } from "react";
import { LoyaltyApi } from "../../api/services";
import type { LoyaltyMember, LoyaltyMemberDetail, LoyaltySettings } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function LoyaltyPage() {
  const { t, lang } = useLanguage();
  const [settings, setSettings] = useState<LoyaltySettings | null>(null);
  const [members, setMembers] = useState<LoyaltyMember[]>([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [detail, setDetail] = useState<LoyaltyMemberDetail | null>(null);
  const [adjustPoints, setAdjustPoints] = useState("");
  const [adjustNote, setAdjustNote] = useState("");

  const [showNew, setShowNew] = useState(false);
  const [newName, setNewName] = useState("");
  const [newPhone, setNewPhone] = useState("");

  const dateFormat = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString(lang === "ar" ? "ar-EG" : "en-GB") : "—");

  async function loadMembers(text = search) {
    const res = await LoyaltyApi.getMembers(text || undefined);
    setMembers(res.data);
  }

  useEffect(() => {
    LoyaltyApi.getSettings().then((r) => setSettings(r.data)).catch((e) => setError(getErrorMessage(e)));
    loadMembers("").catch((e) => setError(getErrorMessage(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function saveSettings(e: React.FormEvent) {
    e.preventDefault();
    if (!settings) return;
    setError(null);
    setMessage(null);
    try {
      const res = await LoyaltyApi.updateSettings(settings);
      setSettings(res.data);
      setMessage(t.restaurant.loyaltySaved);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function openDetail(id: string) {
    setError(null);
    try {
      const res = await LoyaltyApi.getMember(id);
      setDetail(res.data);
      setAdjustPoints("");
      setAdjustNote("");
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleAdjust(e: React.FormEvent) {
    e.preventDefault();
    if (!detail) return;
    setError(null);
    try {
      await LoyaltyApi.adjustPoints(detail.id, { points: Number(adjustPoints), note: adjustNote || null });
      await openDetail(detail.id);
      await loadMembers();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await LoyaltyApi.createMember({ name: newName, phone: newPhone });
      setShowNew(false);
      setNewName("");
      setNewPhone("");
      await loadMembers();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.restaurant.loyaltyTitle}</h1>
        <button className="btn" onClick={() => setShowNew((v) => !v)}>
          {showNew ? t.common.cancel : t.restaurant.loyaltyAddMember}
        </button>
      </div>
      <p className="text-muted">{t.restaurant.loyaltyIntro}</p>

      {error && <div className="alert-error">{error}</div>}
      {message && <div className="alert-success">{message}</div>}

      {settings && (
        <div className="card">
          <form onSubmit={saveSettings}>
            <label style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: "normal", marginBottom: 12 }}>
              <input type="checkbox" checked={settings.enabled} onChange={(e) => setSettings({ ...settings, enabled: e.target.checked })} />
              {t.restaurant.loyaltyEnabled}
            </label>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.restaurant.loyaltySpendPerPoint}</label>
                <input type="number" min={0.01} step="0.01" value={settings.spendPerPoint} onChange={(e) => setSettings({ ...settings, spendPerPoint: Number(e.target.value) })} />
              </div>
              <div className="form-field">
                <label>{t.restaurant.loyaltyPointValue}</label>
                <input type="number" min={0} step="0.01" value={settings.pointValue} onChange={(e) => setSettings({ ...settings, pointValue: Number(e.target.value) })} />
              </div>
              <div className="form-field">
                <label>{t.restaurant.loyaltyMinRedeem}</label>
                <input type="number" min={0} step="1" value={settings.minRedeemPoints} onChange={(e) => setSettings({ ...settings, minRedeemPoints: Number(e.target.value) })} />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 12 }}>{t.common.save}</button>
          </form>
        </div>
      )}

      {showNew && (
        <div className="card">
          <form onSubmit={handleCreate}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.restaurant.loyaltyMember}</label>
                <input value={newName} onChange={(e) => setNewName(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.restaurant.loyaltyPhone}</label>
                <input value={newPhone} onChange={(e) => setNewPhone(e.target.value)} inputMode="tel" required />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 12 }}>{t.common.save}</button>
          </form>
        </div>
      )}

      <div className="card toolbar">
        <div className="form-field" style={{ flex: 1 }}>
          <input
            placeholder={t.restaurant.loyaltySearch}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && loadMembers()}
          />
        </div>
        <button className="btn" onClick={() => loadMembers()}>{t.common.viewReport}</button>
      </div>

      <div className="card" style={{ overflowX: "auto" }}>
        <table>
          <thead>
            <tr>
              <th>{t.restaurant.loyaltyMember}</th>
              <th>{t.restaurant.loyaltyPhone}</th>
              <th>{t.restaurant.loyaltyPoints}</th>
              <th>{t.restaurant.loyaltyVisits}</th>
              <th>{t.restaurant.loyaltyTotalSpent}</th>
              <th>{t.restaurant.loyaltyAvgSpend}</th>
              <th>{t.restaurant.loyaltyLastVisit}</th>
            </tr>
          </thead>
          <tbody>
            {members.length === 0 && (
              <tr><td colSpan={7} className="text-muted" style={{ textAlign: "center", padding: 20 }}>{t.restaurant.loyaltyNoMembers}</td></tr>
            )}
            {members.map((m) => (
              <tr key={m.id} style={{ cursor: "pointer" }} onClick={() => openDetail(m.id)}>
                <td>{m.name}</td>
                <td dir="ltr">{m.phone}</td>
                <td><strong>{m.pointsBalance.toLocaleString()}</strong></td>
                <td>{m.visitCount}</td>
                <td>{m.totalSpent.toLocaleString()}</td>
                <td>{m.averageSpend.toLocaleString()}</td>
                <td>{dateFormat(m.lastVisitUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {detail && (
        <div className="modal-overlay" onClick={() => setDetail(null)}>
          <div className="card" style={{ maxWidth: 720, margin: "4% auto", maxHeight: "88vh", overflowY: "auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{detail.name} — <span dir="ltr">{detail.phone}</span></h3>
            <p>
              {t.restaurant.loyaltyBalance}: <strong>{detail.pointsBalance.toLocaleString()}</strong> · {t.restaurant.loyaltyVisits}: {detail.visitCount} ·{" "}
              {t.restaurant.loyaltyTotalSpent}: {detail.totalSpent.toLocaleString()}
            </p>
            {detail.favouriteItems.length > 0 && (
              <p><strong>{t.restaurant.loyaltyFavourites}:</strong> {detail.favouriteItems.join("، ")}</p>
            )}

            <h4>{t.restaurant.loyaltyHistory}</h4>
            <table>
              <tbody>
                {detail.orders.map((o) => (
                  <tr key={o.orderId}>
                    <td>{dateFormat(o.orderDate)}</td>
                    <td>{o.orderNumber}</td>
                    <td>{o.items.join("، ")}</td>
                    <td>{o.total.toLocaleString()}</td>
                    <td className="text-muted">+{o.pointsEarned} / -{o.pointsRedeemed}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <h4>{t.restaurant.loyaltyLedger}</h4>
            <table>
              <tbody>
                {detail.transactions.map((x, i) => (
                  <tr key={i}>
                    <td>{dateFormat(x.createdAtUtc)}</td>
                    <td>{t.restaurant.loyaltyTypes[x.type]}</td>
                    <td className={x.points < 0 ? "text-danger" : "text-success"}>{x.points > 0 ? "+" : ""}{x.points}</td>
                    <td>{x.balanceAfter}</td>
                    <td className="text-muted">{x.note}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <h4>{t.restaurant.loyaltyAdjust}</h4>
            <form onSubmit={handleAdjust} style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "flex-end" }}>
              <div className="form-field">
                <label>{t.restaurant.loyaltyPoints}</label>
                <input type="number" step="1" value={adjustPoints} onChange={(e) => setAdjustPoints(e.target.value)} placeholder={t.restaurant.loyaltyAdjustHint} required />
              </div>
              <div className="form-field">
                <label>{t.restaurant.loyaltyNote}</label>
                <input value={adjustNote} onChange={(e) => setAdjustNote(e.target.value)} />
              </div>
              <button className="btn" type="submit">{t.common.save}</button>
              <button className="btn btn-secondary" type="button" onClick={() => setDetail(null)}>{t.common.cancel}</button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
