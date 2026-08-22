import { useEffect, useState } from "react";
import { DepartmentsApi, PositionsApi } from "../../api/services";
import type { Department, Position } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";

export default function Positions() {
  const { t } = useLanguage();
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
        <h1>{t.hr.positionsTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.hr.newPosition}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.hr.positionCode}</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.titleAr}</label>
                <input value={titleAr} onChange={(e) => setTitleAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.titleEn}</label>
                <input value={titleEn} onChange={(e) => setTitleEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.department}</label>
                <select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)} required>
                  <option value="">{t.hr.selectDepartment}</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.code} - {d.nameAr}
                    </option>
                  ))}
                </select>
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
              <th>{t.hr.jobTitle}</th>
              <th>{t.hr.department}</th>
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
