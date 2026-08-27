import streamDeck, {
  action,
  type DidReceiveSettingsEvent,
  type KeyDownEvent,
  type SendToPluginEvent,
  SingletonAction,
  type WillAppearEvent,
} from "@elgato/streamdeck";

import { fileTimers } from "../file-timer-service";
import {
  normalizeFileControlSettings,
  type FileTimerControlSettings,
} from "../settings";
import {
  isOutputPathRequest,
  type PluginMessage,
  sendOutputPath,
} from "./file-output-path";

const logger = streamDeck.logger.createScope("StreamDeckControl");

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

  override async onSendToPlugin(
    ev: SendToPluginEvent<PluginMessage, FileTimerControlSettings>,
  ): Promise<void> {
    if (isOutputPathRequest(ev.payload)) {
      await sendOutputPath(
        logger,
        await ev.action.getSettings(),
        normalizeFileControlSettings,
      );
    }
  }

  override async onKeyDown(
    ev: KeyDownEvent<FileTimerControlSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeFileControlSettings(ev.payload.settings);
      await fileTimers.control(settings, ev.action, controlTitle(settings));
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
    await sendOutputPath(
      logger,
      ev.payload.settings,
      normalizeFileControlSettings,
    );
  }

  private async updateTitle(
    ev:
      | WillAppearEvent<FileTimerControlSettings>
      | DidReceiveSettingsEvent<FileTimerControlSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeFileControlSettings(ev.payload.settings);
      await ev.action.setTitle(controlTitle(settings));
    } catch (error) {
      logger.warn("Invalid Stream Deck Control settings.", error);
      await ev.action.setTitle("Configure");
    }
  }
}

function controlTitle(settings: { operation: string }): string {
  return `File\n${capitalize(settings.operation)}`;
}

function capitalize(value: string): string {
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}
