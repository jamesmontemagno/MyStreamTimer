const target = document.querySelector("#target");
const startMode = document.querySelector("#start-mode");

function setVisible(id, visible) {
  document.querySelector(id).style.display = visible ? "" : "none";
}

function setSelectValue(element, value) {
  if (element.value !== value) {
    element.value = value;
    element.dispatchEvent(new Event("change", { bubbles: true }));
  }
}

function updateFields() {
  const isTime = target.value === "time";
  const isCountUp = target.value === "countup" || target.value === "countup2";

  if (isTime) {
    setSelectValue(startMode, "current-time");
  } else if (
    isCountUp &&
    (startMode.value === "clock-time" ||
      startMode.value === "top-of-hour" ||
      startMode.value === "current-time")
  ) {
    setSelectValue(startMode, "duration");
  } else if (startMode.value === "current-time") {
    setSelectValue(startMode, "duration");
  }

  const usesDuration = !isTime && startMode.value === "duration";
  setVisible("#mode-item", !isTime);
  setVisible("#amount-item", usesDuration);
  setVisible("#unit-item", usesDuration);
  setVisible("#clock-item", !isTime && startMode.value === "clock-time");
}

Promise.all([
  customElements.whenDefined("sdpi-select"),
  customElements.whenDefined("sdpi-textfield"),
]).then(() => {
  target.addEventListener("change", updateFields);
  startMode.addEventListener("change", updateFields);
  updateFields();
});
