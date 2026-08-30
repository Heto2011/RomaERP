import { useEffect, useState } from "react";
import { DepartmentsApi } from "../../api/services";
import type { Department } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

export default function Departments() {
  const { t, lang } = useLanguage();
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
        <h1>{t.hr.departmentsTitle}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.hr.newDepartment}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.hr.departmentCode}</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameAr}</label>
                <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameEn}</label>
                <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.hr.parentDepartment}</label>
                <select value={parentDepartmentId} onChange={(e) => setParentDepartmentId(e.target.value)}>
                  <option value="">{t.common.none}</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.code} - {bilingualName(d.nameAr, d.nameEn, lang)}
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
              <th>{t.common.nameAr}</th>
              <th>{t.common.nameEn}</th>
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
