import { describe, expect, it } from "vitest";
import { join } from "node:path";
import process from "node:process";

import {
  getFileOutputPath,
  normalizeControlSettings,
  normalizeFileControlSettings,
  normalizeFileName,
  normalizeFileStartSettings,
  normalizeStartSettings,
} from "../src/settings";

describe("settings", () => {
  it("applies safe defaults", () => {
    const settings = normalizeStartSettings({});
    expect(settings.target).toBe("countdown");
    expect(settings.amount).toBe(5);
    expect(settings.startMode).toBe("duration");
  });

  it("rejects directory traversal in a file name", () => {
    expect(() => normalizeFileName("../countdown.txt")).toThrow();
    expect(() => normalizeFileName("folder/countdown.txt")).toThrow();
  });

  it("resolves the file timer output path", () => {
    expect(
      getFileOutputPath(
        normalizeFileStartSettings({
          outputDirectory: "test-output",
          fileName: "countdown.txt",
        }),
      ),
    ).toBe(join(process.cwd(), "test-output", "countdown.txt"));
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

  it("validates only the stream start settings needed by the selected mode", () => {
    expect(
      normalizeStartSettings({
        target: "time",
        startMode: "current-time",
        amount: "invalid",
        clockTime: "25:00",
      }),
    ).toMatchObject({ amount: 5, clockTime: "12:00" });
    expect(() =>
      normalizeStartSettings({
        target: "time",
        startMode: "duration",
      }),
    ).toThrow("Current Time only supports Start.");
    expect(() =>
      normalizeStartSettings({
        target: "countdown",
        startMode: "clock-time",
        clockTime: "25:00",
      }),
    ).toThrow("Enter a clock time");
  });

  it("normalizes file timer formats and ignores stale duration settings", () => {
    expect(
      normalizeFileStartSettings({
        displayFormat: "countup",
        amount: "invalid",
        unit: "hours" as "minutes",
      }),
    ).toMatchObject({ amount: 5, unit: "minutes" });
    expect(
      normalizeFileStartSettings({
        displayFormat: "current-time",
        amount: "invalid",
        unit: "hours" as "minutes",
      }),
    ).toMatchObject({ amount: 5, unit: "minutes" });
    expect(() =>
      normalizeFileStartSettings({ displayFormat: "countdown", amount: 0 }),
    ).toThrow("Amount must be greater than zero.");
    expect(() =>
      normalizeFileStartSettings({
        displayFormat: "invalid" as "countdown",
      }),
    ).toThrow("valid file timer display format");
  });

  it("validates control target and operation combinations", () => {
    expect(
      normalizeControlSettings({
        target: "countdown",
        operation: "pause",
        amount: "invalid",
        unit: "hours" as "minutes",
      }),
    ).toMatchObject({ amount: 1, unit: "minutes" });
    expect(() =>
      normalizeControlSettings({ target: "time", operation: "pause" }),
    ).toThrow("Current Time only supports Start and Stop.");
    expect(() =>
      normalizeControlSettings({ target: "countdown", operation: "start" }),
    ).toThrow("Use the App Timer Start action");
    expect(() =>
      normalizeControlSettings({
        target: "countdown",
        operation: "add",
        amount: 0,
      }),
    ).toThrow("Amount must be greater than zero.");
  });

  it("validates file control format and operation combinations", () => {
    expect(
      normalizeFileControlSettings({
        displayFormat: "countdown",
        operation: "pause",
        outputDirectory: "test-output",
        fileName: "timer.txt",
      }),
    ).toMatchObject({ displayFormat: "countdown", operation: "pause" });
    expect(() =>
      normalizeFileControlSettings({
        displayFormat: "current-time",
        operation: "pause",
      }),
    ).toThrow("Current Time file timers only support Start and Stop.");
    expect(() =>
      normalizeFileControlSettings({
        displayFormat: "countup",
        operation: "start",
      }),
    ).toThrow("Use the Stream Deck Start action");
    expect(() =>
      normalizeFileControlSettings({
        displayFormat: "countdown",
        operation: "invalid" as "pause",
      }),
    ).toThrow("valid file timer operation");
  });
});
