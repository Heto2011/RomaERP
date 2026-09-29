import { useEffect, useState } from "react";
import { SalesApi, LookupsApi } from "../../api/services";
import { EInvoiceStatus, SalesNoteType, type FiscalPeriod, type SalesInvoice, type SalesNote, type SalesNoteLineInput } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

const emptyLine = (): SalesNoteLineInput => ({ description: "", quantity: 1, unitPrice: 0 });

export default function SalesNotes() {
  const { t } = useLanguage();
  const [notes, setNotes] = useState<SalesNote[]>([]);
  const [invoices, setInvoices] = useState<SalesInvoice[]>([]);
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [vatRate, setVatRate] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const [showForm, setShowForm] = useState(false);
  const [originalInvoiceId, setOriginalInvoiceId] = useState("");
  const [noteType, setNoteType] = useState<SalesNoteType>(SalesNoteType.Credit);
  const [fiscalPeriodId, setFiscalPeriodId] = useState("");
  const [noteDate, setNoteDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState("");
  const [notesText, setNotesText] = useState("");
  const [lines, setLines] = useState<SalesNoteLineInput[]>([emptyLine()]);

  const [submittingEInvoiceId, setSubmittingEInvoiceId] = useState<string | null>(null);

  const netTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const vatTotal = netTotal * vatRate;
  const grandTotal = netTotal + vatTotal;

  async function load() {
    const [notesRes, invoicesRes, periodsRes, settingsRes] = await Promise.all([
      SalesApi.getNotes(),
      SalesApi.getInvoices(),
      LookupsApi.fiscalPeriods(),
      LookupsApi.companySettings(),
    ]);
    setNotes(notesRes.data);
    setInvoices(invoicesRes.data);
    setPeriods(periodsRes.data);
    setVatRate(settingsRes.data.vatRate);
  }

  useEffect(() => {
    load();
  }, []);

  function updateLine(idx: number, patch: Partial<SalesNoteLineInput>) {
    setLines((prev) => prev.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  }

  function resetForm() {
    setOriginalInvoiceId("");
    setNoteType(SalesNoteType.Credit);
    setFiscalPeriodId("");
    setReason("");
    setNotesText("");
    setLines([emptyLine()]);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await SalesApi.createNote({
        originalInvoiceId,
        noteType,
        noteDate,
        fiscalPeriodId,
        reason,
        notes: notesText || null,
        lines,
      });
      setShowForm(false);
      resetForm();
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDownloadPdf(note: SalesNote) {
    setError(null);
    try {
      const res = await SalesApi.downloadNotePdf(note.id);
      const url = window.URL.createObjectURL(res.data as Blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${note.noteNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleSubmitEInvoice(noteId: string) {
    setError(null);
    setSubmittingEInvoiceId(noteId);
    try {
      await SalesApi.submitNoteEInvoice(noteId);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSubmittingEInvoiceId(null);
    }
  }

  const noteTypeLabel: Record<SalesNoteType, string> = {
    [SalesNoteType.Credit]: t.sales.creditNote,
    [SalesNoteType.Debit]: t.sales.debitNote,
  };

  const eInvoiceStatusLabel: Record<EInvoiceStatus, string> = {
    [EInvoiceStatus.NotSubmitted]: t.eInvoicing.statuses.notSubmitted,
    [EInvoiceStatus.Submitted]: t.eInvoicing.statuses.submitted,
    [EInvoiceStatus.Accepted]: t.eInvoicing.statuses.accepted,
    [EInvoiceStatus.Rejected]: t.eInvoicing.statuses.rejected,
  };

  const eInvoiceStatusClass: Record<EInvoiceStatus, string> = {
    [EInvoiceStatus.NotSubmitted]: "text-muted",
    [EInvoiceStatus.Submitted]: "text-muted",
    [EInvoiceStatus.Accepted]: "text-success",
    [EInvoiceStatus.Rejected]: "text-danger",
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.sales.notesTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.sales.newNote}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <h3>{t.sales.createNoteTitle}</h3>
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.sales.originalInvoice}</label>
                <select value={originalInvoiceId} onChange={(e) => setOriginalInvoiceId(e.target.value)} required>
                  <option value="" disabled>-</option>
                  {invoices.map((inv) => (
                    <option key={inv.id} value={inv.id}>{inv.invoiceNumber} - {inv.customerName}</option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.sales.noteType}</label>
                <select value={noteType} onChange={(e) => setNoteType(Number(e.target.value) as SalesNoteType)}>
                  <option value={SalesNoteType.Credit}>{t.sales.creditNote}</option>
                  <option value={SalesNoteType.Debit}>{t.sales.debitNote}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.common.date}</label>
                <input type="date" value={noteDate} onChange={(e) => setNoteDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.fiscalPeriod}</label>
                <select value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} required>
                  <option value="" disabled>-</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id}>{p.name}</option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.sales.reason}</label>
                <input value={reason} onChange={(e) => setReason(e.target.value)} required />
              </div>
            </div>

            <div style={{ marginTop: 16 }}>
              <table>
                <thead>
                  <tr>
                    <th>{t.common.description}</th>
                    <th>{t.common.quantity}</th>
                    <th>{t.common.unitPrice}</th>
                    <th>{t.common.vat}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {lines.map((line, idx) => (
                    <tr key={idx}>
                      <td><input value={line.description} onChange={(e) => updateLine(idx, { description: e.target.value })} required /></td>
                      <td><input type="number" min={0.0001} step="0.01" value={line.quantity} onChange={(e) => updateLine(idx, { quantity: Number(e.target.value) })} style={{ width: 90 }} required /></td>
                      <td><input type="number" min={0} step="0.01" value={line.unitPrice} onChange={(e) => updateLine(idx, { unitPrice: Number(e.target.value) })} style={{ width: 110 }} required /></td>
                      <td className="text-muted">{(vatRate * 100).toFixed(0)}%</td>
                      <td>
                        {lines.length > 1 && (
                          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setLines((prev) => prev.filter((_, i) => i !== idx))}>
                            {t.common.delete}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <button type="button" className="btn btn-secondary btn-sm" style={{ marginTop: 8 }} onClick={() => setLines((prev) => [...prev, emptyLine()])}>
                {t.sales.addLine}
              </button>

              <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 12 }}>
                <table style={{ width: 260 }}>
                  <tbody>
                    <tr>
                      <td>{t.common.subtotal}</td>
                      <td style={{ textAlign: "end" }}>{netTotal.toLocaleString()}</td>
                    </tr>
                    <tr>
                      <td>{t.common.vat} ({(vatRate * 100).toFixed(0)}%)</td>
                      <td style={{ textAlign: "end" }}>{vatTotal.toLocaleString()}</td>
                    </tr>
                    <tr>
                      <td><strong>{t.common.total}</strong></td>
                      <td style={{ textAlign: "end" }}><strong>{grandTotal.toLocaleString()}</strong></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <div className="form-field" style={{ marginTop: 14 }}>
              <label>{t.common.notes}</label>
              <input value={notesText} onChange={(e) => setNotesText(e.target.value)} />
            </div>

            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.sales.noteNumber}</th>
              <th>{t.sales.noteType}</th>
              <th>{t.common.date}</th>
              <th>{t.sales.customer}</th>
              <th>{t.sales.originalInvoice}</th>
              <th>{t.common.total}</th>
              <th>{t.sales.eInvoice}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {notes.length === 0 && (
              <tr>
                <td colSpan={8} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {notes.map((note) => (
              <tr key={note.id}>
                <td>{note.noteNumber}</td>
                <td>{noteTypeLabel[note.noteType]}</td>
                <td>{new Date(note.noteDate).toLocaleDateString()}</td>
                <td>{note.customerName}</td>
                <td>{note.originalInvoiceNumber}</td>
                <td>{note.totalAmount.toLocaleString()}</td>
                <td>
                  <span className={eInvoiceStatusClass[note.eInvoiceStatus]} title={note.eInvoiceErrorMessage ?? undefined}>
                    {eInvoiceStatusLabel[note.eInvoiceStatus]}
                  </span>
                </td>
                <td style={{ display: "flex", gap: 6 }}>
                  <button className="btn btn-secondary btn-sm" title={t.sales.downloadPdf} onClick={() => handleDownloadPdf(note)}>
                    🖨️ {t.sales.downloadPdf}
                  </button>
                  {note.eInvoiceStatus !== EInvoiceStatus.Accepted && note.eInvoiceStatus !== EInvoiceStatus.Submitted && (
                    <button
                      className="btn btn-secondary btn-sm"
                      disabled={submittingEInvoiceId === note.id}
                      onClick={() => handleSubmitEInvoice(note.id)}
                    >
                      {submittingEInvoiceId === note.id ? t.eInvoicing.submitting : `🧾 ${t.eInvoicing.submit}`}
                    </button>
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
