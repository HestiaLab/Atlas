import assert from "node:assert/strict";
import test from "node:test";

import {
  ATLAS_PROTOCOL_VERSION,
  isValidControllerIdentity,
  isSupportedProtocolVersion
} from "../ClientScripts/protocol";
import { AtlasInteractionController } from "../ClientScripts/controller";
import type {
  AtlasInteractionMessage,
  AtlasDotNetReceiver
} from "../ClientScripts/protocol";
import protocolFixture from "../../../tests/fixtures/interaction-protocol-v1.json";

test("protocol version is explicit and rejects unknown versions", () => {
  assert.equal(ATLAS_PROTOCOL_VERSION, 1);
  assert.equal(isSupportedProtocolVersion(ATLAS_PROTOCOL_VERSION), true);
  assert.equal(isSupportedProtocolVersion(ATLAS_PROTOCOL_VERSION + 1), false);
});

test("shared interaction protocol fixture matches TypeScript v1 union", () => {
  assert.equal(protocolFixture.protocolVersion, ATLAS_PROTOCOL_VERSION);
  assert.equal(
    isValidControllerIdentity(protocolFixture.identity),
    true);
  const resize = protocolFixture.resizeSplitCommit as AtlasInteractionMessage;
  const keyboardResize =
    protocolFixture.keyboardResizeCommit as AtlasInteractionMessage;
  const keyboard =
    protocolFixture.keyboardCommandIntent as AtlasInteractionMessage;
  assert.equal(resize.type, "resize-split");
  assert.equal(
    resize.type === "resize-split" ? resize.baseRevision : -1,
    7);
  assert.equal(
    keyboardResize.type === "resize-split" ? keyboardResize.mode : "",
    "keyboard");
  assert.equal(
    keyboardResize.type === "resize-split"
      ? keyboardResize.baseRevision
      : -1,
    null);
  assert.equal(keyboard.type, "keyboard-command");
});

test("controller identity requires stable host workspace and generation", () => {
  assert.equal(isValidControllerIdentity({
    hostId: "host-1",
    workspaceId: "workspace-1",
    generation: 0,
    protocolVersion: ATLAS_PROTOCOL_VERSION
  }), true);
  assert.equal(isValidControllerIdentity({
    hostId: "",
    workspaceId: "workspace-1",
    generation: 0,
    protocolVersion: ATLAS_PROTOCOL_VERSION
  }), false);
  assert.equal(isValidControllerIdentity({
    hostId: "host-1",
    workspaceId: "",
    generation: -1,
    protocolVersion: ATLAS_PROTOCOL_VERSION
  }), false);
});

class FakeStyle {
  private readonly values = new Map<string, string>();

  public setProperty(name: string, value: string): void {
    this.values.set(name, value);
  }

  public removeProperty(name: string): string {
    const previous = this.values.get(name) ?? "";
    this.values.delete(name);
    return previous;
  }

  public get(name: string): string | undefined {
    return this.values.get(name);
  }
}

class FakeElement extends EventTarget {
  public readonly dataset: Record<string, string> = {};
  public readonly style = new FakeStyle();
  public parentElement: FakeElement | null = null;
  public capturedPointer?: number;
  public releasedPointer?: number;
  public editable = false;

  public closest<T extends Element>(selector: string): T | null {
    if (selector === "[data-atlas-split-id]") {
      return this.dataset.atlasSplitId ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-collapse-child]") {
      return this.dataset.atlasCollapseChild ? this as unknown as T : null;
    }

    if (selector.startsWith("input,")) {
      return this.editable ? this as unknown as T : null;
    }

    return null;
  }

  public querySelector<T extends Element>(_selector: string): T | null {
    return this as unknown as T;
  }

  public getBoundingClientRect(): DOMRect {
    return { width: 100, height: 100 } as DOMRect;
  }

  public setPointerCapture(pointerId: number): void {
    this.capturedPointer = pointerId;
  }

  public releasePointerCapture(pointerId: number): void {
    this.releasedPointer = pointerId;
  }

  public removeAttribute(name: string): void {
    if (name.startsWith("data-")) {
      const property = name
        .slice(5)
        .replace(/-([a-z])/g, (_, character: string) => character.toUpperCase());
      delete this.dataset[property];
    }
  }
}

Object.defineProperty(globalThis, "Element", {
  configurable: true,
  value: FakeElement
});

function interactionEvent(
  type: string,
  values: Record<string, unknown>): Event {
  const event = new Event(type, { cancelable: true });
  for (const [key, value] of Object.entries(values)) {
    Object.defineProperty(event, key, { value });
  }

  return event;
}

function controllerFixture(delay = 1000): {
  controller: AtlasInteractionController;
  splitter: FakeElement;
  split: FakeElement;
  messages: AtlasInteractionMessage[];
} {
  const splitter = new FakeElement();
  const split = new FakeElement();
  splitter.parentElement = split;
  splitter.dataset.atlasSplitId = "root";
  split.dataset.atlasNodeId = "root";
  split.dataset.atlasSplitAxis = "InlineChildren";
  split.dataset.atlasSplitLocked = "false";
  split.dataset.atlasSplitRatio = "0.5";
  const messages: AtlasInteractionMessage[] = [];
  const receiver: AtlasDotNetReceiver = {
    invokeMethodAsync: async (_method, message) => {
      messages.push(message);
    }
  };
  const controller = new AtlasInteractionController(
    splitter as unknown as HTMLElement,
    {
      hostId: "host",
      workspaceId: "workspace",
      generation: 4,
      protocolVersion: ATLAS_PROTOCOL_VERSION
    },
    receiver,
    { keyboardCommitDelayMilliseconds: delay });
  return { controller, splitter, split, messages };
}

async function flushCallbacks(): Promise<void> {
  await new Promise(resolve => setTimeout(resolve, 0));
}

test("pointer preview stays local and pointerup commits exactly once", async () => {
  const fixture = controllerFixture();
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerdown",
    { pointerId: 7, clientX: 10, clientY: 0, button: 0, isPrimary: true }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 7, clientX: 20, clientY: 0 }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 7, clientX: 30, clientY: 0 }));

  assert.equal(fixture.messages.length, 0);
  assert.equal(fixture.split.style.get("--atlas-split-preview"), "0.7");

  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerup",
    { pointerId: 7, clientX: 30, clientY: 0 }));
  await flushCallbacks();

  assert.equal(fixture.messages.length, 1);
  assert.deepEqual(fixture.messages[0], {
    type: "resize-split",
    splitId: "root",
    firstRatio: 0.7,
    mode: "resize",
    generation: 4,
    baseRevision: 0
  });
  fixture.controller.dispose();
});

test("cancel and dispose produce no terminal callback", async () => {
  const fixture = controllerFixture();
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerdown",
    { pointerId: 8, clientX: 10, clientY: 0, button: 0, isPrimary: true }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 8, clientX: 40, clientY: 0 }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointercancel",
    { pointerId: 8, clientX: 40, clientY: 0 }));
  fixture.controller.dispose();
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerup",
    { pointerId: 8, clientX: 40, clientY: 0 }));
  await flushCallbacks();

  assert.equal(fixture.messages.length, 0);
  assert.equal(fixture.split.style.get("--atlas-split-preview"), undefined);
});

test("each splitter key step commits and double click requests equalize", async () => {
  const fixture = controllerFixture();
  fixture.splitter.dispatchEvent(interactionEvent("keydown", {
    code: "ArrowRight",
    ctrlKey: false,
    shiftKey: false,
    altKey: false
  }));
  fixture.splitter.dispatchEvent(interactionEvent("keydown", {
    code: "ArrowRight",
    ctrlKey: false,
    shiftKey: false,
    altKey: false
  }));
  fixture.splitter.dispatchEvent(interactionEvent("keyup", {
    code: "ArrowRight"
  }));
  await flushCallbacks();

  assert.equal(fixture.messages.length, 2);
  assert.equal(fixture.messages[0]?.type, "resize-split");
  assert.equal(
    fixture.messages[0]?.type === "resize-split"
      ? fixture.messages[0].firstRatio
      : 0,
    0.6);
  assert.deepEqual(fixture.messages[1], {
    type: "resize-split",
    splitId: "root",
    firstRatio: 0.7,
    mode: "keyboard",
    generation: 4,
    baseRevision: null
  });

  fixture.splitter.dispatchEvent(interactionEvent("dblclick", {}));
  await flushCallbacks();
  assert.equal(fixture.messages.length, 3);
  assert.deepEqual(fixture.messages[2], {
    type: "resize-split",
    splitId: "root",
    firstRatio: 0.5,
    mode: "equalize",
    generation: 4,
    baseRevision: 0
  });
  fixture.controller.dispose();
});

test("locked splitter rejects pointer input and constraints preserve hysteresis", async () => {
  const locked = controllerFixture();
  locked.split.dataset.atlasSplitLocked = "true";
  locked.splitter.dispatchEvent(interactionEvent(
    "pointerdown",
    { pointerId: 9, clientX: 10, clientY: 0, button: 0, isPrimary: true }));
  locked.splitter.dispatchEvent(interactionEvent(
    "pointerup",
    { pointerId: 9, clientX: 40, clientY: 0 }));
  await flushCallbacks();
  assert.equal(locked.messages.length, 0);
  assert.equal(locked.splitter.capturedPointer, undefined);
  locked.controller.dispose();

  const constrained = controllerFixture();
  constrained.split.dataset.atlasFirstMinimum = "40";
  constrained.splitter.dispatchEvent(interactionEvent(
    "pointerdown",
    { pointerId: 10, clientX: 10, clientY: 0, button: 0, isPrimary: true }));
  constrained.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 10, clientX: -40, clientY: 0 }));
  assert.equal(constrained.split.style.get("--atlas-split-preview"), "0.4");
  constrained.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 10, clientX: -30, clientY: 0 }));
  assert.equal(constrained.split.style.get("--atlas-split-preview"), "0.4");
  constrained.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 10, clientX: 5, clientY: 0 }));
  assert.equal(constrained.split.style.get("--atlas-split-preview"), "0.45");
  constrained.controller.dispose();
});

test("fixed-pixel basis resolves against current extent", async () => {
  const fixture = controllerFixture();
  fixture.split.dataset.atlasSplitBasisKind = "FixedPixels";
  fixture.split.dataset.atlasSplitBasisValue = "25";
  fixture.split.dataset.atlasSplitAnchor = "First";
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerdown",
    { pointerId: 11, clientX: 10, clientY: 0, button: 0, isPrimary: true }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 11, clientX: 20, clientY: 0 }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerup",
    { pointerId: 11, clientX: 20, clientY: 0 }));
  await flushCallbacks();

  const message = fixture.messages[0];
  assert.equal(
    message?.type === "resize-split" ? message.firstRatio : 0,
    0.35);
  fixture.controller.dispose();
});

test("root keyboard map respects editable guard and global override", async () => {
  const root = new FakeElement();
  const messages: AtlasInteractionMessage[] = [];
  const receiver: AtlasDotNetReceiver = {
    invokeMethodAsync: async (_method, message) => {
      messages.push(message);
    }
  };
  const identity = {
    hostId: "keyboard-host",
    workspaceId: "keyboard-workspace",
    generation: 2,
    protocolVersion: ATLAS_PROTOCOL_VERSION
  } as const;
  const controller = new AtlasInteractionController(
    root as unknown as HTMLElement,
    identity,
    receiver);
  root.editable = true;
  root.dispatchEvent(interactionEvent("keydown", {
    code: "Tab",
    ctrlKey: true,
    shiftKey: false,
    altKey: false
  }));
  await flushCallbacks();
  assert.equal(messages.length, 0);
  controller.update(identity, {
    keyboardBindings: [{
      code: "Tab",
      control: true,
      shift: false,
      alt: false,
      meta: false,
      command: "next-tab",
      isGlobal: true
    }]
  });
  root.dispatchEvent(interactionEvent("keydown", {
    code: "Tab",
    ctrlKey: true,
    shiftKey: false,
    altKey: false
  }));
  await flushCallbacks();
  assert.deepEqual(messages, [{
    type: "keyboard-command",
    command: "next-tab",
    generation: 2
  }]);
  controller.dispose();
});

test("invalid controller options are rejected", () => {
  const fixture = controllerFixture();
  assert.throws(
    () => fixture.controller.update(
      {
        hostId: "host",
        workspaceId: "workspace",
        generation: 4,
        protocolVersion: ATLAS_PROTOCOL_VERSION
      },
      { keyboardStepPixels: 0 }),
    /ATLAS-INTERACTION-PROTOCOL-INVALID/);
  fixture.controller.dispose();
});
