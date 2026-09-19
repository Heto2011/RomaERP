import { useEffect, useState } from "react";
import { SupportApi } from "../api/services";
import type { SupportTicket, SupportTicketSummary } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const STATUS_KEY: Record<string, string> = {
  Open: "statusOpen",
  AwaitingCustomer: "statusAwaitingCustomer",
  InProgress: "statusInProgress",
  Resolved: "statusResolved",
  Closed: "statusClosed",
};

const SENDER_KEY: Record<string, string> = {
  Customer: "senderCustomer",
  Support: "senderSupport",
  Ai: "senderAi",
};

export default function SupportPage() {
  const { t } = useLanguage();
  const [tickets, setTickets] = useState<SupportTicketSummary[] | null>(null);
  const [selected, setSelected] = useState<SupportTicket | null>(null);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [files, setFiles] = useState<File[]>([]);

  const [replyBody, setReplyBody] = useState("");
  const [replyFiles, setReplyFiles] = useState<File[]>([]);

  async function loadList() {
    setError(null);
    try {
      const res = await SupportApi.getMyTickets();
      setTickets(res.data);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  useEffect(() => {
    loadList();
  }, []);

  async function openTicket(id: string) {
    setError(null);
    try {
      const res = await SupportApi.getTicket(id);
      setSelected(res.data);
      setCreating(false);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const res = await SupportApi.createTicket(subject, body, files);
      setSubject("");
      setBody("");
      setFiles([]);
      setCreating(false);
      setSelected(res.data);
      loadList();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleReply(e: React.FormEvent) {
    e.preventDefault();
    if (!selected) return;
    setError(null);
    setSubmitting(true);
    try {
      const res = await SupportApi.addMessage(selected.id, replyBody, replyFiles);
      setReplyBody("");
      setReplyFiles([]);
      setSelected(res.data);
      loadList();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (selected) {
    return (
      <div>
        <div className="page-header">
          <h1>{t.support.ticketNumber} #{selected.ticketNumber}</h1>
          <button className="btn btn-secondary btn-sm" onClick={() => setSelected(null)}>{t.support.backToList}</button>
        </div>
        <p className="text-muted" style={{ marginTop: 0 }}>{selected.subject}</p>
        {error && <div className="alert-error">{error}</div>}

        <div style={{ display: "flex", flexDirection: "column", gap: 10, marginBottom: 20 }}>
          {selected.messages.map((m) => (
            <div className="card" key={m.id}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <strong>{t.support[SENDER_KEY[m.senderType] as keyof typeof t.support]}</strong>
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
                      onClick={() => SupportApi.downloadAttachment(selected.id, a.id, a.fileName)}
                    >
                      📎 {a.fileName}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>

        {selected.status === "Closed" ? (
          <div className="card text-muted">{t.support.closedNotice}</div>
        ) : (
          <form onSubmit={handleReply} className="card">
            <div className="form-field">
              <textarea rows={3} value={replyBody} onChange={(e) => setReplyBody(e.target.value)} placeholder={t.support.replyPlaceholder} required />
            </div>
            <div className="form-field">
              <label>{t.support.attachments}</label>
              <input type="file" multiple onChange={(e) => setReplyFiles(Array.from(e.target.files ?? []))} />
            </div>
            <button className="btn" type="submit" disabled={submitting}>{t.support.sendReply}</button>
          </form>
        )}
      </div>
    );
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.support.title}</h1>
        <button className="btn btn-sm" onClick={() => setCreating(!creating)}>{t.support.newTicket}</button>
      </div>
      <p className="text-muted">{t.support.intro}</p>
      {error && <div className="alert-error">{error}</div>}

      {creating && (
        <form onSubmit={handleCreate} className="card">
          <div className="form-field">
            <label>{t.support.subject}</label>
            <input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder={t.support.subjectPlaceholder} required />
          </div>
          <div className="form-field">
            <label>{t.support.description}</label>
            <textarea rows={4} value={body} onChange={(e) => setBody(e.target.value)} placeholder={t.support.descriptionPlaceholder} required />
          </div>
          <div className="form-field">
            <label>{t.support.attachments}</label>
            <input type="file" multiple onChange={(e) => setFiles(Array.from(e.target.files ?? []))} />
          </div>
          <button className="btn" type="submit" disabled={submitting}>{t.support.submit}</button>
        </form>
      )}

      {tickets && tickets.length === 0 && !creating && <div className="card text-muted">{t.support.noTickets}</div>}

      {tickets && tickets.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {tickets.map((ticket) => (
            <div className="card" key={ticket.id} style={{ cursor: "pointer" }} onClick={() => openTicket(ticket.id)}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <strong>#{ticket.ticketNumber} — {ticket.subject}</strong>
                <span className="badge">{t.support[STATUS_KEY[ticket.status] as keyof typeof t.support]}</span>
              </div>
              <div className="text-muted" style={{ fontSize: 13, marginTop: 6 }}>
                {new Date(ticket.createdAtUtc).toLocaleString()}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
