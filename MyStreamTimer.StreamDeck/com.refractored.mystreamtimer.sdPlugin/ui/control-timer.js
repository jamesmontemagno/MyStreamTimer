const target = document.querySelector("#target");
const operation = document.querySelector("#operation");

function setVisible(id, visible) {
  document.querySelector(id).style.display = visible ? "" : "none";
}

// Assigning value emits sdpi-components' "valuechange" and persists the setting.
function setOperation(value) {
  if (operation.value !== value) {
    operation.value = value;
  }
}

function updateFields() {
  const isTime = (target.value ?? "countdown") === "time";
  const operationValue = operation.value ?? "pause";
  if (isTime && operationValue !== "start" && operationValue !== "stop") {
    setOperation("stop");
  } else if (!isTime && operationValue === "start") {
    setOperation("stop");
  }

  const effectiveOperation = operation.value ?? "pause";
  const usesAmount =
    effectiveOperation === "add" || effectiveOperation === "subtract";
  setVisible("#amount-item", usesAmount);
  setVisible("#unit-item", usesAmount);
}

customElements.whenDefined("sdpi-select").then(() => {
  target.addEventListener("valuechange", updateFields);
  operation.addEventListener("valuechange", updateFields);
  updateFields();
});
