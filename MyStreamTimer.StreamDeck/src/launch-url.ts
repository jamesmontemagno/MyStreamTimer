import { execFile } from "node:child_process";
import process from "node:process";

export interface LaunchCommand {
  file: string;
  args: string[];
}

export type Launcher = (command: LaunchCommand) => Promise<void>;

const protocolPrefix = "mystreamtimer://";

// Stream Deck's openUrl hands the URL to the default browser pipeline, which
// drops custom schemes, so protocol URLs are launched through the OS instead.
export function launchCommand(
  url: string,
  platform: NodeJS.Platform = process.platform,
): LaunchCommand {
  if (!url.startsWith(protocolPrefix)) {
    throw new Error("Only mystreamtimer:// URLs can be launched.");
  }

  switch (platform) {
    case "win32":
      return {
        file: "rundll32.exe",
        args: ["url.dll,FileProtocolHandler", url],
      };
    case "darwin":
      return { file: "open", args: [url] };
    default:
      return { file: "xdg-open", args: [url] };
  }
}

const defaultLauncher: Launcher = ({ file, args }) =>
  new Promise((resolve, reject) => {
    execFile(file, args, { windowsHide: true }, (error: Error | null) => {
      if (error) {
        reject(error);
      } else {
        resolve();
      }
    });
  });

export async function openTimerUrl(
  url: string,
  launcher: Launcher = defaultLauncher,
): Promise<void> {
  await launcher(launchCommand(url));
}
