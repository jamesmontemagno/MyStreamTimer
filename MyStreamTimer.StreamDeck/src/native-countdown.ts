import { mkdir, rename, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import process from "node:process";

export interface CountdownOutput {
  setTitle(title: string): Promise<void>;
  showAlert(): Promise<void>;
}

interface CountdownSession {
  cancelled: boolean;
  outputPath: string;
  temporaryPath: string;
  endAt: number;
  lastText?: string;
  idleTitle: string;
  output: CountdownOutput;
  outputKey: string;
  writeChain: Promise<void>;
}

export class NativeCountdownManager {
  private readonly sessions = new Map<string, CountdownSession>();
  private readonly outputOwners = new Map<string, string>();

  constructor(private readonly onError: (error: unknown) => void) {}

  async toggle(
    context: string,
    durationSeconds: number,
    outputDirectory: string,
    fileName: string,
    idleTitle: string,
    output: CountdownOutput,
  ): Promise<"started" | "stopped"> {
    const running = this.sessions.get(context);
    if (running) {
      running.cancelled = true;
      this.sessions.delete(context);
      try {
        await this.writeOutput(running, "");
        await output.setTitle(running.idleTitle);
      } finally {
        this.outputOwners.delete(running.outputKey);
      }
      return "stopped";
    }

    if (!Number.isFinite(durationSeconds) || durationSeconds <= 0) {
      throw new Error("Native countdown duration must be greater than zero.");
    }

    await mkdir(outputDirectory, { recursive: true });
    const outputPath = join(outputDirectory, fileName);
    const outputKey = normalizeOutputPath(outputPath);
    const owner = this.outputOwners.get(outputKey);
    if (owner && owner !== context) {
      throw new Error(
        "Another native countdown is already using this output file.",
      );
    }
    const session: CountdownSession = {
      cancelled: false,
      outputPath,
      temporaryPath: `${outputPath}.${context.replaceAll(/[^a-zA-Z0-9_-]/g, "_")}.tmp`,
      endAt: Date.now() + durationSeconds * 1000,
      idleTitle,
      output,
      outputKey,
      writeChain: Promise.resolve(),
    };

    this.sessions.set(context, session);
    this.outputOwners.set(outputKey, context);
    try {
      await this.tick(context, session);
    } catch (error) {
      session.cancelled = true;
      this.sessions.delete(context);
      this.outputOwners.delete(outputKey);
      throw error;
    }
    void this.run(context, session).catch(async (error: unknown) => {
      session.cancelled = true;
      this.sessions.delete(context);
      this.outputOwners.delete(outputKey);
      this.onError(error);
      await session.output.showAlert();
    });
    return "started";
  }

  isRunning(context: string): boolean {
    return this.sessions.has(context);
  }

  private async run(context: string, session: CountdownSession): Promise<void> {
    while (!session.cancelled && this.sessions.get(context) === session) {
      await new Promise((resolve) => setTimeout(resolve, 250));
      if (session.cancelled || this.sessions.get(context) !== session) {
        break;
      }
      await this.tick(context, session);
    }
  }

  private async tick(
    context: string,
    session: CountdownSession,
  ): Promise<void> {
    const remainingMilliseconds = Math.max(0, session.endAt - Date.now());
    const text =
      remainingMilliseconds === 0 ? "" : formatRemaining(remainingMilliseconds);
    if (text !== session.lastText) {
      await this.writeOutput(session, text);
      await session.output.setTitle(
        remainingMilliseconds === 0 ? session.idleTitle : text,
      );
      session.lastText = text;
    }

    if (remainingMilliseconds === 0) {
      session.cancelled = true;
      this.sessions.delete(context);
      this.outputOwners.delete(session.outputKey);
    }
  }

  private async writeOutput(
    session: CountdownSession,
    text: string,
  ): Promise<void> {
    const pendingWrite = session.writeChain.then(async () => {
      await writeFile(session.temporaryPath, text, "utf8");
      await rename(session.temporaryPath, session.outputPath);
    });
    session.writeChain = pendingWrite.catch(() => undefined);
    await pendingWrite;
  }
}

function normalizeOutputPath(path: string): string {
  const normalized = resolve(path);
  return process.platform === "win32" ? normalized.toLowerCase() : normalized;
}

export function formatRemaining(milliseconds: number): string {
  const totalSeconds = Math.max(0, Math.ceil(milliseconds / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  if (hours > 0) {
    return `${hours}:${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
  }

  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}
