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

function setOperation(value) {
  if (operation.value !== value) {
    operation.value = value;
    operation.dispatchEvent(new Event("change", { bubbles: true }));
  }
}

function updateFields() {
  const isCurrentTime = displayFormat.value === "current-time";
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
    operation.value !== "start" &&
    operation.value !== "stop"
  ) {
    setOperation("stop");
  } else if (!isCurrentTime && operation.value === "start") {
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
  displayFormat.addEventListener("change", updateFields);
  operation.addEventListener("change", updateFields);
  copyOutputPath.addEventListener("click", copyOutputFilePath);
  updateFields();
});
