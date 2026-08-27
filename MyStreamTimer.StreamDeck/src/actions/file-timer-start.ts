import streamDeck, {
  action,
  type DidReceiveSettingsEvent,
  type KeyDownEvent,
  SingletonAction,
  type WillAppearEvent,
} from "@elgato/streamdeck";

import { fileTimerIdleTitle } from "../file-timer";
import { fileTimers } from "../file-timer-service";
import {
  getFileOutputPath,
  normalizeFileStartSettings,
  type FileTimerStartSettings,
} from "../settings";

const logger = streamDeck.logger.createScope("FileTimerStart");

@action({ UUID: "com.refractored.mystreamtimer.file-timer-start" })
export class FileTimerStartAction extends SingletonAction<FileTimerStartSettings> {
  override onWillAppear(
    ev: WillAppearEvent<FileTimerStartSettings>,
  ): Promise<void> {
    return this.updateTitle(ev);
  }

  override onDidReceiveSettings(
    ev: DidReceiveSettingsEvent<FileTimerStartSettings>,
  ): Promise<void> {
    return this.updateSettings(ev);
  }

  override async onKeyDown(
    ev: KeyDownEvent<FileTimerStartSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeFileStartSettings(ev.payload.settings);
      await fileTimers.start(
        settings,
        fileTimerIdleTitle(
          settings.displayFormat,
          settings.amount,
          settings.unit,
        ),
        ev.action,
      );
      await ev.action.showOk();
    } catch (error) {
      logger.error("Unable to start file timer.", error);
      await ev.action.showAlert();
    }
  }

  private async updateSettings(
    ev: DidReceiveSettingsEvent<FileTimerStartSettings>,
  ): Promise<void> {
    await this.updateTitle(ev);
    try {
      await streamDeck.ui.sendToPropertyInspector({
        event: "file-output-path",
        path: getFileOutputPath(
          normalizeFileStartSettings(ev.payload.settings),
        ),
      });
    } catch (error) {
      logger.warn("Unable to resolve file output path.", error);
    }
  }

  private async updateTitle(
    ev:
      | WillAppearEvent<FileTimerStartSettings>
      | DidReceiveSettingsEvent<FileTimerStartSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeFileStartSettings(ev.payload.settings);
      const outputPath = getFileOutputPath(settings);
      if (!fileTimers.isRunning(outputPath)) {
        await ev.action.setTitle(
          fileTimerIdleTitle(
            settings.displayFormat,
            settings.amount,
            settings.unit,
          ),
        );
      }
    } catch (error) {
      logger.warn("Invalid File Timer Start settings.", error);
      await ev.action.setTitle("Configure");
    }
  }
}
