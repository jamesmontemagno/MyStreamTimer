import streamDeck, {
  action,
  type DidReceiveSettingsEvent,
  type KeyDownEvent,
  SingletonAction,
  type WillAppearEvent,
} from "@elgato/streamdeck";

import { NativeCountdownManager } from "../native-countdown";
import { normalizeStartSettings, type StartTimerSettings } from "../settings";
import { buildStartUrl, timerLabel } from "../timer-commands";

const logger = streamDeck.logger.createScope("StartTimer");
const nativeCountdowns = new NativeCountdownManager((error) => {
  logger.error("Native countdown failed while writing output.", error);
});

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
    return this.updateTitle(ev);
  }

  override async onKeyDown(
    ev: KeyDownEvent<StartTimerSettings>,
  ): Promise<void> {
    try {
      const settings = normalizeStartSettings(ev.payload.settings);
      if (settings.backend === "native") {
        const durationSeconds =
          settings.amount * (settings.unit === "seconds" ? 1 : 60);
        await nativeCountdowns.toggle(
          ev.action.id,
          durationSeconds,
          settings.outputDirectory,
          settings.fileName,
          nativeIdleTitle(settings.amount, settings.unit),
          ev.action,
        );
      } else {
        const url = buildStartUrl({
          target: settings.target,
          mode: settings.startMode,
          amount: settings.amount,
          unit: settings.unit,
          clockTime: settings.clockTime,
        });
        await streamDeck.system.openUrl(url);
      }

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
      if (settings.backend === "native") {
        if (!nativeCountdowns.isRunning(ev.action.id)) {
          await ev.action.setTitle(
            nativeIdleTitle(settings.amount, settings.unit),
          );
        }
        return;
      }

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
      logger.warn("Invalid Start Timer settings.", error);
      await ev.action.setTitle("Configure");
    }
  }
}

function formatDuration(amount: number, unit: "minutes" | "seconds"): string {
  return `${amount}${unit === "seconds" ? " sec" : " min"}`;
}

function nativeIdleTitle(amount: number, unit: "minutes" | "seconds"): string {
  return `File\n${formatDuration(amount, unit)}`;
}
