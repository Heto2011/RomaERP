import { useEffect, useRef, useState } from "react";
import { DepartmentsApi, EmployeesApi, PositionsApi, SalaryComponentsApi, WorkLocationsApi } from "../../api/services";
import {
  CalculationType,
  EmploymentStatus,
  Gender,
  MaritalStatus,
  SalaryComponentType,
  type Department,
  type Employee,
  type EmployeeSalaryComponentAssignment,
  type Position,
  type SalaryComponent,
  type WorkLocation,
} from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

export default function Employees() {
  const { t, lang } = useLanguage();
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [positions, setPositions] = useState<Position[]>([]);
  const [workLocations, setWorkLocations] = useState<WorkLocation[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const facePhotoInputRef = useRef<HTMLInputElement>(null);
  const [facePhotoEmployeeId, setFacePhotoEmployeeId] = useState<string | null>(null);

  const [employeeCode, setEmployeeCode] = useState("");
  const [fullNameAr, setFullNameAr] = useState("");
  const [fullNameEn, setFullNameEn] = useState("");
  const [gender, setGender] = useState(Gender.Male);
  const [maritalStatus, setMaritalStatus] = useState(MaritalStatus.Single);
  const [hireDate, setHireDate] = useState(new Date().toISOString().slice(0, 10));
  const [departmentId, setDepartmentId] = useState("");
  const [positionId, setPositionId] = useState("");
  const [basicSalary, setBasicSalary] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [workLocationId, setWorkLocationId] = useState("");
  const [isSaudiNational, setIsSaudiNational] = useState(false);
  const [annualLeaveDaysPerYear, setAnnualLeaveDaysPerYear] = useState("21");
  const [employmentStatus, setEmploymentStatus] = useState(EmploymentStatus.Active);
  const [terminationDate, setTerminationDate] = useState("");

  const [allComponents, setAllComponents] = useState<SalaryComponent[]>([]);
  const [componentsEmployee, setComponentsEmployee] = useState<Employee | null>(null);
  const [assignedComponents, setAssignedComponents] = useState<EmployeeSalaryComponentAssignment[]>([]);
  const [newComponentId, setNewComponentId] = useState("");
  const [newComponentValue, setNewComponentValue] = useState("");
  const [componentsError, setComponentsError] = useState<string | null>(null);

  async function load() {
    const [empRes, depRes, posRes, compRes, locRes] = await Promise.all([
      EmployeesApi.getAll(),
      DepartmentsApi.getAll(),
      PositionsApi.getAll(),
      SalaryComponentsApi.getAll(),
      WorkLocationsApi.getAll(),
    ]);
    setEmployees(empRes.data);
    setDepartments(depRes.data);
    setPositions(posRes.data);
    setAllComponents(compRes.data);
    setWorkLocations(locRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  const filteredPositions = positions.filter((p) => p.departmentId === departmentId);

  function resetForm() {
    setEditingId(null);
    setEmployeeCode("");
    setFullNameAr("");
    setFullNameEn("");
    setGender(Gender.Male);
    setMaritalStatus(MaritalStatus.Single);
    setHireDate(new Date().toISOString().slice(0, 10));
    setDepartmentId("");
    setPositionId("");
    setBasicSalary("");
    setEmail("");
    setPhone("");
    setWorkLocationId("");
    setIsSaudiNational(false);
    setAnnualLeaveDaysPerYear("21");
    setEmploymentStatus(EmploymentStatus.Active);
    setTerminationDate("");
  }

  function startCreate() {
    resetForm();
    setShowForm(true);
  }

  function closeForm() {
    setShowForm(false);
    resetForm();
  }

  function startEdit(emp: Employee) {
    setEditingId(emp.id);
    setEmployeeCode(emp.employeeCode);
    setFullNameAr(emp.fullNameAr);
    setFullNameEn(emp.fullNameEn);
    setGender(emp.gender);
    setMaritalStatus(emp.maritalStatus);
    setHireDate(emp.hireDate.slice(0, 10));
    setDepartmentId(emp.departmentId);
    setPositionId(emp.positionId);
    setBasicSalary(String(emp.basicSalary));
    setEmail(emp.email ?? "");
    setPhone(emp.phone ?? "");
    setWorkLocationId(emp.workLocationId ?? "");
    setIsSaudiNational(emp.isSaudiNational);
    setAnnualLeaveDaysPerYear(String(emp.annualLeaveDaysPerYear));
    setEmploymentStatus(emp.employmentStatus);
    setTerminationDate(emp.terminationDate ? emp.terminationDate.slice(0, 10) : "");
    setShowForm(true);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const payload = {
        employeeCode,
        fullNameAr,
        fullNameEn,
        gender,
        maritalStatus,
        hireDate,
        departmentId,
        positionId,
        basicSalary: Number(basicSalary) || 0,
        email: email || null,
        phone: phone || null,
        workLocationId: workLocationId || null,
        isSaudiNational,
        annualLeaveDaysPerYear: Number(annualLeaveDaysPerYear) || 21,
      };
      if (editingId) {
        await EmployeesApi.update(editingId, { ...payload, employmentStatus, terminationDate: terminationDate || null });
      } else {
        await EmployeesApi.create(payload);
      }
      closeForm();
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await EmployeesApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function startFacePhotoUpload(employeeId: string) {
    setFacePhotoEmployeeId(employeeId);
    facePhotoInputRef.current?.click();
  }

  async function handleFacePhotoSelected(file: File) {
    if (!facePhotoEmployeeId) return;
    setError(null);
    try {
      await EmployeesApi.uploadFacePhoto(facePhotoEmployeeId, file);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setFacePhotoEmployeeId(null);
    }
  }

  async function openComponentsModal(emp: Employee) {
    setComponentsEmployee(emp);
    setComponentsError(null);
    setNewComponentId("");
    setNewComponentValue("");
    const res = await SalaryComponentsApi.getForEmployee(emp.id);
    setAssignedComponents(res.data);
  }

  async function handleAssignComponent(e: React.FormEvent) {
    e.preventDefault();
    if (!componentsEmployee || !newComponentId) return;
    setComponentsError(null);
    try {
      await SalaryComponentsApi.assign(componentsEmployee.id, newComponentId, Number(newComponentValue) || 0);
      const res = await SalaryComponentsApi.getForEmployee(componentsEmployee.id);
      setAssignedComponents(res.data);
      setNewComponentId("");
      setNewComponentValue("");
    } catch (err) {
      setComponentsError(getErrorMessage(err));
    }
  }

  async function handleRemoveComponent(salaryComponentId: string) {
    if (!componentsEmployee) return;
    setComponentsError(null);
    try {
      await SalaryComponentsApi.remove(componentsEmployee.id, salaryComponentId);
      const res = await SalaryComponentsApi.getForEmployee(componentsEmployee.id);
      setAssignedComponents(res.data);
    } catch (err) {
      setComponentsError(getErrorMessage(err));
    }
  }

  const employmentStatusLabel: Record<EmploymentStatus, string> = {
    [EmploymentStatus.Active]: t.hr.employmentStatusActive,
    [EmploymentStatus.OnLeave]: t.hr.employmentStatusOnLeave,
    [EmploymentStatus.Terminated]: t.hr.employmentStatusTerminated,
  };
  const employmentStatusBadgeClass: Record<EmploymentStatus, string> = {
    [EmploymentStatus.Active]: "badge-posted",
    [EmploymentStatus.OnLeave]: "badge-draft",
    [EmploymentStatus.Terminated]: "badge-reversed",
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.employeesTitle}</h1>
        <button className="btn" onClick={() => (showForm ? closeForm() : startCreate())}>
          {showForm ? t.common.cancel : t.hr.newEmployee}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.hr.employeeCode}</label>
                <input value={employeeCode} onChange={(e) => setEmployeeCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameAr}</label>
                <input value={fullNameAr} onChange={(e) => setFullNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameEn}</label>
                <input value={fullNameEn} onChange={(e) => setFullNameEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.genderLabel}</label>
                <select value={gender} onChange={(e) => setGender(Number(e.target.value))}>
                  <option value={Gender.Male}>{t.hr.male}</option>
                  <option value={Gender.Female}>{t.hr.female}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.maritalStatus}</label>
                <select value={maritalStatus} onChange={(e) => setMaritalStatus(Number(e.target.value))}>
                  <option value={MaritalStatus.Single}>{t.hr.single}</option>
                  <option value={MaritalStatus.Married}>{t.hr.married}</option>
                  <option value={MaritalStatus.Divorced}>{t.hr.divorced}</option>
                  <option value={MaritalStatus.Widowed}>{t.hr.widowed}</option>
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.hireDate}</label>
                <input type="date" value={hireDate} onChange={(e) => setHireDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.department}</label>
                <select
                  value={departmentId}
                  onChange={(e) => {
                    setDepartmentId(e.target.value);
                    setPositionId("");
                  }}
                  required
                >
                  <option value="">{t.hr.selectDepartment}</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.code} - {bilingualName(d.nameAr, d.nameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.position}</label>
                <select value={positionId} onChange={(e) => setPositionId(e.target.value)} required disabled={!departmentId}>
                  <option value="">{t.hr.selectPosition}</option>
                  {filteredPositions.map((p) => (
                    <option key={p.id} value={p.id}>
                      {bilingualName(p.titleAr, p.titleEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.basicSalary}</label>
                <input type="number" step="0.01" value={basicSalary} onChange={(e) => setBasicSalary(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.email}</label>
                <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.common.phone}</label>
                <input value={phone} onChange={(e) => setPhone(e.target.value)} />
              </div>
              <div className="form-field">
                <label>{t.hr.workLocation}</label>
                <select value={workLocationId} onChange={(e) => setWorkLocationId(e.target.value)}>
                  <option value="">{t.hr.noWorkLocation}</option>
                  {workLocations.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.hr.annualLeaveDaysPerYear}</label>
                <input type="number" min={0} value={annualLeaveDaysPerYear} onChange={(e) => setAnnualLeaveDaysPerYear(e.target.value)} />
              </div>
              <div className="form-field">
                <label>
                  <input type="checkbox" checked={isSaudiNational} onChange={(e) => setIsSaudiNational(e.target.checked)} style={{ marginInlineEnd: 6 }} />
                  {t.hr.isSaudiNational}
                </label>
              </div>
              {editingId && (
                <>
                  <div className="form-field">
                    <label>{t.hr.employmentStatus}</label>
                    <select value={employmentStatus} onChange={(e) => setEmploymentStatus(Number(e.target.value))}>
                      <option value={EmploymentStatus.Active}>{t.hr.employmentStatusActive}</option>
                      <option value={EmploymentStatus.OnLeave}>{t.hr.employmentStatusOnLeave}</option>
                      <option value={EmploymentStatus.Terminated}>{t.hr.employmentStatusTerminated}</option>
                    </select>
                  </div>
                  {employmentStatus === EmploymentStatus.Terminated && (
                    <div className="form-field">
                      <label>{t.hr.terminationDate}</label>
                      <input type="date" value={terminationDate} onChange={(e) => setTerminationDate(e.target.value)} />
                    </div>
                  )}
                </>
              )}
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
              <th>{t.common.code}</th>
              <th>{t.common.nameAr}</th>
              <th>{t.hr.department}</th>
              <th>{t.hr.position}</th>
              <th>{t.hr.basicSalary}</th>
              <th>{t.hr.employmentStatus}</th>
              <th>{t.hr.faceReferencePhoto}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {employees.map((emp) => (
              <tr key={emp.id}>
                <td>{emp.employeeCode}</td>
                <td>{bilingualName(emp.fullNameAr, emp.fullNameEn, lang)}</td>
                <td>{bilingualName(emp.departmentNameAr, emp.departmentNameEn, lang)}</td>
                <td>{bilingualName(emp.positionNameAr, emp.positionNameEn, lang)}</td>
                <td>{emp.basicSalary.toLocaleString()}</td>
                <td>
                  <span className={`badge ${employmentStatusBadgeClass[emp.employmentStatus]}`}>
                    {employmentStatusLabel[emp.employmentStatus]}
                  </span>
                </td>
                <td>
                  <span className={`badge ${emp.hasFaceReferencePhoto ? "badge-posted" : "badge-draft"}`}>
                    {emp.hasFaceReferencePhoto ? t.hr.faceReferencePhotoSet : t.hr.faceReferencePhotoNotSet}
                  </span>
                </td>
                <td style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => startEdit(emp)}>
                    {t.common.edit}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => startFacePhotoUpload(emp.id)}>
                    {t.hr.uploadFaceReferencePhoto}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => openComponentsModal(emp)}>
                    {t.hr.salaryComponentsButton}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(emp.id)}>
                    {t.common.delete}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <input
        ref={facePhotoInputRef}
        type="file"
        accept="image/jpeg"
        style={{ display: "none" }}
        onChange={(e) => e.target.files?.[0] && handleFacePhotoSelected(e.target.files[0])}
      />

      {componentsEmployee && (
        <div className="modal-overlay" onClick={() => setComponentsEmployee(null)}>
          <div className="card" style={{ maxWidth: 520, margin: "6% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{t.hr.assignToEmployee.replace("{name}", bilingualName(componentsEmployee.fullNameAr, componentsEmployee.fullNameEn, lang))}</h3>

            {componentsError && <div className="alert-error">{componentsError}</div>}

            {allComponents.length === 0 && <p className="text-muted">{t.hr.noComponentsYet}</p>}

            {allComponents.length > 0 && (
              <form onSubmit={handleAssignComponent} className="form-grid" style={{ marginBottom: 16 }}>
                <div className="form-field">
                  <label>{t.hr.salaryComponentsTitle}</label>
                  <select value={newComponentId} onChange={(e) => setNewComponentId(e.target.value)} required>
                    <option value="" disabled>-</option>
                    {allComponents.map((c) => (
                      <option key={c.id} value={c.id}>
                        {bilingualName(c.nameAr, c.nameEn, lang)} ({c.componentType === SalaryComponentType.Allowance ? t.hr.allowanceType : t.hr.deductionType})
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-field">
                  <label>{t.hr.valueLabel}</label>
                  <input type="number" step="0.01" value={newComponentValue} onChange={(e) => setNewComponentValue(e.target.value)} required />
                </div>
                <button className="btn" type="submit" style={{ alignSelf: "flex-end" }}>{t.hr.assign}</button>
              </form>
            )}

            <div className="text-muted" style={{ marginBottom: 8 }}>{t.hr.currentlyAssigned}</div>
            {assignedComponents.length === 0 && <p className="text-muted">{t.hr.noComponentsAssigned}</p>}
            {assignedComponents.length > 0 && (
              <table>
                <tbody>
                  {assignedComponents.map((ac) => (
                    <tr key={ac.salaryComponentId}>
                      <td>{bilingualName(ac.nameAr, ac.nameEn, lang)}</td>
                      <td>{ac.componentType === SalaryComponentType.Allowance ? t.hr.allowanceType : t.hr.deductionType}</td>
                      <td style={{ textAlign: "end" }}>
                        {ac.value.toLocaleString()}{ac.calculationType === CalculationType.PercentageOfBasic ? "%" : ""}
                      </td>
                      <td>
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => handleRemoveComponent(ac.salaryComponentId)}>
                          {t.common.delete}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            <button className="btn btn-secondary" type="button" style={{ marginTop: 14 }} onClick={() => setComponentsEmployee(null)}>
              {t.common.cancel}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
