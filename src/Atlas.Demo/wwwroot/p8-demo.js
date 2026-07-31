window.atlasP8 = {
  beginPanelMeasurement(expectedCount) {
    const startedAt = performance.now();
    const observer = new MutationObserver(() => finish());
    const finish = () => {
      const evidence = document.querySelector("#p8-performance-evidence");
      const host = document.querySelector("[data-testid=p8-performance-host]");
      const count = host?.querySelectorAll("[data-atlas-toolbar-entry]").length ?? 0;
      if (!evidence || count < expectedCount) {
        return;
      }

      const elapsed = performance.now() - startedAt;
      evidence.dataset.panelCount = String(count);
      evidence.dataset.initialRenderMs = elapsed.toFixed(3);
      evidence.dataset.initialRenderPass = String(elapsed <= 500);
      observer.disconnect();
    };
    observer.observe(document.body, { childList: true, subtree: true });
    requestAnimationFrame(finish);
  }
};
