import { useRef, useState } from "react";
import { AiAssistantApi } from "../../api/services";
import { ChatRole, ExpenseCaptureStatus, type ChatMessage } from "../../api/types";
import { getErrorMessage } from "../../api/client";

const statusLabel: Record<ExpenseCaptureStatus, string> = {
  [ExpenseCaptureStatus.AwaitingDetails]: "محتاج تفاصيل أكتر",
  [ExpenseCaptureStatus.AwaitingFundingSource]: "في انتظار تحديد مصدر الصرف (عهدة ولا جاري)",
  [ExpenseCaptureStatus.AwaitingCustodyEmployee]: "في انتظار تحديد الموظف صاحب العهدة",
  [ExpenseCaptureStatus.AwaitingPaymentMethod]: "في انتظار طريقة الدفع",
  [ExpenseCaptureStatus.AwaitingReconciliation]: "في انتظار المطابقة البنكية",
  [ExpenseCaptureStatus.PendingApproval]: "في انتظار اعتماد المدير",
  [ExpenseCaptureStatus.Posted]: "تم الاعتماد والترحيل",
  [ExpenseCaptureStatus.Rejected]: "مرفوض",
};

export default function AiAssistant() {
  const [messages, setMessages] = useState<ChatMessage[]>([
    { role: ChatRole.Assistant, content: "أهلاً! قولّي عملت مصروف إيه، وأنا هسجله وأترحّله في الحسابات. مثال: \"اشتريت بنزين بـ100 جنيه\".", createdAtUtc: new Date().toISOString() },
  ]);
  const [captureId, setCaptureId] = useState<string | null>(null);
  const [status, setStatus] = useState<ExpenseCaptureStatus | null>(null);
  const [input, setInput] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  async function handleSend(e: React.FormEvent) {
    e.preventDefault();
    if (!input.trim()) return;

    setError(null);
    const userMessage: ChatMessage = { role: ChatRole.User, content: input, createdAtUtc: new Date().toISOString() };
    setMessages((prev) => [...prev, userMessage]);
    setInput("");
    setSending(true);

    try {
      const res = await AiAssistantApi.sendMessage(captureId, userMessage.content);
      setCaptureId(res.data.captureId);
      setStatus(res.data.status);
      setMessages((prev) => [...prev, { role: ChatRole.Assistant, content: res.data.assistantReply, createdAtUtc: new Date().toISOString() }]);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  }

  async function handleUploadProof(file: File) {
    if (!captureId) return;
    setError(null);
    try {
      await AiAssistantApi.uploadProof(captureId, file);
      setMessages((prev) => [...prev, { role: ChatRole.Assistant, content: `تم إرفاق الإثبات: ${file.name} ✅`, createdAtUtc: new Date().toISOString() }]);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function startNewExpense() {
    setCaptureId(null);
    setStatus(null);
    setMessages([{ role: ChatRole.Assistant, content: "تمام، قولّي المصروف الجديد.", createdAtUtc: new Date().toISOString() }]);
  }

  return (
    <div>
      <div className="page-header">
        <h1>المساعد الذكي للمصروفات</h1>
        <button className="btn btn-secondary" onClick={startNewExpense}>
          + مصروف جديد
        </button>
      </div>

      {status && (
        <div className="card toolbar" style={{ marginBottom: 12 }}>
          <span className="badge badge-draft">{statusLabel[status]}</span>
          {(status === ExpenseCaptureStatus.AwaitingReconciliation || status === ExpenseCaptureStatus.PendingApproval) && (
            <>
              <span className="text-muted">تقدر ترفع صورة الإيصال كإثبات:</span>
              <button className="btn btn-secondary btn-sm" onClick={() => fileInputRef.current?.click()}>
                رفع إثبات
              </button>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/*,application/pdf"
                style={{ display: "none" }}
                onChange={(e) => e.target.files?.[0] && handleUploadProof(e.target.files[0])}
              />
            </>
          )}
        </div>
      )}

      {error && <div className="alert-error">{error}</div>}

      <div className="card" style={{ display: "flex", flexDirection: "column", height: 500 }}>
        <div style={{ flex: 1, overflowY: "auto", display: "flex", flexDirection: "column", gap: 10, padding: "4px 4px 16px" }}>
          {messages.map((m, i) => (
            <div
              key={i}
              style={{
                alignSelf: m.role === ChatRole.User ? "flex-start" : "flex-end",
                background: m.role === ChatRole.User ? "#f2f3f5" : "#e6f2f6",
                borderRadius: 10,
                padding: "8px 14px",
                maxWidth: "70%",
              }}
            >
              {m.content}
            </div>
          ))}
          {sending && <div className="text-muted" style={{ alignSelf: "flex-end" }}>بيكتب...</div>}
        </div>

        <form onSubmit={handleSend} style={{ display: "flex", gap: 10, borderTop: "1px solid var(--color-border)", paddingTop: 12 }}>
          <input
            style={{ flex: 1 }}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="اكتب المصروف هنا..."
            disabled={sending}
          />
          <button className="btn" type="submit" disabled={sending || !input.trim()}>
            إرسال
          </button>
        </form>
      </div>
    </div>
  );
}
