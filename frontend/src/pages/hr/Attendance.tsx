import { useEffect, useRef, useState } from "react";
import { AttendanceApi } from "../../api/services";
import type { AttendanceRecord } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

type PendingAction = "checkin" | "checkout" | null;

function getPosition(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
      reject(new Error("Geolocation not supported"));
      return;
    }
    navigator.geolocation.getCurrentPosition(resolve, reject, { enableHighAccuracy: true, timeout: 15000 });
  });
}

export default function Attendance() {
  const { t } = useLanguage();
  const [history, setHistory] = useState<AttendanceRecord[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const [pendingCoords, setPendingCoords] = useState<{ lat: number; lng: number } | null>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);

  const hasOpenAttendance = history.some((h) => !h.checkOutAtUtc);

  async function load() {
    const res = await AttendanceApi.getMine();
    setHistory(res.data);
  }

  useEffect(() => {
    load();
    return () => stopCamera();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function stopCamera() {
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
  }

  async function startFlow(action: "checkin" | "checkout") {
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      const pos = await getPosition();
      const coords = { lat: pos.coords.latitude, lng: pos.coords.longitude };
      setPendingCoords(coords);
      setPendingAction(action);

      const stream = await navigator.mediaDevices?.getUserMedia({ video: { facingMode: "user" } }).catch(() => null);
      if (stream && videoRef.current) {
        streamRef.current = stream;
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
        setBusy(false);
      } else {
        await submit(action, coords, null);
      }
    } catch {
      setError(t.hr.locationDenied);
      setBusy(false);
    }
  }

  async function capture() {
    if (!videoRef.current || !pendingAction || !pendingCoords) return;
    setBusy(true);
    const canvas = document.createElement("canvas");
    canvas.width = videoRef.current.videoWidth;
    canvas.height = videoRef.current.videoHeight;
    canvas.getContext("2d")?.drawImage(videoRef.current, 0, 0);
    stopCamera();

    canvas.toBlob(
      async (blob) => {
        await submit(pendingAction, pendingCoords, blob);
      },
      "image/jpeg",
      0.85
    );
  }

  async function skipPhoto() {
    if (!pendingAction || !pendingCoords) return;
    stopCamera();
    await submit(pendingAction, pendingCoords, null);
  }

  async function submit(action: "checkin" | "checkout", coords: { lat: number; lng: number }, selfie: Blob | null) {
    setBusy(true);
    try {
      const res = action === "checkin" ? await AttendanceApi.checkIn(coords.lat, coords.lng, selfie) : await AttendanceApi.checkOut(coords.lat, coords.lng, selfie);
      setMessage(
        `${action === "checkin" ? t.hr.checkIn : t.hr.checkOut} — ${
          (action === "checkin" ? res.data.checkInWithinGeofence : res.data.checkOutWithinGeofence) ? t.hr.withinGeofence : t.hr.outsideGeofence
        }`
      );
      setPendingAction(null);
      setPendingCoords(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  function faceLabel(verified: boolean | null) {
    if (verified === null) return t.hr.faceNotChecked;
    return verified ? t.hr.faceVerified : t.hr.faceNotVerified;
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.attendanceTitle}</h1>
      </div>
      <p className="text-muted">{t.hr.attendanceIntro}</p>

      {error && <div className="alert-error">{error}</div>}
      {message && (
        <div className="card" style={{ borderColor: "var(--color-success)" }}>
          <strong className="text-success">{message}</strong>
        </div>
      )}

      <div className="card">
        {pendingAction ? (
          <div>
            <video ref={videoRef} muted playsInline style={{ width: "100%", maxWidth: 360, borderRadius: 8 }} />
            <div style={{ display: "flex", gap: 10, marginTop: 12 }}>
              <button className="btn" onClick={capture} disabled={busy}>
                {busy ? t.common.loading : (pendingAction === "checkin" ? t.hr.checkIn : t.hr.checkOut)}
              </button>
              <button className="btn btn-secondary" onClick={skipPhoto} disabled={busy}>
                {t.hr.skipPhoto}
              </button>
            </div>
          </div>
        ) : (
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={() => startFlow("checkin")} disabled={busy || hasOpenAttendance}>
              {busy && !pendingAction ? t.hr.locatingYou : t.hr.checkIn}
            </button>
            <button className="btn btn-secondary" onClick={() => startFlow("checkout")} disabled={busy || !hasOpenAttendance}>
              {t.hr.checkOut}
            </button>
          </div>
        )}
        {hasOpenAttendance && !pendingAction && <p className="text-muted" style={{ marginTop: 10, marginBottom: 0 }}>{t.hr.openAttendanceWarning}</p>}
      </div>

      <div className="card">
        <h3 style={{ marginTop: 0 }}>{t.hr.myAttendanceHistory}</h3>
        <table>
          <thead>
            <tr>
              <th>{t.hr.checkIn}</th>
              <th>{t.hr.withinGeofence}</th>
              <th>{t.hr.checkOut}</th>
              <th>{t.hr.withinGeofence}</th>
              <th>{t.hr.faceVerified}</th>
            </tr>
          </thead>
          <tbody>
            {history.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {history.map((h) => (
              <tr key={h.id}>
                <td>{new Date(h.checkInAtUtc).toLocaleString()}</td>
                <td>
                  <span className={`badge ${h.checkInWithinGeofence ? "badge-posted" : "badge-draft"}`}>
                    {h.checkInWithinGeofence ? t.hr.withinGeofence : t.hr.outsideGeofence}
                  </span>
                </td>
                <td>{h.checkOutAtUtc ? new Date(h.checkOutAtUtc).toLocaleString() : "-"}</td>
                <td>
                  {h.checkOutAtUtc && (
                    <span className={`badge ${h.checkOutWithinGeofence ? "badge-posted" : "badge-draft"}`}>
                      {h.checkOutWithinGeofence ? t.hr.withinGeofence : t.hr.outsideGeofence}
                    </span>
                  )}
                </td>
                <td>{faceLabel(h.checkInFaceVerified)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
