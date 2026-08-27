import { mkdir, rename, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import process from "node:process";

import {
  getFileOutputPath,
  type NormalizedFileTimerControlSettings,
  type NormalizedFileTimerStartSettings,
} from "./settings";

export interface FileTimerOutput {
  setTitle(title: string): Promise<void>;
  showAlert(): Promise<void>;
}

interface FileTimerSession {
  cancelled: boolean;
  displayFormat: NormalizedFileTimerStartSettings["displayFormat"];
  durationMilliseconds?: number;
  pausedAt?: number;
  startedAt: number;
  outputPath: string;
  temporaryPath: string;
  lastText?: string;
  idleTitle: string;
  output: FileTimerOutput;
  outputKey: string;
  writeChain: Promise<void>;
}

export class FileTimerManager {
  private readonly sessions = new Map<string, FileTimerSession>();

  constructor(private readonly onError: (error: unknown) => void) {}

  async start(
    settings: NormalizedFileTimerStartSettings,
    idleTitle: string,
    output: FileTimerOutput,
  ): Promise<void> {
    const outputPath = getFileOutputPath(settings);
    const outputKey = normalizeOutputPath(outputPath);
    const existing = this.sessions.get(outputKey);
    if (existing) {
      existing.cancelled = true;
      await existing.writeChain;
    }

    await mkdir(settings.outputDirectory, { recursive: true });
    const now = Date.now();
    const session: FileTimerSession = {
      cancelled: false,
      displayFormat: settings.displayFormat,
      durationMilliseconds:
        settings.displayFormat === "countdown"
          ? settings.amount * (settings.unit === "seconds" ? 1_000 : 60_000)
          : undefined,
      startedAt: now,
      outputPath,
      temporaryPath: `${outputPath}.mystreamtimer.tmp`,
      idleTitle,
      output,
      outputKey,
      writeChain: Promise.resolve(),
    };

    this.sessions.set(outputKey, session);
    try {
      await this.tick(session);
    } catch (error) {
      session.cancelled = true;
      this.sessions.delete(outputKey);
      throw error;
    }
    void this.run(session).catch(async (error: unknown) => {
      session.cancelled = true;
      this.sessions.delete(outputKey);
      this.onError(error);
      await session.output.showAlert();
    });
  }

  async control(
    settings: NormalizedFileTimerControlSettings,
    output: FileTimerOutput,
  ): Promise<void> {
    const outputKey = normalizeOutputPath(getFileOutputPath(settings));
    const session = this.sessions.get(outputKey);

    if (
      settings.displayFormat === "current-time" &&
      settings.operation === "start"
    ) {
      if (session) {
        this.requireMatchingFormat(session, settings.displayFormat);
        return;
      }
      await this.start(
        {
          ...settings,
          amount: 5,
          unit: "minutes",
        },
        fileTimerIdleTitle("current-time"),
        output,
      );
      return;
    }

    if (!session) {
      throw new Error("No active file timer is using this output file.");
    }
    this.requireMatchingFormat(session, settings.displayFormat);

    if (settings.operation === "pause") {
      if (session.pausedAt !== undefined) {
        throw new Error("The file timer is already paused.");
      }
      await this.tick(session);
      if (session.cancelled) {
        throw new Error("The file timer has completed.");
      }
      session.pausedAt = Date.now();
      return;
    }
    if (settings.operation === "resume") {
      if (session.pausedAt === undefined) {
        throw new Error("The file timer is not paused.");
      }
      session.startedAt += Date.now() - session.pausedAt;
      session.pausedAt = undefined;
      await this.tick(session);
      return;
    }
    if (settings.operation === "reset") {
      session.startedAt = Date.now();
      session.pausedAt = undefined;
      session.lastText = undefined;
      await this.tick(session);
      return;
    }
    if (settings.operation === "stop") {
      session.cancelled = true;
      this.sessions.delete(outputKey);
      await this.writeOutput(session, "");
      await session.output.setTitle(session.idleTitle);
      return;
    }

    throw new Error("Only Current Time file timers can be started here.");
  }

  isRunning(outputPath: string): boolean {
    return this.sessions.has(normalizeOutputPath(outputPath));
  }

  private async run(session: FileTimerSession): Promise<void> {
    while (
      !session.cancelled &&
      this.sessions.get(session.outputKey) === session
    ) {
      await new Promise((resolve) => setTimeout(resolve, 250));
      if (
        session.cancelled ||
        this.sessions.get(session.outputKey) !== session
      ) {
        break;
      }
      if (session.pausedAt === undefined) {
        await this.tick(session);
      }
    }
  }

  private async tick(session: FileTimerSession): Promise<void> {
    const now = session.pausedAt ?? Date.now();
    const elapsedMilliseconds = Math.max(0, now - session.startedAt);
    const remainingMilliseconds =
      session.displayFormat === "countdown"
        ? Math.max(0, (session.durationMilliseconds ?? 0) - elapsedMilliseconds)
        : undefined;
    const text = formatFileTimerText(
      session.displayFormat,
      remainingMilliseconds ?? elapsedMilliseconds,
      now,
    );
    if (text !== session.lastText) {
      await this.writeOutput(session, text);
      await session.output.setTitle(text);
      session.lastText = text;
    }

    if (session.displayFormat === "countdown" && remainingMilliseconds === 0) {
      session.cancelled = true;
      this.sessions.delete(session.outputKey);
      await session.output.setTitle(session.idleTitle);
    }
  }

  private async writeOutput(
    session: FileTimerSession,
    text: string,
  ): Promise<void> {
    const pendingWrite = session.writeChain.then(async () => {
      await writeFile(session.temporaryPath, text, "utf8");
      await rename(session.temporaryPath, session.outputPath);
    });
    session.writeChain = pendingWrite.catch(() => undefined);
    await pendingWrite;
  }

  private requireMatchingFormat(
    session: FileTimerSession,
    displayFormat: NormalizedFileTimerStartSettings["displayFormat"],
  ): void {
    if (session.displayFormat !== displayFormat) {
      throw new Error(
        `The output file is running a ${fileTimerFormatName(session.displayFormat)} timer.`,
      );
    }
  }
}

function normalizeOutputPath(path: string): string {
  const normalized = resolve(path);
  return process.platform === "win32" ? normalized.toLowerCase() : normalized;
}

export function formatFileTimerText(
  displayFormat: NormalizedFileTimerStartSettings["displayFormat"],
  milliseconds: number,
  now: number,
): string {
  if (displayFormat === "current-time") {
    return formatCurrentTime(new Date(now));
  }
  return formatDuration(milliseconds);
}

export function fileTimerIdleTitle(
  displayFormat: NormalizedFileTimerStartSettings["displayFormat"],
  amount?: number,
  unit?: "minutes" | "seconds",
): string {
  if (displayFormat === "countdown") {
    return `File\n${amount ?? 5}${unit === "seconds" ? " sec" : " min"}`;
  }
  return `File\n${displayFormat === "countup" ? "Count Up" : "Time"}`;
}

export function formatDuration(milliseconds: number): string {
  const totalSeconds = Math.max(0, Math.ceil(milliseconds / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  if (hours > 0) {
    return `${hours}:${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
  }

  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

function formatCurrentTime(date: Date): string {
  return [date.getHours(), date.getMinutes(), date.getSeconds()]
    .map((value) => value.toString().padStart(2, "0"))
    .join(":");
}

function fileTimerFormatName(
  displayFormat: NormalizedFileTimerStartSettings["displayFormat"],
): string {
  return (
    {
      countdown: "countdown",
      countup: "count-up",
      "current-time": "current time",
    } satisfies Record<
      NormalizedFileTimerStartSettings["displayFormat"],
      string
    >
  )[displayFormat];
}
