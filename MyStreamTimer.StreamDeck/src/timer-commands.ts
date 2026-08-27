export const timerTargets = [
  "countdown",
  "countdown2",
  "countdown3",
  "countdown4",
  "countup",
  "countup2",
  "time",
] as const;

export type TimerTarget = (typeof timerTargets)[number];
export type DurationUnit = "minutes" | "seconds";
export type StartMode =
  "duration" | "clock-time" | "top-of-hour" | "current-time";
export type ControlOperation =
  "add" | "subtract" | "pause" | "resume" | "reset" | "stop" | "start";
const controlOperations = new Set<ControlOperation>([
  "add",
  "subtract",
  "pause",
  "resume",
  "reset",
  "stop",
  "start",
]);

export interface StartCommand {
  target: TimerTarget;
  mode: StartMode;
  amount?: number;
  unit?: DurationUnit;
  clockTime?: string;
}

export interface ControlCommand {
  target: TimerTarget;
  operation: ControlOperation;
  amount?: number;
  unit?: DurationUnit;
}

const countdownTargets = new Set<TimerTarget>([
  "countdown",
  "countdown2",
  "countdown3",
  "countdown4",
]);
const durationTargets = new Set<TimerTarget>([
  "countdown",
  "countdown2",
  "countdown3",
  "countdown4",
  "countup",
  "countup2",
]);
const amountOperations = new Set<ControlOperation>(["add", "subtract"]);

export function isTimerTarget(value: unknown): value is TimerTarget {
  return (
    typeof value === "string" && timerTargets.includes(value as TimerTarget)
  );
}

export function isCountdownTarget(target: TimerTarget): boolean {
  return countdownTargets.has(target);
}

export function buildStartUrl(command: StartCommand): string {
  if (!isTimerTarget(command.target)) {
    throw new Error("Select a valid timer.");
  }

  if (command.target === "time") {
    if (command.mode !== "current-time") {
      throw new Error("Current Time only supports Start.");
    }

    return "mystreamtimer://time/?start";
  }

  if (command.mode === "duration") {
    if (!durationTargets.has(command.target)) {
      throw new Error("This timer does not support a duration.");
    }

    const amount = requirePositiveAmount(command.amount);
    const duration = formatDuration(amount, command.unit);
    return `mystreamtimer://${command.target}/?${duration.parameter}=${duration.value}`;
  }

  if (!countdownTargets.has(command.target)) {
    throw new Error(
      "Clock time and top-of-hour starts require a countdown timer.",
    );
  }

  if (command.mode === "top-of-hour") {
    return `mystreamtimer://${command.target}/?topofhour`;
  }

  if (command.mode === "clock-time") {
    const clockTime = command.clockTime?.trim();
    if (!clockTime || !/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(clockTime)) {
      throw new Error("Enter a clock time in 24-hour HH:mm format.");
    }

    return `mystreamtimer://${command.target}/?to=${clockTime}`;
  }

  throw new Error("Select a valid start mode.");
}

export function buildControlUrl(command: ControlCommand): string {
  if (!isTimerTarget(command.target)) {
    throw new Error("Select a valid timer.");
  }
  if (!controlOperations.has(command.operation)) {
    throw new Error("Select a valid timer operation.");
  }

  if (command.target === "time") {
    if (command.operation !== "start" && command.operation !== "stop") {
      throw new Error("Current Time only supports Start and Stop.");
    }

    return `mystreamtimer://time/?${command.operation}`;
  }

  if (command.operation === "start") {
    throw new Error(
      "Use the App Timer Start action to start a countdown or count-up.",
    );
  }

  if (amountOperations.has(command.operation)) {
    const amount = requirePositiveAmount(command.amount);
    const duration = formatDuration(amount, command.unit);
    return `mystreamtimer://${command.target}/?${command.operation}${duration.parameter}=${duration.value}`;
  }

  return `mystreamtimer://${command.target}/?${command.operation}`;
}

export function timerLabel(target: TimerTarget): string {
  return (
    {
      countdown: "Down 1",
      countdown2: "Down 2",
      countdown3: "Down 3",
      countdown4: "Down 4",
      countup: "Up 1",
      countup2: "Up 2",
      time: "Time",
    } satisfies Record<TimerTarget, string>
  )[target];
}

function requirePositiveAmount(value: number | undefined): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value <= 0) {
    throw new Error("Amount must be greater than zero.");
  }

  return value;
}

function formatDuration(
  value: number,
  unit: DurationUnit | undefined,
): { parameter: "mins" | "secs"; value: string } {
  if (unit !== "seconds" && Number.isInteger(value)) {
    return { parameter: "mins", value: value.toString() };
  }

  const seconds = Math.round(value * (unit === "seconds" ? 1 : 60));
  if (seconds <= 0) {
    throw new Error("Amount is too small; use at least one second.");
  }

  return { parameter: "secs", value: seconds.toString() };
}
