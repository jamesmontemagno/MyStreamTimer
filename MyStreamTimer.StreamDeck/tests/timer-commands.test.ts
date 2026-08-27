import { describe, expect, it } from "vitest";

import { buildControlUrl, buildStartUrl } from "../src/timer-commands";

describe("buildStartUrl", () => {
  it.each([
    [
      { target: "countdown", mode: "duration", amount: 5, unit: "minutes" },
      "mystreamtimer://countdown/?mins=5",
    ],
    [
      { target: "countdown2", mode: "duration", amount: 90, unit: "seconds" },
      "mystreamtimer://countdown2/?secs=90",
    ],
    [
      { target: "countdown3", mode: "clock-time", clockTime: "15:30" },
      "mystreamtimer://countdown3/?to=15:30",
    ],
    [
      { target: "countdown4", mode: "top-of-hour" },
      "mystreamtimer://countdown4/?topofhour",
    ],
    [
      { target: "countup", mode: "duration", amount: 1, unit: "minutes" },
      "mystreamtimer://countup/?mins=1",
    ],
    [{ target: "time", mode: "current-time" }, "mystreamtimer://time/?start"],
    [
      { target: "countdown", mode: "duration", amount: 1.5, unit: "minutes" },
      "mystreamtimer://countdown/?secs=90",
    ],
  ] as const)("builds %o", (command, expected) => {
    expect(buildStartUrl(command)).toBe(expected);
  });

  it("rejects unsupported combinations", () => {
    expect(() =>
      buildStartUrl({ target: "countup", mode: "top-of-hour" }),
    ).toThrow();
    expect(() =>
      buildStartUrl({ target: "time", mode: "duration", amount: 5 }),
    ).toThrow();
    expect(() =>
      buildStartUrl({ target: "countdown", mode: "duration", amount: 0 }),
    ).toThrow();
    expect(() =>
      buildStartUrl({
        target: "countdown",
        mode: "clock-time",
        clockTime: "25:00",
      }),
    ).toThrow();
  });
});

describe("buildControlUrl", () => {
  it.each([
    [
      { target: "countdown", operation: "pause" },
      "mystreamtimer://countdown/?pause",
    ],
    [
      { target: "countdown2", operation: "resume" },
      "mystreamtimer://countdown2/?resume",
    ],
    [
      { target: "countdown3", operation: "reset" },
      "mystreamtimer://countdown3/?reset",
    ],
    [
      { target: "countdown4", operation: "stop" },
      "mystreamtimer://countdown4/?stop",
    ],
    [
      { target: "countup", operation: "add", amount: 1, unit: "minutes" },
      "mystreamtimer://countup/?addmins=1",
    ],
    [
      {
        target: "countup2",
        operation: "subtract",
        amount: 30,
        unit: "seconds",
      },
      "mystreamtimer://countup2/?subtractsecs=30",
    ],
    [{ target: "time", operation: "start" }, "mystreamtimer://time/?start"],
    [{ target: "time", operation: "stop" }, "mystreamtimer://time/?stop"],
    [
      {
        target: "countdown",
        operation: "add",
        amount: 0.5,
        unit: "minutes",
      },
      "mystreamtimer://countdown/?addsecs=30",
    ],
  ] as const)("builds %o", (command, expected) => {
    expect(buildControlUrl(command)).toBe(expected);
  });

  it("rejects unsupported combinations", () => {
    expect(() =>
      buildControlUrl({ target: "time", operation: "pause" }),
    ).toThrow();
    expect(() =>
      buildControlUrl({ target: "countdown", operation: "start" }),
    ).toThrow();
    expect(() =>
      buildControlUrl({ target: "countdown", operation: "add", amount: -1 }),
    ).toThrow();
    expect(() =>
      buildControlUrl({
        target: "countdown",
        operation: "invalid" as "pause",
      }),
    ).toThrow();
  });
});
