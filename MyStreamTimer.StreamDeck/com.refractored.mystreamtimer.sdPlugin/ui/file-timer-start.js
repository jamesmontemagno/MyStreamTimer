const displayFormat = document.querySelector("#display-format");
const copyOutputPath = document.querySelector("#copy-output-path");
let outputPath;

SDPIComponents.streamDeckClient.sendToPropertyInspector.subscribe((event) => {
  if (event.payload?.event === "file-output-path") {
    outputPath = event.payload.path;
    copyOutputPath.textContent = "Copy output file path";
  }
});

function setVisible(id, visible) {
  document.querySelector(id).style.display = visible ? "" : "none";
}

function updateFields() {
  const usesDuration = displayFormat.value === "countdown";
  setVisible("#amount-item", usesDuration);
  setVisible("#unit-item", usesDuration);
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
  copyOutputPath.addEventListener("click", copyOutputFilePath);
  updateFields();
});
