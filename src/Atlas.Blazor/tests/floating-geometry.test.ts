import assert from "node:assert/strict";
import test from "node:test";
import {
  ATLAS_FLOATING_DEFAULT_HEIGHT,
  ATLAS_FLOATING_DEFAULT_WIDTH,
  ATLAS_FLOATING_MIN_HEIGHT,
  ATLAS_FLOATING_MIN_WIDTH,
  clampFloatingGeometry,
  resizeFloatingGeometry,
  rootRelativeGeometry
} from "../ClientScripts/floating-geometry";

test("root relative geometry subtracts the host origin", () => {
  assert.deepEqual(
    rootRelativeGeometry(
      { left: 10, top: 20, width: 800, height: 600 },
      { left: 60, top: 70, width: 360, height: 480 }),
    { x: 50, y: 50, width: 360, height: 480 });
});

test("clamp keeps the default size inside the host", () => {
  assert.deepEqual(
    clampFloatingGeometry(
      {
        x: 900,
        y: -40,
        width: ATLAS_FLOATING_DEFAULT_WIDTH,
        height: ATLAS_FLOATING_DEFAULT_HEIGHT
      },
      { width: 800, height: 600 }),
    {
      x: 440,
      y: 0,
      width: ATLAS_FLOATING_DEFAULT_WIDTH,
      height: ATLAS_FLOATING_DEFAULT_HEIGHT
    });
});

test("clamp enforces minimum size and finite coordinates", () => {
  assert.deepEqual(
    clampFloatingGeometry(
      { x: Number.NaN, y: Number.POSITIVE_INFINITY, width: 10, height: 10 },
      { width: 800, height: 600 }),
    {
      x: 0,
      y: 0,
      width: ATLAS_FLOATING_MIN_WIDTH,
      height: ATLAS_FLOATING_MIN_HEIGHT
    });
});

test("resize from the south east grows around the fixed top left corner", () => {
  assert.deepEqual(
    resizeFloatingGeometry(
      { x: 100, y: 80, width: 360, height: 480 },
      "se",
      40,
      30,
      { width: 800, height: 600 }),
    { x: 100, y: 80, width: 400, height: 510 });
});

test("resize from the west keeps the opposite edge and minimum width", () => {
  assert.deepEqual(
    resizeFloatingGeometry(
      { x: 100, y: 80, width: 360, height: 480 },
      "w",
      400,
      0,
      { width: 800, height: 600 }),
    {
      x: 100 + 360 - ATLAS_FLOATING_MIN_WIDTH,
      y: 80,
      width: ATLAS_FLOATING_MIN_WIDTH,
      height: 480
    });
});

test("resize from the north clamps to the host boundary", () => {
  assert.deepEqual(
    resizeFloatingGeometry(
      { x: 100, y: 80, width: 360, height: 480 },
      "n",
      0,
      -1000,
      { width: 800, height: 600 }),
    { x: 100, y: 0, width: 360, height: 560 });
});
