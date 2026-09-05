import { useEffect, useState } from "react";
import { DepartmentsApi, EmployeesApi, PositionsApi, SalaryComponentsApi } from "../../api/services";
import {
  CalculationType,
  Gender,
  MaritalStatus,
  SalaryComponentType,
  type Department,
  type Employee,
  type EmployeeSalaryComponentAssignment,
  type Position,
  type SalaryComponent,
} from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

export default function Employees() {
  const { t, lang } = useLanguage();
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [positions, setPositions] = useState<Position[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  const [allComponents, setAllComponents] = useState<SalaryComponent[]>([]);
  const [componentsEmployee, setComponentsEmployee] = useState<Employee | null>(null);
  const [assignedComponents, setAssignedComponents] = useState<EmployeeSalaryComponentAssignment[]>([]);
  const [newComponentId, setNewComponentId] = useState("");
  const [newComponentValue, setNewComponentValue] = useState("");
  const [componentsError, setComponentsError] = useState<string | null>(null);

  async function load() {
    const [empRes, depRes, posRes, compRes] = await Promise.all([
      EmployeesApi.getAll(),
      DepartmentsApi.getAll(),
      PositionsApi.getAll(),
      SalaryComponentsApi.getAll(),
    ]);
    setEmployees(empRes.data);
    setDepartments(depRes.data);
    setPositions(posRes.data);
    setAllComponents(compRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  const filteredPositions = positions.filter((p) => p.departmentId === departmentId);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await EmployeesApi.create({
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
      });
      setShowForm(false);
      setEmployeeCode("");
      setFullNameAr("");
      setFullNameEn("");
      setDepartmentId("");
      setPositionId("");
      setBasicSalary("");
      setEmail("");
      setPhone("");
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

  return (
    <div>
      <div className="page-header">
        <h1>{t.hr.employeesTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
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
              <th></th>
            </tr>
          </thead>
          <tbody>
            {employees.map((emp) => (
              <tr key={emp.id}>
                <td>{emp.employeeCode}</td>
                <td>{bilingualName(emp.fullNameAr, emp.fullNameEn, lang)}</td>
                <td>{emp.departmentName}</td>
                <td>{emp.positionName}</td>
                <td>{emp.basicSalary.toLocaleString()}</td>
                <td style={{ display: "flex", gap: 8 }}>
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
