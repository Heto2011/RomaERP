import { useEffect, useState } from "react";
import { DepartmentsApi, PositionsApi } from "../../api/services";
import type { Department, Position } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function Positions() {
  const [positions, setPositions] = useState<Position[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [titleAr, setTitleAr] = useState("");
  const [titleEn, setTitleEn] = useState("");
  const [departmentId, setDepartmentId] = useState("");

  async function load() {
    const [posRes, depRes] = await Promise.all([PositionsApi.getAll(), DepartmentsApi.getAll()]);
    setPositions(posRes.data);
    setDepartments(depRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function departmentName(id: string) {
    return departments.find((d) => d.id === id)?.nameAr ?? "";
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await PositionsApi.create({ code, titleAr, titleEn, departmentId });
      setShowForm(false);
      setCode("");
      setTitleAr("");
      setTitleEn("");
      setDepartmentId("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await PositionsApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>الوظائف</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "إلغاء" : "+ وظيفة جديدة"}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>كود الوظيفة</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>المسمى بالعربي</label>
                <input value={titleAr} onChange={(e) => setTitleAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>المسمى بالإنجليزي</label>
                <input value={titleEn} onChange={(e) => setTitleEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>القسم</label>
                <select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)} required>
                  <option value="">اختر القسم</option>
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
              <th>المسمى الوظيفي</th>
              <th>القسم</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {positions.map((p) => (
              <tr key={p.id}>
                <td>{p.code}</td>
                <td>{p.titleAr}</td>
                <td>{departmentName(p.departmentId)}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(p.id)}>
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
