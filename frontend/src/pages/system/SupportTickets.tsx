import { useState } from "react";
import { SystemSupportApi } from "../../api/services";
import type { SupportTicket, SupportTicketStatus, SupportTicketSummary } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const STATUSES: SupportTicketStatus[] = ["Open", "AwaitingCustomer", "InProgress", "Resolved", "Closed"];

export default function SupportTicketsPage() {
  const [systemKey, setSystemKey] = useState("");
  const [tickets, setTickets] = useState<SupportTicketSummary[] | null>(null);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [selected, setSelected] = useState<SupportTicket | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [replyBody, setReplyBody] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function loadTickets() {
    if (!systemKey) {
      setError("Enter the system key first.");
      return;
    }
    setError(null);
    try {
      const res = await SystemSupportApi.getAllTickets(systemKey, statusFilter || undefined);
      setTickets(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function openTicket(id: string) {
    setError(null);
    try {
      const res = await SystemSupportApi.getTicket(systemKey, id);
      setSelected(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleReply(e: React.FormEvent) {
    e.preventDefault();
    if (!selected) return;
    setError(null);
    setSubmitting(true);
    try {
      const res = await SystemSupportApi.reply(systemKey, selected.id, replyBody);
      setSelected(res.data);
      setReplyBody("");
      loadTickets();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleStatusChange(status: SupportTicketStatus) {
    if (!selected) return;
    setError(null);
    try {
      await SystemSupportApi.updateStatus(systemKey, selected.id, status);
      const res = await SystemSupportApi.getTicket(systemKey, selected.id);
      setSelected(res.data);
      loadTickets();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  if (selected) {
    return (
      <div style={{ maxWidth: 900, margin: "40px auto", padding: "0 20px" }}>
        <button className="btn btn-secondary btn-sm" onClick={() => setSelected(null)}>← Back to tickets</button>
        <h1>#{selected.ticketNumber} — {selected.subject}</h1>
        <p className="text-muted">
          {selected.companyCode} · {selected.requesterName} ({selected.requesterEmail})
        </p>

        <div className="form-field" style={{ maxWidth: 260 }}>
          <label>Status</label>
          <select value={selected.status} onChange={(e) => handleStatusChange(e.target.value as SupportTicketStatus)}>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>

        {error && <div className="alert-error">{error}</div>}

        <div style={{ display: "flex", flexDirection: "column", gap: 10, margin: "16px 0" }}>
          {selected.messages.map((m) => (
            <div className="card" key={m.id}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <strong>{m.senderName} ({m.senderType})</strong>
                <span className="text-muted" style={{ fontSize: 12 }}>{new Date(m.createdAtUtc).toLocaleString()}</span>
              </div>
              <div style={{ marginTop: 6, whiteSpace: "pre-wrap" }}>{m.body}</div>
              {m.attachments.length > 0 && (
                <div style={{ marginTop: 8, display: "flex", flexWrap: "wrap", gap: 8 }}>
                  {m.attachments.map((a) => (
                    <button
                      key={a.id}
                      type="button"
                      className="btn btn-secondary btn-sm"
                      onClick={() => SystemSupportApi.downloadAttachment(systemKey, a.id, a.fileName)}
                    >
                      📎 {a.fileName}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>

        <form onSubmit={handleReply} className="card">
          <div className="form-field">
            <textarea rows={3} value={replyBody} onChange={(e) => setReplyBody(e.target.value)} placeholder="Reply to the customer..." required />
          </div>
          <button className="btn" type="submit" disabled={submitting}>Send reply</button>
        </form>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 900, margin: "40px auto", padding: "0 20px" }}>
      <h1>Support Inbox</h1>
      <p className="text-muted">Internal use only — every tenant's support tickets in one place.</p>

      <div className="card">
        <div className="form-grid">
          <div className="form-field">
            <label>System Key</label>
            <input type="password" value={systemKey} onChange={(e) => setSystemKey(e.target.value)} placeholder="X-System-Key" />
          </div>
          <div className="form-field">
            <label>Filter by status</label>
            <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
              <option value="">All</option>
              {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>
        </div>
        <button className="btn btn-sm" onClick={loadTickets}>Load tickets</button>
      </div>

      {error && <div className="alert-error" style={{ marginTop: 16 }}>{error}</div>}

      {tickets && tickets.length === 0 && <div className="card text-muted" style={{ marginTop: 16 }}>No tickets found.</div>}

      {tickets && tickets.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 16 }}>
          {tickets.map((ticket) => (
            <div className="card" key={ticket.id} style={{ cursor: "pointer" }} onClick={() => openTicket(ticket.id)}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <strong>#{ticket.ticketNumber} — {ticket.subject}</strong>
                <span className="badge">{ticket.status}</span>
              </div>
              <div className="text-muted" style={{ fontSize: 13, marginTop: 6 }}>
                {ticket.companyCode} · {ticket.requesterName} · {new Date(ticket.createdAtUtc).toLocaleString()}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
