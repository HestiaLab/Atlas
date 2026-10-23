import assert from "node:assert/strict";
import test from "node:test";

import {
  feasibleRange,
  resolveSplit,
  type ResolveBasis,
  type ResolveConstraints
} from "../ClientScripts/split-geometry";
import geometryFixture from "../../../tests/fixtures/split-geometry/split-geometry-v1.json";

interface FixtureCase {
  readonly name: string;
  readonly availableExtent: number;
  readonly basis: {
    readonly kind: "proportional" | "fixed-pixels";
    readonly value: number;
    readonly anchor: "First" | "Second" | null;
  };
  readonly constraints: {
    readonly firstMinimum: number;
    readonly firstMaximum: number | null;
    readonly secondMinimum: number;
    readonly secondMaximum: number | null;
  };
  readonly expected: {
    readonly firstExtent: number;
    readonly secondExtent: number;
    readonly isClamped: boolean;
  };
  readonly expectedRange: {
    readonly minimum: number;
    readonly maximum: number;
  };
}

const fixture = geometryFixture as { cases: FixtureCase[] };
const tolerance = 1e-6;

function assertNear(actual: number, expected: number, label: string): void {
  assert.ok(
    Math.abs(actual - expected) < tolerance,
    `${label}: expected ${expected}, got ${actual}`);
}

test("split geometry matches the shared Core golden vectors", () => {
  assert.ok(fixture.cases.length > 0, "the fixture set must not be empty");
  for (const item of fixture.cases) {
    const basis: ResolveBasis = {
      kind: item.basis.kind,
      value: item.basis.value,
      anchor: item.basis.anchor ?? undefined
    };
    const constraints: ResolveConstraints = {
      firstMinimum: item.constraints.firstMinimum,
      firstMaximum: item.constraints.firstMaximum ?? undefined,
      secondMinimum: item.constraints.secondMinimum,
      secondMaximum: item.constraints.secondMaximum ?? undefined
    };

    const resolved = resolveSplit(item.availableExtent, basis, constraints);
    assertNear(resolved.firstExtent, item.expected.firstExtent, `${item.name}.firstExtent`);
    assertNear(resolved.secondExtent, item.expected.secondExtent, `${item.name}.secondExtent`);
    assert.equal(resolved.isClamped, item.expected.isClamped, `${item.name}.isClamped`);

    const range = feasibleRange(item.availableExtent, constraints);
    assertNear(range.minimum, item.expectedRange.minimum, `${item.name}.range.minimum`);
    assertNear(range.maximum, item.expectedRange.maximum, `${item.name}.range.maximum`);
  }
});
