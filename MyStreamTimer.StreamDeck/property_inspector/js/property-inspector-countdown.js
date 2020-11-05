// global websocket, used to communicate from/to Stream Deck software
// as well as some info about our plugin, as sent by Stream Deck software 
var websocket = null,
    uuid = null,
    inInfo = null,
    actionInfo = {},
    settingsModel = {
        Minutes: 5,
        Seconds: 0,
        FileName: "countdown.txt"
    };

function connectElgatoStreamDeckSocket(inPort, inUUID, inRegisterEvent, inInfo, inActionInfo) {
    uuid = inUUID;
    actionInfo = JSON.parse(inActionInfo);
    inInfo = JSON.parse(inInfo);
    websocket = new WebSocket('ws://localhost:' + inPort);

    //initialize values
    if (actionInfo.payload.settings.settingsModel) {
        settingsModel.Minutes = actionInfo.payload.settings.settingsModel.Minutes;
        settingsModel.Seconds = actionInfo.payload.settings.settingsModel.Seconds;
        settingsModel.FileName = actionInfo.payload.settings.settingsModel.FileName;
    }

    document.getElementById('txtMinutesValue').value = settingsModel.Minutes;
    document.getElementById('txtSecondsValue').value = settingsModel.Seconds;
    document.getElementById('txtFileNameValue').value = settingsModel.FileName;

    websocket.onopen = function () {
        var json = { event: inRegisterEvent, uuid: inUUID };
        // register property inspector to Stream Deck
        websocket.send(JSON.stringify(json));

    };

    websocket.onmessage = function (evt) {
        // Received message from Stream Deck
        var jsonObj = JSON.parse(evt.data);
        var sdEvent = jsonObj['event'];
        switch (sdEvent) {
            case "didReceiveSettings":
                if (jsonObj.payload.settings.settingsModel.Minutes) {
                    settingsModel.Minutes = jsonObj.payload.settings.settingsModel.Minutes;
                    document.getElementById('txtMinutesValue').value = settingsModel.Minutes;
                }

                if (jsonObj.payload.settings.settingsModel.Seconds) {
                    settingsModel.Seconds = jsonObj.payload.settings.settingsModel.Seconds;
                    document.getElementById('txtSecondsValue').value = settingsModel.Seconds;
                }

                if (jsonObj.payload.settings.settingsModel.FileName) {
                    settingsModel.FileName = jsonObj.payload.settings.settingsModel.FileName;
                    document.getElementById('txtFileNameValue').value = settingsModel.FileName;
                }
                break;
            default:
                break;
        }
    };
}

const setSettings = (value, param) => {
    if (websocket) {
        settingsModel[param] = value;
        var json = {
            "event": "setSettings",
            "context": uuid,
            "payload": {
                "settingsModel": settingsModel
            }
        };
        websocket.send(JSON.stringify(json));
    }
};

