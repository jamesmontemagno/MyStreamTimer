import streamDeck, {
  action,
  type DidReceiveSettingsEvent,
  type KeyDownEvent,
  SingletonAction,
  type WillAppearEvent,
} from "@elgato/streamdeck";

import { normalizeStartSettings, type StartTimerSettings } from "../settings";
import { buildStartUrl, timerLabel } from "../timer-commands";

const logger = streamDeck.logger.createScope("AppTimerStart");

@action({ UUID: "com.refractored.mystreamtimer.start-timer" })
export class StartTimerAction extends SingletonAction<StartTimerSettings> {
  override onWillAppear(
    ev: WillAppearEvent<StartTimerSettings>,
  ): Promise<void> {
    return this.updateTitle(ev);
  }

  override onDidReceiveSettings(
    ev: DidReceiveSettingsEvent<StartTimerSettings>,
  ): Promise<void> {
    return this.updateSettings(ev);
  }

  override async onKeyDown(
    ev: KeyDownEvent<StartTimerSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeStartSettings(ev.payload.settings);
      const url = buildStartUrl({
        target: settings.target,
        mode: settings.startMode,
        amount: settings.amount,
        unit: settings.unit,
        clockTime: settings.clockTime,
      });
      await streamDeck.system.openUrl(url);
      await ev.action.showOk();
    } catch (error) {
      logger.error("Unable to start timer.", error);
      await ev.action.showAlert();
    }
  }

  private async updateTitle(
    ev:
      | WillAppearEvent<StartTimerSettings>
      | DidReceiveSettingsEvent<StartTimerSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeStartSettings(ev.payload.settings);
      const mode =
        settings.target === "time"
          ? "Start"
          : settings.startMode === "top-of-hour"
            ? "Top Hour"
            : settings.startMode === "clock-time"
              ? settings.clockTime
              : formatDuration(settings.amount, settings.unit);
      await ev.action.setTitle(`${timerLabel(settings.target)}\n${mode}`);
    } catch (error) {
      logger.warn("Invalid App Timer Start settings.", error);
      await ev.action.setTitle("Configure");
    }
  }

  private async updateSettings(
    ev: DidReceiveSettingsEvent<StartTimerSettings>,
  ): Promise<void> {
    await this.updateTitle(ev);
  }
}

function formatDuration(amount: number, unit: "minutes" | "seconds"): string {
  return `${amount}${unit === "seconds" ? " sec" : " min"}`;
}
