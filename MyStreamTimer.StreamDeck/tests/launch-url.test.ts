import { describe, expect, it } from "vitest";

import { launchCommand, openTimerUrl } from "../src/launch-url";

describe("launchCommand", () => {
  const url = "mystreamtimer://countdown/?mins=5";

  it("uses the Windows shell protocol handler", () => {
    expect(launchCommand(url, "win32")).toEqual({
      file: "rundll32.exe",
      args: ["url.dll,FileProtocolHandler", url],
    });
  });

  it("uses open on macOS", () => {
    expect(launchCommand(url, "darwin")).toEqual({ file: "open", args: [url] });
  });

  it("uses xdg-open elsewhere", () => {
    expect(launchCommand(url, "linux")).toEqual({
      file: "xdg-open",
      args: [url],
    });
  });

  it("refuses non-protocol URLs", () => {
    expect(() => launchCommand("https://example.com", "win32")).toThrow(
      "Only mystreamtimer://",
    );
  });
});

describe("openTimerUrl", () => {
  it("passes the command to the launcher", async () => {
    const launched: unknown[] = [];
    await openTimerUrl("mystreamtimer://time/?start", (command) => {
      launched.push(command);
      return Promise.resolve();
    });
    expect(launched).toHaveLength(1);
  });

  it("propagates launcher failures", async () => {
    await expect(
      openTimerUrl("mystreamtimer://time/?start", () =>
        Promise.reject(new Error("spawn failed")),
      ),
    ).rejects.toThrow("spawn failed");
  });
});
