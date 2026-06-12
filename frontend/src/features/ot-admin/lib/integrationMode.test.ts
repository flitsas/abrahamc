import { describe, expect, it } from "vitest";
import {
  integrationModeLabel,
  isQuipuxMode,
  normalizeIntegrationMode,
  toggleIntegrationMode,
} from "./integrationMode.js";

describe("integrationMode", () => {
  it("detects quipux aliases", () => {
    expect(isQuipuxMode("quipux")).toBe(true);
    expect(isQuipuxMode("qx")).toBe(true);
    expect(isQuipuxMode("dashboard")).toBe(false);
  });

  it("normalizes integration mode", () => {
    expect(normalizeIntegrationMode("qx")).toBe("quipux");
    expect(normalizeIntegrationMode("dashboard")).toBe("dashboard");
  });

  it("toggles between dashboard and quipux", () => {
    expect(toggleIntegrationMode("dashboard")).toBe("quipux");
    expect(toggleIntegrationMode("quipux")).toBe("dashboard");
  });

  it("returns human-readable labels", () => {
    expect(integrationModeLabel("dashboard")).toContain("Dashboard");
    expect(integrationModeLabel("quipux")).toContain("Quipux");
  });
});
