import { useEffect, useState } from "react";
import { DepartmentsApi, EmployeesApi, PositionsApi } from "../../api/services";
import { Gender, MaritalStatus, type Department, type Employee, type Position } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function Employees() {
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

  async function load() {
    const [empRes, depRes, posRes] = await Promise.all([
      EmployeesApi.getAll(),
      DepartmentsApi.getAll(),
      PositionsApi.getAll(),
    ]);
    setEmployees(empRes.data);
    setDepartments(depRes.data);
    setPositions(posRes.data);
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

  return (
    <div>
      <div className="page-header">
        <h1>الموظفون</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "إلغاء" : "+ موظف جديد"}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>كود الموظف</label>
                <input value={employeeCode} onChange={(e) => setEmployeeCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالعربي</label>
                <input value={fullNameAr} onChange={(e) => setFullNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالإنجليزي</label>
                <input value={fullNameEn} onChange={(e) => setFullNameEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>النوع</label>
                <select value={gender} onChange={(e) => setGender(Number(e.target.value))}>
                  <option value={Gender.Male}>ذكر</option>
                  <option value={Gender.Female}>أنثى</option>
                </select>
              </div>
              <div className="form-field">
                <label>الحالة الاجتماعية</label>
                <select value={maritalStatus} onChange={(e) => setMaritalStatus(Number(e.target.value))}>
                  <option value={MaritalStatus.Single}>أعزب</option>
                  <option value={MaritalStatus.Married}>متزوج</option>
                  <option value={MaritalStatus.Divorced}>مطلق</option>
                  <option value={MaritalStatus.Widowed}>أرمل</option>
                </select>
              </div>
              <div className="form-field">
                <label>تاريخ التعيين</label>
                <input type="date" value={hireDate} onChange={(e) => setHireDate(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>القسم</label>
                <select
                  value={departmentId}
                  onChange={(e) => {
                    setDepartmentId(e.target.value);
                    setPositionId("");
                  }}
                  required
                >
                  <option value="">اختر القسم</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.code} - {d.nameAr}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>الوظيفة</label>
                <select value={positionId} onChange={(e) => setPositionId(e.target.value)} required disabled={!departmentId}>
                  <option value="">اختر الوظيفة</option>
                  {filteredPositions.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.titleAr}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>الراتب الأساسي</label>
                <input type="number" step="0.01" value={basicSalary} onChange={(e) => setBasicSalary(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>البريد الإلكتروني</label>
                <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
              </div>
              <div className="form-field">
                <label>الهاتف</label>
                <input value={phone} onChange={(e) => setPhone(e.target.value)} />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              حفظ
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>الكود</th>
              <th>الاسم</th>
              <th>القسم</th>
              <th>الوظيفة</th>
              <th>الراتب الأساسي</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {employees.map((emp) => (
              <tr key={emp.id}>
                <td>{emp.employeeCode}</td>
                <td>{emp.fullNameAr}</td>
                <td>{emp.departmentName}</td>
                <td>{emp.positionName}</td>
                <td>{emp.basicSalary.toLocaleString()}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(emp.id)}>
                    حذف
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
