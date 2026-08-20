import { useEffect, useState } from "react";
import { DepartmentsApi } from "../../api/services";
import type { Department } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function Departments() {
  const [departments, setDepartments] = useState<Department[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [parentDepartmentId, setParentDepartmentId] = useState("");

  async function load() {
    const res = await DepartmentsApi.getAll();
    setDepartments(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await DepartmentsApi.create({ code, nameAr, nameEn, parentDepartmentId: parentDepartmentId || null });
      setShowForm(false);
      setCode("");
      setNameAr("");
      setNameEn("");
      setParentDepartmentId("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await DepartmentsApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>الأقسام</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "إلغاء" : "+ قسم جديد"}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>كود القسم</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالعربي</label>
                <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالإنجليزي</label>
                <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>القسم الأب (اختياري)</label>
                <select value={parentDepartmentId} onChange={(e) => setParentDepartmentId(e.target.value)}>
                  <option value="">بدون</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.code} - {d.nameAr}
                    </option>
                  ))}
                </select>
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
              <th>الاسم بالعربي</th>
              <th>الاسم بالإنجليزي</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {departments.map((d) => (
              <tr key={d.id}>
                <td>{d.code}</td>
                <td>{d.nameAr}</td>
                <td>{d.nameEn}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(d.id)}>
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
