import { useState, type FormEvent } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import { usePortalManifest } from "../utils/pwa";
import PasswordInput from "../components/PasswordInput";

/// <summary>A distinct front door for warehouse/inventory staff — same login API/tenant as the main app,
/// but branded and landing straight in the ROMA Inventory portal, the same treatment PeopleLogin/
/// RestaurantLogin give the HR and POS modules.</summary>
export default function InventoryLogin() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const { t, lang, setLang } = useLanguage();
  usePortalManifest("inventory");
  const [companyCode, setCompanyCode] = useState(() => localStorage.getItem("companyCode") ?? "");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(companyCode.trim().toLowerCase(), email, password);
      navigate("/inventory-portal");
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-page inventory-theme">
      <div className="login-card">
        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <button className="btn btn-secondary btn-sm" onClick={() => setLang(lang === "ar" ? "en" : "ar")}>
            {t.language}
          </button>
        </div>
        <h1>{t.inventoryAppName}</h1>
        <p>{t.login.subtitle}</p>
        {error && <div className="alert-error">{error}</div>}
        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <div className="form-field">
            <label>{t.login.companyCode}</label>
            <input type="text" value={companyCode} onChange={(e) => setCompanyCode(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.login.email}</label>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>
          <div className="form-field">
            <label>{t.login.password}</label>
            <PasswordInput value={password} onChange={setPassword} required />
          </div>
          <button className="btn" type="submit" disabled={loading}>
            {loading ? t.login.submitting : t.login.submit}
          </button>
        </form>
        <p style={{ marginTop: 16, fontSize: 13 }} className="text-muted">
          {t.poweredByRomaErpInventory}
        </p>
        <p style={{ marginTop: 8, fontSize: 13 }} className="text-muted">
          <Link to="/login">{t.backToRomaErp}</Link>
        </p>
      </div>
    </div>
  );
}
