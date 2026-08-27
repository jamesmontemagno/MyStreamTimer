const target = document.querySelector("#target");
const operation = document.querySelector("#operation");

function setVisible(id, visible) {
  document.querySelector(id).style.display = visible ? "" : "none";
}

function setOperation(value) {
  if (operation.value !== value) {
    operation.value = value;
    operation.dispatchEvent(new Event("change", { bubbles: true }));
  }
}

function updateFields() {
  const isTime = target.value === "time";
  if (isTime && operation.value !== "start" && operation.value !== "stop") {
    setOperation("stop");
  } else if (!isTime && operation.value === "start") {
    setOperation("stop");
  }

  const usesAmount =
    operation.value === "add" || operation.value === "subtract";
  setVisible("#amount-item", usesAmount);
  setVisible("#unit-item", usesAmount);
}

customElements.whenDefined("sdpi-select").then(() => {
  target.addEventListener("change", updateFields);
  operation.addEventListener("change", updateFields);
  updateFields();
});
