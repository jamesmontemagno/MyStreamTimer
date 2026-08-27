const displayFormat = document.querySelector("#display-format");
const operation = document.querySelector("#operation");
const copyOutputPath = document.querySelector("#copy-output-path");
let outputPath;

SDPIComponents.streamDeckClient.sendToPropertyInspector.subscribe((event) => {
  if (event.payload?.event === "file-output-path") {
    outputPath = event.payload.path;
    copyOutputPath.textContent = "Copy output file path";
  }
});

// Assigning value emits sdpi-components' "valuechange" and persists the setting.
function setOperation(value) {
  if (operation.value !== value) {
    operation.value = value;
  }
}

function updateFields() {
  const isCurrentTime = (displayFormat.value ?? "countdown") === "current-time";
  const operationValue = operation.value ?? "pause";
  for (const option of operation.querySelectorAll("option")) {
    const isCurrentTimeOperation =
      option.value === "start" || option.value === "stop";
    option.disabled = isCurrentTime
      ? !isCurrentTimeOperation
      : option.value === "start";
    option.hidden = option.disabled;
  }

  if (
    isCurrentTime &&
    operationValue !== "start" &&
    operationValue !== "stop"
  ) {
    setOperation("stop");
  } else if (!isCurrentTime && operationValue === "start") {
    setOperation("stop");
  }
}

async function copyOutputFilePath() {
  if (!outputPath) {
    copyOutputPath.textContent = "Path unavailable";
    return;
  }

  try {
    await navigator.clipboard.writeText(outputPath);
    copyOutputPath.textContent = "Copied!";
  } catch {
    copyOutputPath.textContent = "Copy failed";
  }
}

customElements.whenDefined("sdpi-select").then(() => {
  displayFormat.addEventListener("valuechange", updateFields);
  operation.addEventListener("valuechange", updateFields);
  copyOutputPath.addEventListener("click", copyOutputFilePath);
  updateFields();
  SDPIComponents.streamDeckClient.send("sendToPlugin", {
    event: "request-file-output-path",
  });
});
