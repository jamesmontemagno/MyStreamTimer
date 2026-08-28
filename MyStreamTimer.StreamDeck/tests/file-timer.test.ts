import { mkdir, readFile, rm } from "node:fs/promises";
import { randomUUID } from "node:crypto";
import { join } from "node:path";
import process from "node:process";

import { describe, expect, it } from "vitest";

import {
  FileTimerManager,
  formatDuration,
  formatFileTimerText,
  type FileTimerOutput,
} from "../src/file-timer";
import {
  normalizeFileControlSettings,
  normalizeFileStartSettings,
} from "../src/settings";

describe("file timer formatting", () => {
  it.each([
    [0, "0:00"],
    [1, "0:01"],
    [60_000, "1:00"],
    [610_000, "10:10"],
    [3_661_000, "1:01:01"],
  ])("formats %i milliseconds", (milliseconds, expected) => {
    expect(formatDuration(milliseconds)).toBe(expected);
  });

  it("formats current time independently of locale", () => {
    expect(
      formatFileTimerText(
        "current-time",
        0,
        new Date(2026, 0, 2, 3, 4, 5).valueOf(),
      ),
    ).toBe("03:04:05");
  });

  it("rounds countdown remaining time up and count-up elapsed time down", () => {
    expect(formatFileTimerText("countdown", 1, 0)).toBe("0:01");
    expect(formatFileTimerText("countup", 1, 0)).toBe("0:00");
    expect(formatFileTimerText("countup", 1_999, 0)).toBe("0:01");
    expect(formatDuration(1_999, "floor")).toBe("0:01");
  });
});

describe("FileTimerManager", () => {
  it("starts, restarts, and stops a countdown selected by its output path", async () => {
    const directory = await createTestDirectory();
    const output = new FakeOutput();
    const errors: unknown[] = [];
    const manager = new FileTimerManager((error) => errors.push(error));
    const startSettings = normalizeFileStartSettings({
      displayFormat: "countdown",
      amount: 1,
      unit: "minutes",
      outputDirectory: directory,
      fileName: "countdown.txt",
    });

    try {
      await manager.start(startSettings, "File\n1 min", output);
      expect(manager.isRunning(join(directory, "countdown.txt"))).toBe(true);
      expect(await readFile(join(directory, "countdown.txt"), "utf8")).toBe(
        "1:00",
      );

      await manager.start(startSettings, "File\n1 min", output);
      await manager.control(
        normalizeFileControlSettings({
          displayFormat: "countdown",
          operation: "stop",
          outputDirectory: directory,
          fileName: "countdown.txt",
        }),
        new FakeOutput(),
      );
      expect(manager.isRunning(join(directory, "countdown.txt"))).toBe(false);
      expect(await readFile(join(directory, "countdown.txt"), "utf8")).toBe("");
      expect(output.titles.at(-1)).toBe("File\n1 min");
      expect(errors).toEqual([]);
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("pauses, resumes, and resets a count-up timer selected by path", async () => {
    const directory = await createTestDirectory();
    const manager = new FileTimerManager(() => undefined);

    try {
      await manager.start(
        normalizeFileStartSettings({
          displayFormat: "countup",
          outputDirectory: directory,
          fileName: "countup.txt",
        }),
        "File\nCount Up",
        new FakeOutput(),
      );
      const control = (operation: "pause" | "resume" | "reset" | "stop") =>
        manager.control(
          normalizeFileControlSettings({
            displayFormat: "countup",
            operation,
            outputDirectory: directory,
            fileName: "countup.txt",
          }),
          new FakeOutput(),
        );

      await control("pause");
      const pausedText = await readFile(join(directory, "countup.txt"), "utf8");
      await new Promise((resolve) => setTimeout(resolve, 300));
      expect(await readFile(join(directory, "countup.txt"), "utf8")).toBe(
        pausedText,
      );
      await control("resume");
      await control("reset");
      expect(await readFile(join(directory, "countup.txt"), "utf8")).toBe(
        "0:00",
      );
      await control("stop");
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("only allows start and stop controls for current time", async () => {
    const directory = await createTestDirectory();
    const manager = new FileTimerManager(() => undefined);

    try {
      const currentTimeControl = (operation: "start" | "stop") =>
        manager.control(
          normalizeFileControlSettings({
            displayFormat: "current-time",
            operation,
            outputDirectory: directory,
            fileName: "time.txt",
          }),
          new FakeOutput(),
        );

      await currentTimeControl("start");
      expect(await readFile(join(directory, "time.txt"), "utf8")).toMatch(
        /^\d{2}:\d{2}:\d{2}$/,
      );
      await currentTimeControl("stop");
      await expect(
        manager.control(
          normalizeFileControlSettings({
            displayFormat: "countdown",
            operation: "pause",
            outputDirectory: directory,
            fileName: "time.txt",
          }),
          new FakeOutput(),
        ),
      ).rejects.toThrow("No active file timer");
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("rejects controls that do not match the active timer format", async () => {
    const directory = await createTestDirectory();
    const manager = new FileTimerManager(() => undefined);

    try {
      await manager.start(
        normalizeFileStartSettings({
          displayFormat: "countdown",
          outputDirectory: directory,
          fileName: "timer.txt",
        }),
        "File\n5 min",
        new FakeOutput(),
      );
      await expect(
        manager.control(
          normalizeFileControlSettings({
            displayFormat: "countup",
            operation: "pause",
            outputDirectory: directory,
            fileName: "timer.txt",
          }),
          new FakeOutput(),
        ),
      ).rejects.toThrow("running a countdown timer");
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("restores the previous key's idle title when another key restarts the timer", async () => {
    const directory = await createTestDirectory();
    const manager = new FileTimerManager(() => undefined);
    const firstKey = new FakeOutput();
    const secondKey = new FakeOutput();
    const settings = normalizeFileStartSettings({
      displayFormat: "countup",
      outputDirectory: directory,
      fileName: "shared.txt",
    });

    try {
      await manager.start(settings, "File\nCount Up", firstKey);
      await manager.start(settings, "File\nCount Up", secondKey);
      expect(firstKey.titles.at(-1)).toBe("File\nCount Up");
      expect(manager.isRunning(join(directory, "shared.txt"))).toBe(true);
      await manager.control(
        normalizeFileControlSettings({
          displayFormat: "countup",
          operation: "stop",
          outputDirectory: directory,
          fileName: "shared.txt",
        }),
        new FakeOutput(),
      );
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("uses the control key's own idle title when it starts current time", async () => {
    const directory = await createTestDirectory();
    const manager = new FileTimerManager(() => undefined);
    const controlKey = new FakeOutput();
    const control = (operation: "start" | "stop") =>
      manager.control(
        normalizeFileControlSettings({
          displayFormat: "current-time",
          operation,
          outputDirectory: directory,
          fileName: "time.txt",
        }),
        controlKey,
        `File\n${operation === "start" ? "Start" : "Stop"}`,
      );

    try {
      await control("start");
      await control("stop");
      expect(controlKey.titles.at(-1)).toBe("File\nStart");
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("restores the idle title after natural completion", async () => {
    const directory = await createTestDirectory();
    const output = new FakeOutput();
    const manager = new FileTimerManager(() => undefined);

    try {
      await manager.start(
        normalizeFileStartSettings({
          displayFormat: "countdown",
          amount: 0.01,
          unit: "seconds",
          outputDirectory: directory,
          fileName: "countdown.txt",
        }),
        "File\n0.01 min",
        output,
      );

      await expect
        .poll(
          () => manager.isRunning(join(directory, "countdown.txt")),
          { timeout: 2_000 },
        )
        .toBe(false);
      expect(await readFile(join(directory, "countdown.txt"), "utf8")).toBe(
        "0:00",
      );
      expect(output.titles.at(-1)).toBe("File\n0.01 min");
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });
});

class FakeOutput implements FileTimerOutput {
  readonly titles: string[] = [];

  setTitle(title: string): Promise<void> {
    this.titles.push(title);
    return Promise.resolve();
  }

  showAlert(): Promise<void> {
    return Promise.resolve();
  }
}

async function createTestDirectory(): Promise<string> {
  const directory = join(process.cwd(), `.test-file-timer-${randomUUID()}`);
  await mkdir(directory, { recursive: true });
  return directory;
}
