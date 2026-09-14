import { useEffect, useState } from "react";
import { WorkLocationsApi } from "../../api/services";
import type { WorkLocation } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const emptyForm = { name: "", latitude: "", longitude: "", geofenceRadiusMeters: "20", isActive: true };

export default function WorkLocations() {
  const { t } = useLanguage();
  const [locations, setLocations] = useState<WorkLocation[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const res = await WorkLocationsApi.getAll();
    setLocations(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  function startCreate() {
    setEditingId(null);
    setForm(emptyForm);
    setShowForm(true);
  }

  function startEdit(location: WorkLocation) {
    setEditingId(location.id);
    setForm({
      name: location.name,
      latitude: String(location.latitude),
      longitude: String(location.longitude),
      geofenceRadiusMeters: String(location.geofenceRadiusMeters),
      isActive: location.isActive,
    });
    setShowForm(true);
  }

  function useCurrentLocation() {
    navigator.geolocation.getCurrentPosition(
      (pos) => setForm((f) => ({ ...f, latitude: String(pos.coords.latitude), longitude: String(pos.coords.longitude) })),
      () => setError(t.hr.locationDenied)
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const payload = {
        name: form.name,
        latitude: Number(form.latitude),
        longitude: Number(form.longitude),
        geofenceRadiusMeters: Number(form.geofenceRadiusMeters),
        isActive: form.isActive,
      };
      if (editingId) await WorkLocationsApi.update(editingId, payload);
      else await WorkLocationsApi.create(payload);
      setShowForm(false);
      setForm(emptyForm);
      setEditingId(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await WorkLocationsApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.workLocationsTitle}</h1>
        <button className="btn" onClick={showForm ? () => setShowForm(false) : startCreate}>
          {showForm ? t.common.cancel : t.hr.newWorkLocation}
        </button>
      </div>
      <p className="text-muted">{t.hr.workLocationsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.hr.locationName}</label>
                <input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.hr.latitude}</label>
                <input type="number" step="0.000001" value={form.latitude} onChange={(e) => setForm((f) => ({ ...f, latitude: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.hr.longitude}</label>
                <input type="number" step="0.000001" value={form.longitude} onChange={(e) => setForm((f) => ({ ...f, longitude: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.hr.geofenceRadius}</label>
                <input type="number" min={1} value={form.geofenceRadiusMeters} onChange={(e) => setForm((f) => ({ ...f, geofenceRadiusMeters: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>
                  <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} /> {t.hr.isActive}
                </label>
              </div>
            </div>
            <div style={{ display: "flex", gap: 10, marginTop: 14 }}>
              <button className="btn btn-secondary" type="button" onClick={useCurrentLocation}>
                {t.hr.useMyCurrentLocation}
              </button>
              <button className="btn" type="submit">
                {t.common.save}
              </button>
            </div>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.hr.locationName}</th>
              <th>{t.hr.latitude}</th>
              <th>{t.hr.longitude}</th>
              <th>{t.hr.geofenceRadius}</th>
              <th>{t.hr.isActive}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {locations.map((l) => (
              <tr key={l.id}>
                <td>{l.name}</td>
                <td>{l.latitude}</td>
                <td>{l.longitude}</td>
                <td>{l.geofenceRadiusMeters}</td>
                <td>
                  <span className={`badge ${l.isActive ? "badge-posted" : "badge-draft"}`}>{l.isActive ? t.common.active : t.common.inactive}</span>
                </td>
                <td style={{ display: "flex", gap: 8 }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => startEdit(l)}>
                    {t.common.edit}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(l.id)}>
                    {t.common.delete}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
