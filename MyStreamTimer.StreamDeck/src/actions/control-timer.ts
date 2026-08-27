import streamDeck, {
  action,
  type DidReceiveSettingsEvent,
  type KeyDownEvent,
  SingletonAction,
  type WillAppearEvent,
} from "@elgato/streamdeck";

import {
  normalizeControlSettings,
  type ControlTimerSettings,
} from "../settings";
import { buildControlUrl, timerLabel } from "../timer-commands";

const logger = streamDeck.logger.createScope("ControlTimer");

@action({ UUID: "com.refractored.mystreamtimer.control-timer" })
export class ControlTimerAction extends SingletonAction<ControlTimerSettings> {
  override onWillAppear(
    ev: WillAppearEvent<ControlTimerSettings>,
  ): Promise<void> {
    return this.updateTitle(ev);
  }

  override onDidReceiveSettings(
    ev: DidReceiveSettingsEvent<ControlTimerSettings>,
  ): Promise<void> {
    return this.updateTitle(ev);
  }

  override async onKeyDown(
    ev: KeyDownEvent<ControlTimerSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeControlSettings(ev.payload.settings);
      await streamDeck.system.openUrl(buildControlUrl(settings));
      await ev.action.showOk();
    } catch (error) {
      logger.error("Unable to control timer.", error);
      await ev.action.showAlert();
    }
  }

  private async updateTitle(
    ev:
      | WillAppearEvent<ControlTimerSettings>
      | DidReceiveSettingsEvent<ControlTimerSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeControlSettings(ev.payload.settings);
      const operation =
        settings.operation === "add" || settings.operation === "subtract"
          ? `${settings.operation === "add" ? "+" : "-"}${settings.amount}${settings.unit === "seconds" ? "s" : "m"}`
          : capitalize(settings.operation);
      await ev.action.setTitle(`${timerLabel(settings.target)}\n${operation}`);
    } catch (error) {
      logger.warn("Invalid Control Timer settings.", error);
      await ev.action.setTitle("Configure");
    }
  }
}

function capitalize(value: string): string {
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}
