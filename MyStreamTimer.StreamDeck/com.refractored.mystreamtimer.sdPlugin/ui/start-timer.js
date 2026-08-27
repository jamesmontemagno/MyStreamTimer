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
  const modeValue = startMode.value ?? "duration";
  const isTime = targetValue === "time";
  const isCountUp = targetValue === "countup" || targetValue === "countup2";

  if (isTime) {
    setSelectValue(startMode, "current-time");
  } else if (
    isCountUp &&
    (modeValue === "clock-time" ||
      modeValue === "top-of-hour" ||
      modeValue === "current-time")
  ) {
    setSelectValue(startMode, "duration");
  } else if (modeValue === "current-time") {
    setSelectValue(startMode, "duration");
  }

  const effectiveMode = startMode.value ?? "duration";
  const usesDuration = !isTime && effectiveMode === "duration";
  setVisible("#mode-item", !isTime);
  setVisible("#amount-item", usesDuration);
  setVisible("#unit-item", usesDuration);
  setVisible("#clock-item", !isTime && effectiveMode === "clock-time");
}

Promise.all([
  customElements.whenDefined("sdpi-select"),
  customElements.whenDefined("sdpi-textfield"),
]).then(() => {
  target.addEventListener("valuechange", updateFields);
  startMode.addEventListener("valuechange", updateFields);
  updateFields();
});
