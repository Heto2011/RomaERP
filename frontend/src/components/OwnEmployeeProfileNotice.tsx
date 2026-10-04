import { useEffect, useState } from "react";
import axios from "axios";
import { EmployeesApi } from "../api/services";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

/// Shown on the attendance and requests screens when the signed-in account has no employee profile — typically
/// the company owner or HR admin, who signs up as a user rather than as an employee. One click creates and links
/// a minimal profile so they can record their own attendance and requests; nothing shows once they have one.
export default function OwnEmployeeProfileNotice({ onCreated }: { onCreated: () => void }) {
  const { t } = useLanguage();
  const [missing, setMissing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    EmployeesApi.getMyProfile().catch((err) => {
      if (axios.isAxiosError(err) && err.response?.status === 404) setMissing(true);
    });
  }, []);

  async function create() {
    setBusy(true);
    setError(null);
    try {
      await EmployeesApi.createMyProfile();
      setMissing(false);
      onCreated();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  if (!missing) return null;
  return (
    <div className="card" style={{ borderInlineStart: "4px solid var(--color-warning, #b7791f)" }}>
      <strong>{t.hr.ownProfileTitle}</strong>
      <p className="text-muted" style={{ margin: "6px 0 10px" }}>{t.hr.ownProfileBody}</p>
      {error && <div className="alert-error">{error}</div>}
      <button className="btn" onClick={create} disabled={busy}>{busy ? "…" : t.hr.ownProfileCreate}</button>
    </div>
  );
}
