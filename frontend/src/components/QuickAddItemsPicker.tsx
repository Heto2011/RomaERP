import { useMemo, useState } from "react";
import type { Item } from "../api/types";
import { useLanguage } from "../i18n/LanguageContext";
import { bilingualName } from "../i18n/bilingual";

interface QuickAddItemsPickerProps {
  open: boolean;
  onClose: () => void;
  title: string;
  items: Item[];
  onAdd: (selected: Item[]) => void;
}

export default function QuickAddItemsPicker({ open, onClose, title, items, onAdd }: QuickAddItemsPickerProps) {
  const { t, lang } = useLanguage();
  const [search, setSearch] = useState("");
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (i) => i.code.toLowerCase().includes(q) || i.nameAr.toLowerCase().includes(q) || i.nameEn.toLowerCase().includes(q)
    );
  }, [items, search]);

  if (!open) return null;

  function toggle(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function handleClose() {
    setSearch("");
    setSelectedIds(new Set());
    onClose();
  }

  function handleAdd() {
    onAdd(items.filter((i) => selectedIds.has(i.id)));
    handleClose();
  }

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="card" style={{ maxWidth: 480, margin: "5% auto" }} onClick={(e) => e.stopPropagation()}>
        <h3>{title}</h3>

        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t.common.search}
          style={{ width: "100%", marginTop: 10 }}
        />

        <div style={{ maxHeight: 320, overflowY: "auto", marginTop: 10, border: "1px solid var(--color-border)", borderRadius: 6 }}>
          {filtered.length === 0 && (
            <div className="text-muted" style={{ padding: 12, textAlign: "center" }}>
              {t.common.noData}
            </div>
          )}
          {filtered.map((item) => (
            <label
              key={item.id}
              style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 12px", borderBottom: "1px solid var(--color-border)", cursor: "pointer" }}
            >
              <input type="checkbox" checked={selectedIds.has(item.id)} onChange={() => toggle(item.id)} />
              <span>{item.code} - {bilingualName(item.nameAr, item.nameEn, lang)}</span>
            </label>
          ))}
        </div>

        <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
          <button className="btn" onClick={handleAdd} disabled={selectedIds.size === 0}>
            {t.common.quickAddSubmit} ({selectedIds.size})
          </button>
          <button className="btn btn-secondary" onClick={handleClose}>
            {t.common.close}
          </button>
        </div>
      </div>
    </div>
  );
}
