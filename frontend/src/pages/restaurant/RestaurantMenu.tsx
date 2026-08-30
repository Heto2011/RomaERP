import { useEffect, useState } from "react";
import { ItemsApi, RestaurantApi } from "../../api/services";
import type { Item, RecipeLine } from "../../api/types";
import { getErrorMessage } from "../../api/client";
import { useLanguage } from "../../i18n/LanguageContext";
import { bilingualName } from "../../i18n/bilingual";

interface RecipeLineDraft {
  rawMaterialItemId: string;
  quantityPerUnit: number;
}

export default function RestaurantMenu() {
  const { t, lang } = useLanguage();
  const [items, setItems] = useState<Item[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [editingItem, setEditingItem] = useState<Item | null>(null);
  const [isMenuItem, setIsMenuItem] = useState(false);
  const [menuPrice, setMenuPrice] = useState(0);
  const [recipeLines, setRecipeLines] = useState<RecipeLineDraft[]>([]);
  const [newRawMaterialId, setNewRawMaterialId] = useState("");
  const [newQuantity, setNewQuantity] = useState(1);
  const [saving, setSaving] = useState(false);

  async function load() {
    const res = await ItemsApi.getAll();
    setItems(res.data);
  }

  useEffect(() => {
    load();
  }, []);

  async function openEditor(item: Item) {
    setError(null);
    setEditingItem(item);
    setIsMenuItem(item.isMenuItem);
    setMenuPrice(item.menuPrice);
    setNewRawMaterialId("");
    setNewQuantity(1);
    try {
      const res = await RestaurantApi.getRecipe(item.id);
      setRecipeLines(res.data.map((l: RecipeLine) => ({ rawMaterialItemId: l.rawMaterialItemId, quantityPerUnit: l.quantityPerUnit })));
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function closeEditor() {
    setEditingItem(null);
  }

  function addRecipeLine() {
    if (!newRawMaterialId) return;
    if (recipeLines.some((l) => l.rawMaterialItemId === newRawMaterialId)) return;
    setRecipeLines((prev) => [...prev, { rawMaterialItemId: newRawMaterialId, quantityPerUnit: newQuantity }]);
    setNewRawMaterialId("");
    setNewQuantity(1);
  }

  function removeRecipeLine(rawMaterialItemId: string) {
    setRecipeLines((prev) => prev.filter((l) => l.rawMaterialItemId !== rawMaterialItemId));
  }

  function itemLabel(id: string) {
    const item = items.find((i) => i.id === id);
    return item ? `${item.code} - ${bilingualName(item.nameAr, item.nameEn, lang)}` : id;
  }

  async function handleSave() {
    if (!editingItem) return;
    setError(null);
    setSaving(true);
    try {
      await RestaurantApi.setMenuItem(editingItem.id, {
        isMenuItem,
        menuPrice,
        recipeLines,
      });
      setEditingItem(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.restaurant.menuTitle}</h1>
      </div>

      {error && <div className="alert-error">{error}</div>}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.common.code}</th>
              <th>{t.common.nameAr}</th>
              <th>{t.inventory.category}</th>
              <th>{t.restaurant.onMenu}</th>
              <th>{t.restaurant.menuPrice}</th>
              <th>{t.restaurant.recipe}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id}>
                <td>{item.code}</td>
                <td>{bilingualName(item.nameAr, item.nameEn, lang)}</td>
                <td>{item.itemCategoryName}</td>
                <td>{item.isMenuItem ? "✅" : "—"}</td>
                <td>{item.isMenuItem ? item.menuPrice.toLocaleString() : "—"}</td>
                <td className="text-muted">{item.isMenuItem ? t.restaurant.hasRecipeHint : ""}</td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => openEditor(item)}>
                    {t.restaurant.editMenuItem}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editingItem && (
        <div className="modal-overlay" onClick={closeEditor}>
          <div className="card" style={{ maxWidth: 560, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
            <h3>{editingItem.code} - {bilingualName(editingItem.nameAr, editingItem.nameEn, lang)}</h3>

            <div className="form-field" style={{ marginTop: 10 }}>
              <label>
                <input type="checkbox" checked={isMenuItem} onChange={(e) => setIsMenuItem(e.target.checked)} style={{ marginInlineEnd: 8 }} />
                {t.restaurant.onMenu}
              </label>
            </div>

            {isMenuItem && (
              <>
                <div className="form-field">
                  <label>{t.restaurant.menuPrice}</label>
                  <input type="number" min={0} step="0.01" value={menuPrice} onChange={(e) => setMenuPrice(Number(e.target.value))} />
                </div>

                <h4 style={{ marginTop: 16 }}>{t.restaurant.recipe}</h4>
                <p className="text-muted">{t.restaurant.recipeHint}</p>

                <table>
                  <thead>
                    <tr>
                      <th>{t.restaurant.rawMaterial}</th>
                      <th>{t.restaurant.quantityPerUnit}</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {recipeLines.map((line) => (
                      <tr key={line.rawMaterialItemId}>
                        <td>{itemLabel(line.rawMaterialItemId)}</td>
                        <td>{line.quantityPerUnit}</td>
                        <td>
                          <button type="button" className="btn btn-secondary btn-sm" onClick={() => removeRecipeLine(line.rawMaterialItemId)}>
                            {t.common.delete}
                          </button>
                        </td>
                      </tr>
                    ))}
                    <tr>
                      <td>
                        <select value={newRawMaterialId} onChange={(e) => setNewRawMaterialId(e.target.value)}>
                          <option value="">-</option>
                          {items.filter((i) => i.id !== editingItem.id).map((i) => (
                            <option key={i.id} value={i.id}>{i.code} - {bilingualName(i.nameAr, i.nameEn, lang)}</option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <input type="number" min={0.0001} step="0.01" value={newQuantity} onChange={(e) => setNewQuantity(Number(e.target.value))} style={{ width: 90 }} />
                      </td>
                      <td>
                        <button type="button" className="btn btn-secondary btn-sm" onClick={addRecipeLine} disabled={!newRawMaterialId}>
                          {t.sales.addLine}
                        </button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </>
            )}

            <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
              <button className="btn" onClick={handleSave} disabled={saving}>{t.common.save}</button>
              <button className="btn btn-secondary" onClick={closeEditor}>{t.common.cancel}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
