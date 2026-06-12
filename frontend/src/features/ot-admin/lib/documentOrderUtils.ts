import type { OtConsolidatedOrderItem } from "../api/ot-admin.schemas.js";

export function reorderItems(
  items: OtConsolidatedOrderItem[],
  fromIndex: number,
  toIndex: number,
): OtConsolidatedOrderItem[] {
  if (
    fromIndex < 0 ||
    toIndex < 0 ||
    fromIndex >= items.length ||
    toIndex >= items.length ||
    fromIndex === toIndex
  ) {
    return items;
  }

  const next = [...items];
  const [moved] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, moved);

  return next.map((item, index) => ({
    ...item,
    position: index + 1,
  }));
}

export function itemDisplayLabel(item: OtConsolidatedOrderItem): string {
  return (
    item.customLabel?.trim() ||
    item.catalogName?.trim() ||
    item.catalogCode?.trim() ||
    item.source
  );
}
