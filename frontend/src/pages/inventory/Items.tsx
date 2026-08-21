import { useEffect, useState } from "react";
import { ItemCategoriesApi, ItemsApi } from "../../api/services";
import type { Item, ItemCategory } from "../../api/types";
import { getErrorMessage } from "../../api/client";

export default function Items() {
  const [items, setItems] = useState<Item[]>([]);
  const [categories, setCategories] = useState<ItemCategory[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [showCategoryForm, setShowCategoryForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [unitOfMeasure, setUnitOfMeasure] = useState("");
  const [itemCategoryId, setItemCategoryId] = useState("");
  const [reorderLevel, setReorderLevel] = useState("0");

  const [categoryCode, setCategoryCode] = useState("");
  const [categoryNameAr, setCategoryNameAr] = useState("");
  const [categoryNameEn, setCategoryNameEn] = useState("");

  async function load() {
    const [itemsRes, categoriesRes] = await Promise.all([ItemsApi.getAll(), ItemCategoriesApi.getAll()]);
    setItems(itemsRes.data);
    setCategories(categoriesRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function categoryName(id: string) {
    return categories.find((c) => c.id === id)?.nameAr ?? "";
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await ItemsApi.create({
        code,
        nameAr,
        nameEn,
        unitOfMeasure,
        itemCategoryId,
        reorderLevel: Number(reorderLevel) || 0,
      });
      setShowForm(false);
      setCode("");
      setNameAr("");
      setNameEn("");
      setUnitOfMeasure("");
      setItemCategoryId("");
      setReorderLevel("0");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleCategorySubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await ItemCategoriesApi.create({ code: categoryCode, nameAr: categoryNameAr, nameEn: categoryNameEn });
      setShowCategoryForm(false);
      setCategoryCode("");
      setCategoryNameAr("");
      setCategoryNameEn("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    try {
      await ItemsApi.remove(id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>الأصناف</h1>
        <div style={{ display: "flex", gap: 10 }}>
          <button className="btn btn-secondary" onClick={() => setShowCategoryForm((v) => !v)}>
            {showCategoryForm ? "إلغاء" : "+ تصنيف"}
          </button>
          <button className="btn" onClick={() => setShowForm((v) => !v)}>
            {showForm ? "إلغاء" : "+ صنف جديد"}
          </button>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showCategoryForm && (
        <div className="card">
          <form onSubmit={handleCategorySubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>كود التصنيف</label>
                <input value={categoryCode} onChange={(e) => setCategoryCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالعربي</label>
                <input value={categoryNameAr} onChange={(e) => setCategoryNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>الاسم بالإنجليزي</label>
                <input value={categoryNameEn} onChange={(e) => setCategoryNameEn(e.target.value)} required />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              حفظ التصنيف
            </button>
          </form>
        </div>
      )}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>كود الصنف</label>
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
                <label>وحدة القياس</label>
                <input value={unitOfMeasure} onChange={(e) => setUnitOfMeasure(e.target.value)} placeholder="مثال: قطعة، كجم" required />
              </div>
              <div className="form-field">
                <label>التصنيف</label>
                <select value={itemCategoryId} onChange={(e) => setItemCategoryId(e.target.value)} required>
                  <option value="">اختر التصنيف</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} - {c.nameAr}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>حد إعادة الطلب</label>
                <input type="number" step="0.01" value={reorderLevel} onChange={(e) => setReorderLevel(e.target.value)} />
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
              <th>التصنيف</th>
              <th>الوحدة</th>
              <th>الرصيد الحالي</th>
              <th>متوسط التكلفة</th>
              <th>حد إعادة الطلب</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>{item.code}</td>
                <td>{item.nameAr}</td>
                <td>{item.itemCategoryName || categoryName(item.itemCategoryId)}</td>
                <td>{item.unitOfMeasure}</td>
                <td className={item.quantityOnHand <= item.reorderLevel ? "text-danger" : undefined}>
                  {item.quantityOnHand.toLocaleString()}
                </td>
                <td>{item.averageCost.toLocaleString()}</td>
                <td>{item.reorderLevel.toLocaleString()}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(item.id)}>
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
