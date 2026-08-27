import streamDeck from "@elgato/streamdeck";

import { ControlTimerAction } from "./actions/control-timer";
import { StartTimerAction } from "./actions/start-timer";

streamDeck.logger.setLevel("info");
streamDeck.actions.registerAction(new StartTimerAction());
streamDeck.actions.registerAction(new ControlTimerAction());
void streamDeck.connect();
