import streamDeck from "@elgato/streamdeck";

import { FileTimerManager } from "./file-timer";

export const fileTimers = new FileTimerManager((error) => {
  streamDeck.logger.error("File timer failed while writing output.", error);
});
