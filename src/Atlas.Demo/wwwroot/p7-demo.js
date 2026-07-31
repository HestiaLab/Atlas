document.addEventListener("click", event => {
  const target = event.target;
  if (!(target instanceof Element)) {
    return;
  }

  const frame = document.querySelector("iframe[data-atlas-sandbox-frame]");
  if (!(frame instanceof HTMLIFrameElement) || frame.contentWindow === null) {
    return;
  }

  if (target.closest("[data-testid=p7-send-unknown]")) {
    frame.contentWindow.postMessage("send-unknown", "*");
  } else if (target.closest("[data-testid=p7-send-oversized]")) {
    frame.contentWindow.postMessage("send-oversized", "*");
  }
});
