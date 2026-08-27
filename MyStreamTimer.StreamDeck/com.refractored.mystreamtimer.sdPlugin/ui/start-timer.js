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
  const isTime = targetValue === "time";
  const isCountdown = targetValue.startsWith("countdown");

  // Start mode only applies to countdowns; count-ups always use a duration.
  if (!isCountdown) {
    setSelectValue(startMode, "duration");
  }

  const effectiveMode = isCountdown
    ? (startMode.value ?? "duration")
    : "duration";
  const usesDuration = !isTime && effectiveMode === "duration";
  setVisible("#mode-item", isCountdown);
  setVisible("#amount-item", usesDuration);
  setVisible("#unit-item", usesDuration);
  setVisible("#clock-item", isCountdown && effectiveMode === "clock-time");
}

Promise.all([
  customElements.whenDefined("sdpi-select"),
  customElements.whenDefined("sdpi-textfield"),
]).then(() => {
  target.addEventListener("valuechange", updateFields);
  startMode.addEventListener("valuechange", updateFields);
  updateFields();
});
