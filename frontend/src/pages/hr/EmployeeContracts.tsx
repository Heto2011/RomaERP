import { useEffect, useState } from "react";
import { EmployeeContractsApi, EmployeesApi } from "../../api/services";
import { ContractType, EmployeeContractStatus, type Employee, type EmployeeContract } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

const emptyForm = { employeeId: "", contractType: ContractType.Permanent, startDate: new Date().toISOString().slice(0, 10), endDate: "", notes: "" };

export default function EmployeeContracts() {
  const { t, lang } = useLanguage();
  const [contracts, setContracts] = useState<EmployeeContract[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const [contractsRes, employeesRes] = await Promise.all([EmployeeContractsApi.getAll(), EmployeesApi.getAll()]);
    setContracts(contractsRes.data);
    setEmployees(employeesRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function employeeName(id: string) {
    const emp = employees.find((e) => e.id === id);
    return emp ? bilingualName(emp.fullNameAr, emp.fullNameEn, lang) : "";
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await EmployeeContractsApi.create({
        employeeId: form.employeeId,
        contractType: form.contractType,
        startDate: form.startDate,
        endDate: form.contractType === ContractType.Permanent ? null : form.endDate || null,
        notes: form.notes || null,
      });
      setShowForm(false);
      setForm(emptyForm);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleTerminate(id: string) {
    if (!window.confirm(t.hr.confirmTerminateContract)) return;
    setError(null);
    try {
      await EmployeeContractsApi.updateStatus(id, EmployeeContractStatus.Terminated);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    if (!window.confirm(t.hr.confirmDeleteContract)) return;
    setError(null);
    try {
      await EmployeeContractsApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  const contractTypeLabel: Record<ContractType, string> = {
    [ContractType.Permanent]: t.hr.contractTypePermanent,
    [ContractType.FixedTerm]: t.hr.contractTypeFixedTerm,
    [ContractType.PartTime]: t.hr.contractTypePartTime,
  };

  const statusLabel: Record<EmployeeContractStatus, string> = {
    [EmployeeContractStatus.Active]: t.hr.contractStatusActive,
    [EmployeeContractStatus.Renewed]: t.hr.contractStatusRenewed,
    [EmployeeContractStatus.Terminated]: t.hr.contractStatusTerminated,
  };

  const statusBadgeClass: Record<EmployeeContractStatus, string> = {
    [EmployeeContractStatus.Active]: "badge-posted",
    [EmployeeContractStatus.Renewed]: "badge-draft",
    [EmployeeContractStatus.Terminated]: "badge-reversed",
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.employeeContractsTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.hr.newEmployeeContract}
        </button>
      </div>
      <p className="text-muted">{t.hr.employeeContractsIntro}</p>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.nav.employees}</label>
                <select value={form.employeeId} onChange={(e) => setForm((f) => ({ ...f, employeeId: e.target.value }))} required>
                  <option value="">-</option>
                  {employees.map((emp) => (
                    <option key={emp.id} value={emp.id}>
                      {emp.employeeCode} - {bilingualName(emp.fullNameAr, emp.fullNameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.contractType}</label>
                <select
                  value={form.contractType}
                  onChange={(e) => setForm((f) => ({ ...f, contractType: Number(e.target.value) as ContractType }))}
                >
                  <option value={ContractType.Permanent}>{t.hr.contractTypePermanent}</option>
                  <option value={ContractType.FixedTerm}>{t.hr.contractTypeFixedTerm}</option>
                  <option value={ContractType.PartTime}>{t.hr.contractTypePartTime}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.dateFrom}</label>
                <input type="date" value={form.startDate} onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))} required />
              </div>
              {form.contractType !== ContractType.Permanent && (
                <div className="form-field">
                  <label>{t.hr.terminationDate}</label>
                  <input type="date" value={form.endDate} onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))} required />
                </div>
              )}
              <div className="form-field" style={{ gridColumn: "1 / -1" }}>
                <label>{t.hr.reason}</label>
                <input value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} />
              </div>
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
              <th>{t.nav.employees}</th>
              <th>{t.hr.contractType}</th>
              <th>{t.hr.dateFrom}</th>
              <th>{t.hr.terminationDate}</th>
              <th>{t.common.status}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {contracts.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {contracts.map((c) => (
              <tr key={c.id}>
                <td>{c.employeeName || employeeName(c.employeeId)}</td>
                <td>{contractTypeLabel[c.contractType]}</td>
                <td>{new Date(c.startDate).toLocaleDateString()}</td>
                <td>
                  {c.endDate ? new Date(c.endDate).toLocaleDateString() : "-"}
                  {c.status === EmployeeContractStatus.Active && c.daysUntilExpiry !== null && (
                    <div className={`text-muted`} style={{ fontSize: 12, color: c.daysUntilExpiry < 0 ? "var(--color-danger)" : c.daysUntilExpiry <= 30 ? "var(--color-warning, #c78a00)" : undefined }}>
                      {c.daysUntilExpiry < 0
                        ? t.hr.contractOverdue
                        : `${t.hr.contractDaysLeft} ${c.daysUntilExpiry}`}
                    </div>
                  )}
                </td>
                <td>
                  <span className={`badge ${statusBadgeClass[c.status]}`}>{statusLabel[c.status]}</span>
                </td>
                <td style={{ display: "flex", gap: 8 }}>
                  {c.status === EmployeeContractStatus.Active && (
                    <button className="btn btn-secondary btn-sm" onClick={() => handleTerminate(c.id)}>
                      {t.hr.terminateContract}
                    </button>
                  )}
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(c.id)}>
                    {t.common.delete}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
