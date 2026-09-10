import { useEffect, useRef, useState } from "react";
import { ItemCategoriesApi, ItemsApi } from "../../api/services";
import type { Item, ItemCategory } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";
import QuickAddBulk from "../../components/QuickAddBulk";
import { makeSequentialCodeGenerator } from "../../utils/sequentialCode";

export default function Items() {
  const { t, lang } = useLanguage();
  const [items, setItems] = useState<Item[]>([]);
  const [categories, setCategories] = useState<ItemCategory[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [showCategoryForm, setShowCategoryForm] = useState(false);
  const [showQuickAdd, setShowQuickAdd] = useState(false);
  const [quickAddCategoryId, setQuickAddCategoryId] = useState("");
  const [quickAddUnit, setQuickAddUnit] = useState("");
  const codeGenRef = useRef<() => string>(() => "");
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [unitOfMeasure, setUnitOfMeasure] = useState("");
  const [itemCategoryId, setItemCategoryId] = useState("");
  const [reorderLevel, setReorderLevel] = useState("0");
  const [isLotTracked, setIsLotTracked] = useState(false);

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
    const cat = categories.find((c) => c.id === id);
    return cat ? bilingualName(cat.nameAr, cat.nameEn, lang) : "";
  }

  function resetForm() {
    setShowForm(false);
    setEditingId(null);
    setCode("");
    setNameAr("");
    setNameEn("");
    setUnitOfMeasure("");
    setItemCategoryId("");
    setReorderLevel("0");
    setIsLotTracked(false);
  }

  function startEdit(item: Item) {
    setEditingId(item.id);
    setCode(item.code);
    setNameAr(item.nameAr);
    setNameEn(item.nameEn);
    setUnitOfMeasure(item.unitOfMeasure);
    setItemCategoryId(item.itemCategoryId);
    setReorderLevel(String(item.reorderLevel));
    setIsLotTracked(item.isLotTracked);
    setShowForm(true);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const payload = { nameAr, nameEn, unitOfMeasure, itemCategoryId, reorderLevel: Number(reorderLevel) || 0, isLotTracked };
      if (editingId) await ItemsApi.update(editingId, payload);
      else await ItemsApi.create({ ...payload, code });
      resetForm();
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
        <h1>{t.inventory.itemsTitle}</h1>
        <div style={{ display: "flex", gap: 10 }}>
          <button className="btn btn-secondary" onClick={() => setShowCategoryForm((v) => !v)}>
            {showCategoryForm ? t.common.cancel : t.inventory.newCategory}
          </button>
          <button
            className="btn btn-secondary"
            onClick={() => {
              codeGenRef.current = makeSequentialCodeGenerator(items.map((i) => i.code));
              setQuickAddCategoryId("");
              setQuickAddUnit("");
              setShowQuickAdd(true);
            }}
          >
            {t.common.quickAdd}
          </button>
          <button
            className="btn"
            onClick={() => {
              if (showForm) {
                resetForm();
                return;
              }
              setCode(makeSequentialCodeGenerator(items.map((i) => i.code))());
              setShowForm(true);
            }}
          >
            {showForm ? t.common.cancel : t.inventory.newItem}
          </button>
        </div>
      </div>

      {error && <div className="alert-error">{error}</div>}

      <QuickAddBulk
        open={showQuickAdd}
        onClose={() => setShowQuickAdd(false)}
        title={t.inventory.quickAddItemsTitle}
        hint={t.inventory.quickAddItemsHint}
        placeholder={t.inventory.quickAddItemsPlaceholder}
        disabled={!quickAddCategoryId || !quickAddUnit.trim()}
        extraFields={
          <div className="form-grid" style={{ marginTop: 10 }}>
            <div className="form-field">
              <label>{t.inventory.category}</label>
              <select value={quickAddCategoryId} onChange={(e) => setQuickAddCategoryId(e.target.value)}>
                <option value="">{t.inventory.selectCategory}</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.code} - {bilingualName(c.nameAr, c.nameEn, lang)}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-field">
              <label>{t.inventory.unitOfMeasure}</label>
              <input value={quickAddUnit} onChange={(e) => setQuickAddUnit(e.target.value)} placeholder={t.inventory.unitOfMeasurePlaceholder} />
            </div>
          </div>
        }
        onCreateOne={async (name) => {
          await ItemsApi.create({
            code: codeGenRef.current(),
            nameAr: name,
            nameEn: name,
            unitOfMeasure: quickAddUnit,
            itemCategoryId: quickAddCategoryId,
            reorderLevel: 0,
          });
        }}
        onFinished={load}
      />

      {showCategoryForm && (
        <div className="card">
          <form onSubmit={handleCategorySubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.inventory.categoryCode}</label>
                <input value={categoryCode} onChange={(e) => setCategoryCode(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameAr}</label>
                <input value={categoryNameAr} onChange={(e) => setCategoryNameAr(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.common.nameEn}</label>
                <input value={categoryNameEn} onChange={(e) => setCategoryNameEn(e.target.value)} required />
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.inventory.saveCategory}
            </button>
          </form>
        </div>
      )}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.inventory.itemCode}</label>
                <input value={code} required disabled />
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
                <label>{t.inventory.unitOfMeasure}</label>
                <input value={unitOfMeasure} onChange={(e) => setUnitOfMeasure(e.target.value)} placeholder={t.inventory.unitOfMeasurePlaceholder} required />
              </div>
              <div className="form-field">
                <label>{t.inventory.category}</label>
                <select value={itemCategoryId} onChange={(e) => setItemCategoryId(e.target.value)} required>
                  <option value="">{t.inventory.selectCategory}</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} - {bilingualName(c.nameAr, c.nameEn, lang)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <label>{t.inventory.reorderLevel}</label>
                <input type="number" step="0.01" value={reorderLevel} onChange={(e) => setReorderLevel(e.target.value)} />
              </div>
            </div>
            <div className="form-field" style={{ marginTop: 10 }}>
              <label style={{ display: "flex", alignItems: "center", gap: 8, fontWeight: "normal" }}>
                <input type="checkbox" checked={isLotTracked} onChange={(e) => setIsLotTracked(e.target.checked)} />
                {t.inventory.isLotTracked}
              </label>
              <p className="text-muted" style={{ marginTop: 4, fontSize: 13 }}>{t.inventory.isLotTrackedHint}</p>
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
              <th>{t.inventory.category}</th>
              <th>{t.inventory.unit}</th>
              <th>{t.inventory.quantityOnHand}</th>
              <th>{t.inventory.averageCost}</th>
              <th>{t.inventory.reorderLevel}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>{item.code}</td>
                <td>{bilingualName(item.nameAr, item.nameEn, lang)}</td>
                <td>{item.itemCategoryName || categoryName(item.itemCategoryId)}</td>
                <td>{item.unitOfMeasure}</td>
                <td className={item.quantityOnHand <= item.reorderLevel ? "text-danger" : undefined}>
                  {item.quantityOnHand.toLocaleString()}
                </td>
                <td>{item.averageCost.toLocaleString()}</td>
                <td>{item.reorderLevel.toLocaleString()}</td>
                <td style={{ display: "flex", gap: 6 }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => startEdit(item)}>
                    {t.common.edit}
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => handleDelete(item.id)}>
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
