import { basename, join } from "node:path";
import { homedir } from "node:os";

import type {
  ControlOperation,
  DurationUnit,
  StartMode,
  TimerTarget,
} from "./timer-commands";
import { isTimerTarget } from "./timer-commands";

export type ExecutionBackend = "app" | "native";

export interface StartTimerSettings {
  [key: string]: string | number | undefined;
  backend?: ExecutionBackend;
  target?: TimerTarget;
  startMode?: StartMode;
  amount?: number | string;
  unit?: DurationUnit;
  clockTime?: string;
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

export interface NormalizedStartTimerSettings {
  backend: ExecutionBackend;
  target: TimerTarget;
  startMode: StartMode;
  amount: number;
  unit: DurationUnit;
  clockTime: string;
  outputDirectory: string;
  fileName: string;
}

export interface NormalizedControlTimerSettings {
  target: TimerTarget;
  operation: ControlOperation;
  amount: number;
  unit: DurationUnit;
}

export function normalizeStartSettings(
  settings: StartTimerSettings,
): NormalizedStartTimerSettings {
  return {
    backend: normalizeBackend(settings.backend),
    target: normalizeTarget(settings.target),
    startMode: normalizeStartMode(settings.startMode),
    amount: normalizeAmount(settings.amount, 5),
    unit: normalizeUnit(settings.unit),
    clockTime: settings.clockTime?.trim() || "12:00",
    outputDirectory:
      settings.outputDirectory?.trim() ||
      join(homedir(), "Documents", "MyStreamTimerStreamDeck"),
    fileName: normalizeFileName(settings.fileName),
  };
}

export function normalizeControlSettings(
  settings: ControlTimerSettings,
): NormalizedControlTimerSettings {
  return {
    target: normalizeTarget(settings.target),
    operation: normalizeOperation(settings.operation),
    amount: normalizeAmount(settings.amount, 1),
    unit: normalizeUnit(settings.unit),
  };
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

function normalizeBackend(
  value: ExecutionBackend | undefined,
): ExecutionBackend {
  if (value === undefined || value === "app") {
    return "app";
  }
  if (value === "native") {
    return "native";
  }
  throw new Error("Select a valid execution backend.");
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
    value === "top-of-hour" ||
    value === "current-time"
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
