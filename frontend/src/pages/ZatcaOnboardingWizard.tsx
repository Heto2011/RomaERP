import { useEffect, useState } from "react";
import { ZatcaOnboardingApi } from "../api/services";
import { ZatcaOnboardingStage, type SaveZatcaOnboardingDetailsInput, type ZatcaOnboardingStatus } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

const emptyDetails = (): SaveZatcaOnboardingDetailsInput => ({
  organizationIdentifier: "",
  solutionName: "RomaERP",
  model: "SaaS",
  deviceSerialNumber: "",
  organizationUnitName: "",
  address: "",
  businessCategory: "",
  invoiceType: "1100",
});

export default function ZatcaOnboardingWizard() {
  const { t } = useLanguage();
  const [status, setStatus] = useState<ZatcaOnboardingStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [details, setDetails] = useState<SaveZatcaOnboardingDetailsInput>(emptyDetails());
  const [otp, setOtp] = useState("");

  async function load() {
    const res = await ZatcaOnboardingApi.getStatus();
    setStatus(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function run(action: () => Promise<{ data: ZatcaOnboardingStatus }>, successMessage: string) {
    setError(null);
    setSuccess(null);
    setBusy(true);
    try {
      const res = await action();
      setStatus(res.data);
      setSuccess(successMessage);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  const stage = status?.stage ?? ZatcaOnboardingStage.NotStarted;

  return (
    <div className="card" style={{ marginTop: 20 }}>
      <h2 style={{ marginTop: 0 }}>{t.eInvoicing.zatcaOnboardingTitle}</h2>
      <p className="text-muted" style={{ maxWidth: 720 }}>{t.eInvoicing.zatcaOnboardingIntro}</p>

      {error && <div className="alert-error">{error}</div>}
      {success && <div className="alert-success">{success}</div>}

      {/* Stage 1: organization details + CSR */}
      <div style={{ marginTop: 18, opacity: stage >= ZatcaOnboardingStage.NotStarted ? 1 : 0.5 }}>
        <h3>{t.eInvoicing.zatcaStage1}</h3>
        {stage < ZatcaOnboardingStage.CsrGenerated ? (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              run(() => ZatcaOnboardingApi.generateCsr(details), t.eInvoicing.zatcaCsrGenerated);
            }}
          >
            <div className="form-grid">
              <div className="form-field">
                <label>{t.eInvoicing.zatcaOrganizationIdentifier}</label>
                <input
                  value={details.organizationIdentifier}
                  onChange={(e) => setDetails((d) => ({ ...d, organizationIdentifier: e.target.value }))}
                  pattern="3\d{13}3"
                  title="15 digits, starts and ends with 3"
                  required
                />
              </div>
              <div className="form-field">
                <label>{t.eInvoicing.zatcaSolutionName}</label>
                <input value={details.solutionName} onChange={(e) => setDetails((d) => ({ ...d, solutionName: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.eInvoicing.zatcaModel}</label>
                <input value={details.model} onChange={(e) => setDetails((d) => ({ ...d, model: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.eInvoicing.zatcaDeviceSerialNumber}</label>
                <input value={details.deviceSerialNumber} onChange={(e) => setDetails((d) => ({ ...d, deviceSerialNumber: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.eInvoicing.zatcaOrganizationUnitName}</label>
                <input value={details.organizationUnitName} onChange={(e) => setDetails((d) => ({ ...d, organizationUnitName: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.eInvoicing.zatcaAddress}</label>
                <input value={details.address} onChange={(e) => setDetails((d) => ({ ...d, address: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.eInvoicing.zatcaBusinessCategory}</label>
                <input value={details.businessCategory} onChange={(e) => setDetails((d) => ({ ...d, businessCategory: e.target.value }))} required />
              </div>
              <div className="form-field">
                <label>{t.eInvoicing.zatcaInvoiceType}</label>
                <select value={details.invoiceType} onChange={(e) => setDetails((d) => ({ ...d, invoiceType: e.target.value }))}>
                  <option value="1100">{t.eInvoicing.zatcaInvoiceTypeBoth}</option>
                  <option value="1000">{t.eInvoicing.zatcaInvoiceTypeStandardOnly}</option>
                  <option value="0100">{t.eInvoicing.zatcaInvoiceTypeSimplifiedOnly}</option>
                </select>
              </div>
            </div>
            <button className="btn" type="submit" disabled={busy} style={{ marginTop: 14 }}>
              {t.eInvoicing.zatcaGenerateCsr}
            </button>
          </form>
        ) : (
          <div className="text-success">✓ {t.eInvoicing.zatcaCsrGenerated}</div>
        )}
      </div>

      {/* Stage 2: OTP + Compliance CSID */}
      <div style={{ marginTop: 18, opacity: stage >= ZatcaOnboardingStage.CsrGenerated ? 1 : 0.5 }}>
        <h3>{t.eInvoicing.zatcaStage2}</h3>
        {stage < ZatcaOnboardingStage.CsrGenerated ? (
          <div className="text-muted">{t.eInvoicing.zatcaStageNotReachedYet}</div>
        ) : stage < ZatcaOnboardingStage.ComplianceCsidObtained ? (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              run(() => ZatcaOnboardingApi.requestComplianceCsid(otp), t.eInvoicing.zatcaComplianceCsidObtained);
            }}
          >
            <p className="text-muted" style={{ maxWidth: 600 }}>{t.eInvoicing.zatcaOtpIntro}</p>
            <div className="form-field" style={{ maxWidth: 240 }}>
              <label>{t.eInvoicing.zatcaOtp}</label>
              <input value={otp} onChange={(e) => setOtp(e.target.value)} required />
            </div>
            <button className="btn" type="submit" disabled={busy} style={{ marginTop: 10 }}>
              {t.eInvoicing.zatcaRequestComplianceCsid}
            </button>
          </form>
        ) : (
          <div className="text-success">✓ {t.eInvoicing.zatcaComplianceCsidObtained}</div>
        )}
      </div>

      {/* Stage 3: Compliance checks */}
      <div style={{ marginTop: 18, opacity: stage >= ZatcaOnboardingStage.ComplianceCsidObtained ? 1 : 0.5 }}>
        <h3>{t.eInvoicing.zatcaStage3}</h3>
        {stage < ZatcaOnboardingStage.ComplianceCsidObtained ? (
          <div className="text-muted">{t.eInvoicing.zatcaStageNotReachedYet}</div>
        ) : stage < ZatcaOnboardingStage.ComplianceChecksPassed ? (
          <div>
            <p className="text-muted" style={{ maxWidth: 600 }}>{t.eInvoicing.zatcaComplianceChecksNote}</p>
            <button className="btn" disabled={busy} onClick={() => run(() => ZatcaOnboardingApi.runComplianceChecks(), t.eInvoicing.zatcaComplianceChecksPassed)}>
              {t.eInvoicing.zatcaRunComplianceChecks}
            </button>
          </div>
        ) : (
          <div className="text-success">✓ {t.eInvoicing.zatcaComplianceChecksPassed}</div>
        )}
      </div>

      {/* Stage 4: Production CSID */}
      <div style={{ marginTop: 18, opacity: stage >= ZatcaOnboardingStage.ComplianceChecksPassed ? 1 : 0.5 }}>
        <h3>{t.eInvoicing.zatcaStage4}</h3>
        {stage < ZatcaOnboardingStage.ComplianceChecksPassed ? (
          <div className="text-muted">{t.eInvoicing.zatcaStageNotReachedYet}</div>
        ) : stage < ZatcaOnboardingStage.ProductionCsidObtained ? (
          <button className="btn" disabled={busy} onClick={() => run(() => ZatcaOnboardingApi.requestProductionCsid(), t.eInvoicing.zatcaProductionCsidObtained)}>
            {t.eInvoicing.zatcaRequestProductionCsid}
          </button>
        ) : (
          <div className="text-success">✓ {t.eInvoicing.zatcaProductionCsidObtained}</div>
        )}
      </div>
    </div>
  );
}
