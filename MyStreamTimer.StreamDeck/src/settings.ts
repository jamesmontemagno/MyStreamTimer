import { basename, join, resolve } from "node:path";
import { homedir } from "node:os";

import type {
  ControlOperation,
  DurationUnit,
  StartMode,
  TimerTarget,
} from "./timer-commands";
import {
  buildControlUrl,
  buildStartUrl,
  isCountdownTarget,
  isTimerTarget,
} from "./timer-commands";

export type FileTimerFormat = "countdown" | "countup" | "current-time";
export type FileTimerOperation =
  "pause" | "resume" | "reset" | "stop" | "start";

const defaultOutputDirectory = join(
  homedir(),
  "Documents",
  "MyStreamTimerStreamDeck",
);

export interface StartTimerSettings {
  [key: string]: string | number | undefined;
  target?: TimerTarget;
  startMode?: StartMode;
  amount?: number | string;
  unit?: DurationUnit;
  clockTime?: string;
}

export interface FileTimerStartSettings {
  [key: string]: string | number | undefined;
  displayFormat?: FileTimerFormat;
  amount?: number | string;
  unit?: DurationUnit;
  outputDirectory?: string;
  fileName?: string;
}

export interface ControlTimerSettings {
  [key: string]: string | number | undefined;
  target?: TimerTarget;
  operation?: ControlOperation;
  amount?: number | string;
  unit?: DurationUnit;
}

export interface FileTimerControlSettings {
  [key: string]: string | undefined;
  displayFormat?: FileTimerFormat;
  operation?: FileTimerOperation;
  outputDirectory?: string;
  fileName?: string;
}

export interface NormalizedStartTimerSettings {
  target: TimerTarget;
  startMode: StartMode;
  amount: number;
  unit: DurationUnit;
  clockTime: string;
}

interface NormalizedFileOutputSettings {
  outputDirectory: string;
  fileName: string;
}

export interface NormalizedFileTimerStartSettings extends NormalizedFileOutputSettings {
  displayFormat: FileTimerFormat;
  amount: number;
  unit: DurationUnit;
}

export interface NormalizedControlTimerSettings {
  target: TimerTarget;
  operation: ControlOperation;
  amount: number;
  unit: DurationUnit;
}

export interface NormalizedFileTimerControlSettings extends NormalizedFileOutputSettings {
  displayFormat: FileTimerFormat;
  operation: FileTimerOperation;
}

export function normalizeStartSettings(
  settings: StartTimerSettings,
): NormalizedStartTimerSettings {
  const target = normalizeTarget(settings.target);
  // Start mode only applies to countdowns: count-ups always take a duration
  // and Current Time simply starts, so stored modes for those are ignored.
  const startMode: StartMode =
    target === "time"
      ? "current-time"
      : isCountdownTarget(target)
        ? normalizeStartMode(settings.startMode)
        : "duration";
  const usesDuration = startMode === "duration";
  const amount = usesDuration ? normalizeAmount(settings.amount, 5) : 5;
  const unit = usesDuration ? normalizeUnit(settings.unit) : "minutes";
  const clockTime =
    startMode === "clock-time"
      ? settings.clockTime?.trim() || "12:00"
      : "12:00";
  const normalized = {
    target,
    startMode,
    amount,
    unit,
    clockTime,
  };

  buildStartUrl({
    target,
    mode: startMode,
    amount,
    unit,
    clockTime,
  });
  return normalized;
}

export function normalizeFileStartSettings(
  settings: FileTimerStartSettings,
): NormalizedFileTimerStartSettings {
  const displayFormat = normalizeFileTimerFormat(settings.displayFormat);
  const usesDuration = displayFormat === "countdown";
  const normalized = {
    displayFormat,
    amount: usesDuration ? normalizeAmount(settings.amount, 5) : 5,
    unit: usesDuration ? normalizeUnit(settings.unit) : "minutes",
    ...normalizeFileOutputSettings(settings),
  };

  if (usesDuration) {
    requirePositiveAmount(normalized.amount);
  }
  return normalized;
}

export function normalizeControlSettings(
  settings: ControlTimerSettings,
): NormalizedControlTimerSettings {
  const target = normalizeTarget(settings.target);
  const operation = normalizeOperation(settings.operation);
  const usesAmount = operation === "add" || operation === "subtract";
  const normalized = {
    target,
    operation,
    amount: usesAmount ? normalizeAmount(settings.amount, 1) : 1,
    unit: usesAmount ? normalizeUnit(settings.unit) : "minutes",
  };

  buildControlUrl(normalized);
  return normalized;
}

export function normalizeFileControlSettings(
  settings: FileTimerControlSettings,
): NormalizedFileTimerControlSettings {
  const displayFormat = normalizeFileTimerFormat(settings.displayFormat);
  const operation = normalizeFileTimerOperation(settings.operation);

  if (displayFormat === "current-time") {
    if (operation !== "start" && operation !== "stop") {
      throw new Error("Current Time file timers only support Start and Stop.");
    }
  } else if (operation === "start") {
    throw new Error("Use the Stream Deck Start action to start this timer.");
  }

  return {
    displayFormat,
    operation,
    ...normalizeFileOutputSettings(settings),
  };
}

export function getFileOutputPath(
  settings: NormalizedFileOutputSettings,
): string {
  return resolve(settings.outputDirectory, settings.fileName);
}

export function normalizeFileName(value: string | undefined): string {
  const candidate = value?.trim() || "countdown.txt";
  if (
    basename(candidate) !== candidate ||
    candidate === "." ||
    candidate === ".."
  ) {
    throw new Error("File name cannot contain directory separators.");
  }

  return candidate;
}

function normalizeAmount(
  value: number | string | undefined,
  fallback: number,
): number {
  if (value === undefined || value === "") {
    return fallback;
  }

  const parsed = typeof value === "number" ? value : Number(value);
  if (!Number.isFinite(parsed)) {
    throw new Error("Amount must be a number.");
  }

  return parsed;
}

function requirePositiveAmount(value: number): void {
  if (value <= 0) {
    throw new Error("Amount must be greater than zero.");
  }
}

function normalizeTarget(value: TimerTarget | undefined): TimerTarget {
  if (value === undefined) {
    return "countdown";
  }
  if (isTimerTarget(value)) {
    return value;
  }
  throw new Error("Select a valid timer.");
}

function normalizeStartMode(value: StartMode | undefined): StartMode {
  if (value === undefined) {
    return "duration";
  }
  if (
    value === "duration" ||
    value === "clock-time" ||
    value === "top-of-hour"
  ) {
    return value;
  }
  throw new Error("Select a valid start mode.");
}

function normalizeOperation(
  value: ControlOperation | undefined,
): ControlOperation {
  if (value === undefined) {
    return "pause";
  }
  if (
    value === "add" ||
    value === "subtract" ||
    value === "pause" ||
    value === "resume" ||
    value === "reset" ||
    value === "stop" ||
    value === "start"
  ) {
    return value;
  }
  throw new Error("Select a valid timer operation.");
}

function normalizeUnit(value: DurationUnit | undefined): DurationUnit {
  if (value === undefined || value === "minutes") {
    return "minutes";
  }
  if (value === "seconds") {
    return "seconds";
  }
  throw new Error("Select minutes or seconds.");
}

function normalizeFileOutputSettings(
  settings: Pick<FileTimerStartSettings, "outputDirectory" | "fileName">,
): NormalizedFileOutputSettings {
  return {
    outputDirectory: settings.outputDirectory?.trim() || defaultOutputDirectory,
    fileName: normalizeFileName(settings.fileName),
  };
}

function normalizeFileTimerFormat(
  value: FileTimerFormat | undefined,
): FileTimerFormat {
  if (value === undefined || value === "countdown") {
    return "countdown";
  }
  if (value === "countup" || value === "current-time") {
    return value;
  }
  throw new Error("Select a valid file timer display format.");
}

function normalizeFileTimerOperation(
  value: FileTimerOperation | undefined,
): FileTimerOperation {
  if (value === undefined || value === "pause") {
    return "pause";
  }
  if (
    value === "resume" ||
    value === "reset" ||
    value === "stop" ||
    value === "start"
  ) {
    return value;
  }
  throw new Error("Select a valid file timer operation.");
}
