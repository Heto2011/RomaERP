import { useState, type ReactNode } from "react";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

interface QuickAddBulkProps {
  open: boolean;
  onClose: () => void;
  title: string;
  hint: string;
  placeholder: string;
  /** Extra fields shared by the whole batch (e.g. category/unit for items). */
  extraFields?: ReactNode;
  /** Disallow submitting until the extra fields above are filled in. */
  disabled?: boolean;
  /** Create a single record for this name. Throw to report a per-line failure. */
  onCreateOne: (name: string, index: number) => Promise<void>;
  /** Called once after the batch finishes, so the caller can reload its list. */
  onFinished: () => void;
}

export default function QuickAddBulk({ open, onClose, title, hint, placeholder, extraFields, disabled, onCreateOne, onFinished }: QuickAddBulkProps) {
  const { t } = useLanguage();
  const [text, setText] = useState("");
  const [saving, setSaving] = useState(false);
  const [result, setResult] = useState<{ ok: number; failed: { name: string; error: string }[] } | null>(null);

  if (!open) return null;

  async function handleSubmit() {
    const names = Array.from(new Set(text.split("\n").map((line) => line.trim()).filter(Boolean)));
    if (names.length === 0) return;
    setSaving(true);
    setResult(null);
    let ok = 0;
    const failed: { name: string; error: string }[] = [];
    for (let i = 0; i < names.length; i++) {
      try {
        await onCreateOne(names[i], i);
        ok++;
      } catch (err) {
        failed.push({ name: names[i], error: getErrorMessage(err) });
      }
    }
    setSaving(false);
    setResult({ ok, failed });
    onFinished();
    if (failed.length === 0) setText("");
  }

  function handleClose() {
    setText("");
    setResult(null);
    onClose();
  }

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="card" style={{ maxWidth: 520, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
        <h3>{title}</h3>
        <p className="text-muted">{hint}</p>

        {extraFields}

        <textarea
          rows={10}
          style={{ width: "100%", marginTop: 10, fontFamily: "inherit" }}
          placeholder={placeholder}
          value={text}
          onChange={(e) => setText(e.target.value)}
        />

        {result && (
          <div style={{ marginTop: 10 }}>
            <div className="text-success">
              {t.common.quickAddResultOk} {result.ok} {t.common.quickAddResultOkSuffix}
            </div>
            {result.failed.length > 0 && (
              <div className="alert-error" style={{ marginTop: 6 }}>
                {result.failed.map((f) => (
                  <div key={f.name}>
                    {t.common.quickAddResultFailed} "{f.name}": {f.error}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
          <button className="btn" onClick={handleSubmit} disabled={saving || disabled || !text.trim()}>
            {saving ? t.common.quickAddSubmitting : t.common.quickAddSubmit}
          </button>
          <button className="btn btn-secondary" onClick={handleClose}>
            {t.common.close}
          </button>
        </div>
      </div>
    </div>
  );
}
