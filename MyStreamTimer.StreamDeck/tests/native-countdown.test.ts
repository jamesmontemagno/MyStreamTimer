import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

import {
  formatRemaining,
  NativeCountdownManager,
  type CountdownOutput,
} from "../src/native-countdown";

describe("formatRemaining", () => {
  it.each([
    [0, "0:00"],
    [1, "0:01"],
    [60_000, "1:00"],
    [610_000, "10:10"],
    [3_661_000, "1:01:01"],
  ])("formats %i milliseconds", (milliseconds, expected) => {
    expect(formatRemaining(milliseconds)).toBe(expected);
  });
});

describe("NativeCountdownManager", () => {
  it("writes and stops independent action contexts", async () => {
    const directory = await mkdtemp(join(tmpdir(), "mystreamtimer-"));
    const output = new FakeOutput();
    const errors: unknown[] = [];
    const manager = new NativeCountdownManager((error) => errors.push(error));

    try {
      await expect(
        manager.toggle(
          "action-1",
          60,
          directory,
          "countdown.txt",
          "File\n1 min",
          output,
        ),
      ).resolves.toBe("started");
      expect(manager.isRunning("action-1")).toBe(true);
      expect(await readFile(join(directory, "countdown.txt"), "utf8")).toBe(
        "1:00",
      );

      await expect(
        manager.toggle(
          "action-1",
          60,
          directory,
          "countdown.txt",
          "File\n1 min",
          output,
        ),
      ).resolves.toBe("stopped");
      expect(manager.isRunning("action-1")).toBe(false);
      expect(await readFile(join(directory, "countdown.txt"), "utf8")).toBe("");
      expect(output.titles.at(-1)).toBe("File\n1 min");
      expect(errors).toEqual([]);
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("prevents two contexts from owning the same output file", async () => {
    const directory = await mkdtemp(join(tmpdir(), "mystreamtimer-"));
    const manager = new NativeCountdownManager(() => undefined);

    try {
      await manager.toggle(
        "action-1",
        60,
        directory,
        "countdown.txt",
        "File\n1 min",
        new FakeOutput(),
      );
      await expect(
        manager.toggle(
          "action-2",
          60,
          directory,
          "countdown.txt",
          "File\n1 min",
          new FakeOutput(),
        ),
      ).rejects.toThrow("already using this output file");
      await manager.toggle(
        "action-1",
        60,
        directory,
        "countdown.txt",
        "File\n1 min",
        new FakeOutput(),
      );
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });

  it("restores the idle title after natural completion", async () => {
    const directory = await mkdtemp(join(tmpdir(), "mystreamtimer-"));
    const output = new FakeOutput();
    const manager = new NativeCountdownManager(() => undefined);

    try {
      await manager.toggle(
        "action-1",
        0.01,
        directory,
        "countdown.txt",
        "File\n0.01 min",
        output,
      );
      await new Promise((resolve) => setTimeout(resolve, 300));

      expect(manager.isRunning("action-1")).toBe(false);
      expect(await readFile(join(directory, "countdown.txt"), "utf8")).toBe("");
      expect(output.titles.at(-1)).toBe("File\n0.01 min");
    } finally {
      await rm(directory, { force: true, recursive: true });
    }
  });
});

class FakeOutput implements CountdownOutput {
  readonly titles: string[] = [];

  setTitle(title: string): Promise<void> {
    this.titles.push(title);
    return Promise.resolve();
  }

  showAlert(): Promise<void> {
    return Promise.resolve();
  }
}
