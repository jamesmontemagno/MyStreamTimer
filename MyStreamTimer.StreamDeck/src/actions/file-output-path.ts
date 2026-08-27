import streamDeck from "@elgato/streamdeck";

import { getFileOutputPath } from "../settings";

type Logger = typeof streamDeck.logger;

// Messages the property inspector sends via sendToPlugin.
export interface PluginMessage {
  [key: string]: string | undefined;
  event?: string;
}

interface FileOutputLocation {
  outputDirectory: string;
  fileName: string;
}

export function isOutputPathRequest(payload: PluginMessage): boolean {
  return payload.event === "request-file-output-path";
}

// Sends the resolved output path to the property inspector so its copy button
// always reflects the exact file the plugin writes, including defaults.
export async function sendOutputPath<TSettings>(
  logger: Logger,
  settings: TSettings,
  normalize: (settings: TSettings) => FileOutputLocation,
): Promise<void> {
  try {
    await streamDeck.ui.sendToPropertyInspector({
      event: "file-output-path",
      path: getFileOutputPath(normalize(settings)),
    });
  } catch (error) {
    logger.warn("Unable to resolve file output path.", error);
  }
}
