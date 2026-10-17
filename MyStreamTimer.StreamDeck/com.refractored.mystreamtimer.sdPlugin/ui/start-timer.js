const target = document.querySelector("#target");
const startMode = document.querySelector("#start-mode");

function setVisible(id, visible) {
  document.querySelector(id).style.display = visible ? "" : "none";
}

// Assigning value emits sdpi-components' "valuechange" and persists the setting.
function setSelectValue(element, value) {
  if (element.value !== value) {
    element.value = value;
  }
}

function updateFields() {
  const targetValue = target.value ?? "countdown";
  const isCountdown = targetValue.startsWith("countdown");

  // Start mode only applies to countdowns; count-ups use a start-at offset.
  if (!isCountdown) {
    setSelectValue(startMode, "duration");
  }

  const effectiveMode = isCountdown
    ? (startMode.value ?? "duration")
    : "duration";
  const usesDuration = isCountdown && effectiveMode === "duration";
  const usesStartAt = targetValue.startsWith("countup");
  setVisible("#mode-item", isCountdown);
  setVisible("#amount-item", usesDuration);
  setVisible("#start-at-item", usesStartAt);
  setVisible("#unit-item", usesDuration || usesStartAt);
  setVisible("#clock-item", isCountdown && effectiveMode === "clock-time");
}

async function migrateLegacyStartAt() {
  const client = SDPIComponents.streamDeckClient;
  const settings = await client.getSettings();
  const isCountUp =
    settings.target === "countup" || settings.target === "countup2";

  if (
    isCountUp &&
    settings.startAt === undefined &&
    settings.amount !== undefined
  ) {
    await client.setSettings({ ...settings, startAt: settings.amount });
  }
}

Promise.all([
  customElements.whenDefined("sdpi-select"),
  customElements.whenDefined("sdpi-textfield"),
]).then(() => {
  target.addEventListener("valuechange", updateFields);
  startMode.addEventListener("valuechange", updateFields);
  updateFields();
  void migrateLegacyStartAt();
});
