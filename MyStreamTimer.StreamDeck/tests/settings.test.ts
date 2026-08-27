import { describe, expect, it } from "vitest";

import { normalizeFileName, normalizeStartSettings } from "../src/settings";

describe("settings", () => {
  it("applies safe defaults", () => {
    const settings = normalizeStartSettings({});
    expect(settings.backend).toBe("app");
    expect(settings.target).toBe("countdown");
    expect(settings.amount).toBe(5);
    expect(settings.fileName).toBe("countdown.txt");
  });

  it("rejects directory traversal in a file name", () => {
    expect(() => normalizeFileName("../countdown.txt")).toThrow();
    expect(() => normalizeFileName("folder/countdown.txt")).toThrow();
  });

  it("normalizes numeric text fields and rejects invalid settings", () => {
    expect(normalizeStartSettings({ amount: "90" }).amount).toBe(90);
    expect(() =>
      normalizeStartSettings({ target: "invalid" as "countdown" }),
    ).toThrow();
    expect(() => normalizeStartSettings({ amount: "five" })).toThrow();
    expect(() =>
      normalizeStartSettings({ unit: "hours" as "minutes" }),
    ).toThrow();
  });
});
