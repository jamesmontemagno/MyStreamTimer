import streamDeck from "@elgato/streamdeck";

import { ControlTimerAction } from "./actions/control-timer";
import { FileTimerControlAction } from "./actions/file-timer-control";
import { FileTimerStartAction } from "./actions/file-timer-start";
import { StartTimerAction } from "./actions/start-timer";

streamDeck.logger.setLevel("info");
streamDeck.actions.registerAction(new StartTimerAction());
streamDeck.actions.registerAction(new ControlTimerAction());
streamDeck.actions.registerAction(new FileTimerStartAction());
streamDeck.actions.registerAction(new FileTimerControlAction());
void streamDeck.connect();
