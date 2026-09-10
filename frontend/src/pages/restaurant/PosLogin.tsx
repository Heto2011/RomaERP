import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import { AuthApi } from "../../api/services";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const MAX_PIN_LENGTH = 6;

export default function PosLogin() {
  const { loginWithToken } = useAuth();
  const navigate = useNavigate();
  const { t } = useLanguage();

  const [companyCode, setCompanyCode] = useState(() => localStorage.getItem("companyCode") ?? "");
  const [pin, setPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function submit(fullPin: string) {
    if (!companyCode.trim()) {
      setError(t.posLogin.companyCode);
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const res = await AuthApi.posPinLogin(companyCode.trim().toLowerCase(), fullPin);
      const { data } = res;
      loginWithToken(companyCode.trim().toLowerCase(), data.token, data.email, data.fullName, data.roles, data.modules);
      navigate("/restaurant/pos");
    } catch (err) {
      setError(getErrorMessage(err) || t.posLogin.wrongPin);
      setPin("");
    } finally {
      setLoading(false);
    }
  }

  function press(digit: string) {
    if (loading) return;
    const next = (pin + digit).slice(0, MAX_PIN_LENGTH);
    setPin(next);
  }

  function backspace() {
    setPin((p) => p.slice(0, -1));
  }

  return (
    <div className="login-page">
      <div className="login-card" style={{ maxWidth: 340 }}>
        <h1>{t.posLogin.title}</h1>
        <p>{t.posLogin.subtitle}</p>

        <div className="form-field">
          <label>{t.posLogin.companyCode}</label>
          <input value={companyCode} onChange={(e) => setCompanyCode(e.target.value)} />
        </div>

        {error && <div className="alert-error">{error}</div>}

        <div style={{ display: "flex", justifyContent: "center", gap: 10, margin: "18px 0" }}>
          {Array.from({ length: MAX_PIN_LENGTH }).map((_, i) => (
            <span
              key={i}
              style={{
                width: 14,
                height: 14,
                borderRadius: "50%",
                border: "1px solid var(--color-border)",
                background: i < pin.length ? "var(--color-primary)" : "transparent",
              }}
            />
          ))}
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 10 }}>
          {["1", "2", "3", "4", "5", "6", "7", "8", "9"].map((d) => (
            <button key={d} type="button" className="btn btn-secondary" style={{ fontSize: 18, padding: "14px 0" }} onClick={() => press(d)} disabled={loading}>
              {d}
            </button>
          ))}
          <button type="button" className="btn btn-secondary" onClick={backspace} disabled={loading}>
            ⌫
          </button>
          <button type="button" className="btn btn-secondary" style={{ fontSize: 18, padding: "14px 0" }} onClick={() => press("0")} disabled={loading}>
            0
          </button>
          <button type="button" className="btn" onClick={() => submit(pin)} disabled={loading || pin.length < 4}>
            {t.posLogin.enter}
          </button>
        </div>

        <p style={{ marginTop: 16, fontSize: 13, textAlign: "center" }} className="text-muted">
          <Link to="/login">{t.posLogin.backToLogin}</Link>
        </p>
      </div>
    </div>
  );
}
