import { useNavigate } from "react-router-dom";

interface Action {
  label: string;
  to: string;
}

/// Shown instead of a report full of meaningless zeros — tells the customer exactly what's
/// missing and takes them straight to the screen that fixes it, instead of leaving them guessing.
export default function EmptyReportState({ message, actions }: { message: string; actions: Action[] }) {
  const navigate = useNavigate();
  return (
    <div className="card" style={{ textAlign: "center", padding: "40px 24px" }}>
      <div style={{ fontSize: 32, marginBottom: 12 }}>📊</div>
      <p className="text-muted" style={{ maxWidth: 440, margin: "0 auto 20px" }}>{message}</p>
      <div style={{ display: "flex", gap: 10, justifyContent: "center", flexWrap: "wrap" }}>
        {actions.map((a) => (
          <button key={a.to} type="button" className="btn btn-secondary btn-sm" onClick={() => navigate(a.to)}>
            {a.label}
          </button>
        ))}
      </div>
    </div>
  );
}
