import { describe, expect, it } from "vitest";
import { COMPANY_MODULE_KEYS } from "../api/companies.schemas.js";
import {
  formatModuleConfigExample,
  MODULE_CONFIG_EXAMPLES,
} from "./moduleConfigExamples.js";

describe("moduleConfigExamples", () => {
  it("defines a valid example for each module key", () => {
    for (const key of Object.values(COMPANY_MODULE_KEYS)) {
      const entry = MODULE_CONFIG_EXAMPLES[key];
      expect(entry.description.length).toBeGreaterThan(0);
      expect(() => JSON.stringify(entry.example)).not.toThrow();
      expect(formatModuleConfigExample(key)).toMatch(/^\{\n/);
    }
  });
});
