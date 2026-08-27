import streamDeck, {
  action,
  type DidReceiveSettingsEvent,
  type KeyDownEvent,
  SingletonAction,
  type WillAppearEvent,
} from "@elgato/streamdeck";

import { fileTimers } from "../file-timer-service";
import {
  getFileOutputPath,
  normalizeFileControlSettings,
  type FileTimerControlSettings,
} from "../settings";

const logger = streamDeck.logger.createScope("FileTimerControl");

@action({ UUID: "com.refractored.mystreamtimer.file-timer-control" })
export class FileTimerControlAction extends SingletonAction<FileTimerControlSettings> {
  override onWillAppear(
    ev: WillAppearEvent<FileTimerControlSettings>,
  ): Promise<void> {
    return this.updateTitle(ev);
  }

  override onDidReceiveSettings(
    ev: DidReceiveSettingsEvent<FileTimerControlSettings>,
  ): Promise<void> {
    return this.updateSettings(ev);
  }

  override async onKeyDown(
    ev: KeyDownEvent<FileTimerControlSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeFileControlSettings(ev.payload.settings);
      await fileTimers.control(settings, ev.action);
      await ev.action.showOk();
    } catch (error) {
      logger.error("Unable to control file timer.", error);
      await ev.action.showAlert();
    }
  }

  private async updateSettings(
    ev: DidReceiveSettingsEvent<FileTimerControlSettings>,
  ): Promise<void> {
    await this.updateTitle(ev);
    try {
      await streamDeck.ui.sendToPropertyInspector({
        event: "file-output-path",
        path: getFileOutputPath(
          normalizeFileControlSettings(ev.payload.settings),
        ),
      });
    } catch (error) {
      logger.warn("Unable to resolve file output path.", error);
    }
  }

  private async updateTitle(
    ev:
      | WillAppearEvent<FileTimerControlSettings>
      | DidReceiveSettingsEvent<FileTimerControlSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeFileControlSettings(ev.payload.settings);
      await ev.action.setTitle(`File\n${capitalize(settings.operation)}`);
    } catch (error) {
      logger.warn("Invalid File Timer Control settings.", error);
      await ev.action.setTitle("Configure");
    }
  }
}

function capitalize(value: string): string {
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}
