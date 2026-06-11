import { describe, expect, it } from "vitest";
import {
  canEditCompanyConfig,
  hasPermission,
  PERMISSIONS,
} from "../lib/permissions.js";

describe("hasPermission", () => {
  it("returns true when slug is present", () => {
    expect(
      hasPermission(
        ["modulo.tramites.ver", "ui.tramites.generar-consolidado"],
        "ui.tramites.generar-consolidado",
      ),
    ).toBe(true);
  });

  it("returns false when slug is absent", () => {
    expect(
      hasPermission(["modulo.tramites.ver"], "ui.tramites.generar-consolidado"),
    ).toBe(false);
  });

  it("returns true for super admin regardless of slugs", () => {
    expect(hasPermission([], "ui.tramites.generar-consolidado", true)).toBe(
      true,
    );
  });

  it("allows edit company config with ui or crud-total permission", () => {
    expect(canEditCompanyConfig([PERMISSIONS.editCompanyConfig])).toBe(true);
    expect(canEditCompanyConfig([PERMISSIONS.crudTotalCompanies])).toBe(true);
    expect(canEditCompanyConfig(["modulo.companias.ver"])).toBe(false);
    expect(canEditCompanyConfig([], true)).toBe(true);
  });
});
