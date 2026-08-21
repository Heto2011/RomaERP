import { useEffect, useState } from "react";
import { WarehousesApi } from "../../api/services";
import type { Warehouse } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function Warehouses() {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");

  async function load() {
    const res = await WarehousesApi.getAll();
    setWarehouses(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await WarehousesApi.create({ code, nameAr, nameEn });
      setShowForm(false);
      setCode("");
      setNameAr("");
      setNameEn("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await WarehousesApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>المخازن</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? "إلغاء" : "+ مخزن جديد"}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>كود المخزن</label>
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
            {warehouses.map((w) => (
              <tr key={w.id}>
                <td>{w.code}</td>
                <td>{w.nameAr}</td>
                <td>{w.nameEn}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(w.id)}>
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
