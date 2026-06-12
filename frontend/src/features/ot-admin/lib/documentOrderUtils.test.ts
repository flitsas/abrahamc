import { describe, expect, it } from "vitest";
import { itemDisplayLabel, reorderItems } from "./documentOrderUtils.js";
import type { OtConsolidatedOrderItem } from "../api/ot-admin.schemas.js";

const baseItem = (
  overrides: Partial<OtConsolidatedOrderItem>,
): OtConsolidatedOrderItem => ({
  id: "01930101-0001-7001-8001-000000000001",
  position: 1,
  source: "catalog",
  procedureDocumentCatalogId: null,
  catalogCode: null,
  catalogName: null,
  customLabel: null,
  ...overrides,
});

describe("documentOrderUtils", () => {
  it("reorders items and reassigns positions", () => {
    const items = [
      baseItem({
        id: "01930101-0001-7001-8001-000000000001",
        position: 1,
        catalogName: "A",
      }),
      baseItem({
        id: "01930101-0001-7001-8001-000000000002",
        position: 2,
        catalogName: "B",
      }),
      baseItem({
        id: "01930101-0001-7001-8001-000000000003",
        position: 3,
        catalogName: "C",
      }),
    ];

    const reordered = reorderItems(items, 0, 2);
    expect(reordered.map((item) => item.catalogName)).toEqual(["B", "C", "A"]);
    expect(reordered.map((item) => item.position)).toEqual([1, 2, 3]);
  });

  it("returns same array for invalid drag indices", () => {
    const items = [
      baseItem({
        id: "01930101-0001-7001-8001-000000000001",
        catalogName: "A",
      }),
    ];
    expect(reorderItems(items, 0, 0)).toBe(items);
    expect(reorderItems(items, -1, 0)).toBe(items);
  });

  it("prefers custom label for display", () => {
    const label = itemDisplayLabel(
      baseItem({
        customLabel: "Documento especial",
        catalogName: "SOAT",
        source: "catalog",
      }),
    );
    expect(label).toBe("Documento especial");
  });
});
