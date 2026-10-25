import assert from "node:assert/strict";
import test from "node:test";

import { clampPopupPosition } from "../ClientScripts/popup-position";

const viewport = { width: 800, height: 600 };

test("popup opens below its anchor when there is room", () => {
  const anchor = { left: 100, top: 100, right: 180, bottom: 130, width: 80, height: 30 };
  assert.deepEqual(
    clampPopupPosition(anchor, { width: 140, height: 120 }, viewport),
    { left: 100, top: 134 });
});

test("popup flips above its anchor near the bottom edge", () => {
  const anchor = { left: 100, top: 500, right: 180, bottom: 530, width: 80, height: 30 };
  assert.deepEqual(
    clampPopupPosition(anchor, { width: 140, height: 120 }, viewport),
    { left: 100, top: 376 });
});

test("popup clamps to the right and left viewport edges", () => {
  const right = { left: 700, top: 100, right: 780, bottom: 130, width: 80, height: 30 };
  assert.deepEqual(
    clampPopupPosition(right, { width: 140, height: 120 }, viewport),
    { left: 660, top: 134 });

  const left = { left: 10, top: 100, right: 90, bottom: 130, width: 80, height: 30 };
  assert.deepEqual(
    clampPopupPosition(left, { width: 200, height: 120 }, viewport),
    { left: 10, top: 134 });
});

test("popup clamps to the top and bottom viewport edges", () => {
  const anchor = { left: 100, top: 20, right: 180, bottom: 50, width: 80, height: 30 };
  assert.deepEqual(
    clampPopupPosition(anchor, { width: 140, height: 120 }, viewport),
    { left: 100, top: 54 });

  const bottom = { left: 100, top: 590, right: 180, bottom: 620, width: 80, height: 30 };
  assert.deepEqual(
    clampPopupPosition(bottom, { width: 140, height: 120 }, viewport),
    { left: 100, top: 466 });
});

test("popup larger than a small viewport is pinned to the origin", () => {
  const anchor = { left: 30, top: 40, right: 80, bottom: 60, width: 50, height: 20 };
  assert.deepEqual(
    clampPopupPosition(anchor, { width: 160, height: 140 }, { width: 100, height: 100 }),
    { left: 0, top: 0 });
});
