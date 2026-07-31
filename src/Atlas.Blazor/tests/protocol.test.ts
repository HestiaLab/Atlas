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
import sandboxFixture from "../../../tests/fixtures/sandbox-protocol-v1.json";
import {
  ATLAS_SANDBOX_PROTOCOL_VERSION,
  validateSandboxMessage
} from "../ClientScripts/sandbox";

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
  const dock = protocolFixture.dockItemCommit as AtlasInteractionMessage;
  const tabReorder = protocolFixture.tabReorderCommit as AtlasInteractionMessage;
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
  assert.equal(dock.type, "dock-item");
  assert.equal(dock.type === "dock-item" ? dock.hostId : "", "host-fixture");
  assert.equal(
    tabReorder.type === "dock-item" ? tabReorder.insertionIndex : -1,
    2);
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

test("shared sandbox protocol fixture accepts only the bound opaque frame", () => {
  const frameWindow = {} as Window;
  const context = {
    workspaceId: "workspace-fixture",
    itemId: "sandbox-item-fixture",
    generation: 3,
    maxMessageBytes: sandboxFixture.maxMessageBytes
  };
  const accepted = validateSandboxMessage(
    {
      data: sandboxFixture.readyMessage,
      origin: "null",
      source: frameWindow
    },
    frameWindow,
    context);

  assert.equal(ATLAS_SANDBOX_PROTOCOL_VERSION, sandboxFixture.protocolVersion);
  assert.equal(accepted.accepted, true);
  assert.equal(accepted.accepted ? accepted.message.type : "", "atlas-sandbox-ready");
});

test("sandbox protocol fails closed for source origin identity version command and size", () => {
  const frameWindow = {} as Window;
  const base = sandboxFixture.readyMessage;
  const context = {
    workspaceId: base.workspaceId,
    itemId: base.itemId,
    generation: base.generation,
    maxMessageBytes: 256
  };
  const reject = (data: unknown, origin = "null", source: MessageEventSource | null = frameWindow) =>
    validateSandboxMessage({ data, origin, source }, frameWindow, context);

  assert.deepEqual(reject(base, "https://example.test"), {
    accepted: false, reason: "origin"
  });
  assert.deepEqual(reject(base, "null", {} as Window), {
    accepted: false, reason: "source"
  });
  assert.deepEqual(reject({ ...base, protocolVersion: 2 }), {
    accepted: false, reason: "version"
  });
  assert.deepEqual(reject({ ...base, workspaceId: "other" }), {
    accepted: false, reason: "identity"
  });
  assert.deepEqual(reject({ ...base, generation: 2 }), {
    accepted: false, reason: "stale"
  });
  assert.deepEqual(reject({ ...base, type: "unknown-command" }), {
    accepted: false, reason: "command"
  });
  assert.deepEqual(reject({ ...base, padding: "x".repeat(512) }), {
    accepted: false, reason: "size"
  });
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

class FakeDocument extends EventTarget {
  public defaultView = null;
  public hidden = false;
  public activeElement: FakeElement | null = null;
  public pointElement: FakeElement | null = null;

  public elementFromPoint(_x: number, _y: number): FakeElement | null {
    return this.pointElement;
  }
}

class FakeElement extends EventTarget {
  public readonly dataset: Record<string, string> = {};
  public readonly style = new FakeStyle();
  public parentElement: FakeElement | null = null;
  public capturedPointer?: number;
  public releasedPointer?: number;
  public editable = false;
  public ownerDocument?: FakeDocument;
  public rect = { left: 0, top: 0, width: 100, height: 100 };
  public rectReads = 0;

  public closest<T extends Element>(selector: string): T | null {
    if (selector === "[data-atlas-split-id]") {
      return this.dataset.atlasSplitId ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-collapse-child]") {
      return this.dataset.atlasCollapseChild ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-drag-kind]") {
      return this.dataset.atlasDragKind ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-group-id]") {
      return this.dataset.atlasGroupId ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-drag-kind=document-tab]") {
      return this.dataset.atlasDragKind === "document-tab"
        ? this as unknown as T
        : null;
    }

    if (selector === "[data-atlas-group-kind=document][data-atlas-group-id]") {
      return this.dataset.atlasGroupKind === "document" && this.dataset.atlasGroupId
        ? this as unknown as T
        : null;
    }

    if (selector === "[data-atlas-panel-header]") {
      return this.dataset.atlasPanelHeader ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-toolbar-entry]") {
      return this.dataset.atlasToolbarEntry ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-toolbar-region]") {
      return this.dataset.atlasToolbarRegion ? this as unknown as T : null;
    }

    if (selector === "[data-atlas-drop-scaffold]") {
      return this.dataset.atlasDropScaffold ? this as unknown as T : null;
    }

    if (selector === ".atlas-v2-workspace") {
      return this.dataset.atlasWorkspaceId ? this as unknown as T : null;
    }

    if (selector.startsWith("input,")) {
      return this.editable ? this as unknown as T : null;
    }

    return null;
  }

  public querySelector<T extends Element>(_selector: string): T | null {
    return this as unknown as T;
  }

  public querySelectorAll<T extends Element>(selector: string): T[] {
    const documentGroup = selector === "[data-atlas-group-kind=document][data-atlas-group-id]"
      && this.dataset.atlasGroupKind === "document"
      && this.dataset.atlasGroupId;
    const documentTab = selector === "[data-atlas-drag-kind=document-tab][data-atlas-insertion-index]"
      && this.dataset.atlasDragKind === "document-tab"
      && this.dataset.atlasInsertionIndex;
    return documentGroup || documentTab
      ? [this as unknown as T]
      : [];
  }

  public getBoundingClientRect(): DOMRect {
    this.rectReads += 1;
    return this.rect as DOMRect;
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

function dragFixture(kind: "document-tab" | "panel-header"): {
  controller: AtlasInteractionController;
  root: FakeElement;
  messages: AtlasInteractionMessage[];
} {
  const root = new FakeElement();
  root.dataset.atlasHostId = "drag-host";
  root.dataset.atlasWorkspaceId = "drag-workspace";
  root.dataset.atlasRevision = "12";
  root.dataset.atlasDragKind = kind;
  root.dataset.atlasItemId = kind === "document-tab" ? "document" : "tool";
  root.dataset.atlasGroupId = kind === "document-tab" ? "documents" : "tools";
  root.dataset.atlasCommandCapabilities = "2";
  root.dataset.atlasInsertionIndex = "0";
  if (kind === "document-tab") {
    root.dataset.atlasGroupKind = "document";
  } else {
    root.dataset.atlasPanelHeader = "true";
  }

  const messages: AtlasInteractionMessage[] = [];
  const controller = new AtlasInteractionController(
    root as unknown as HTMLElement,
    {
      hostId: "drag-host",
      workspaceId: "drag-workspace",
      generation: 5,
      protocolVersion: ATLAS_PROTOCOL_VERSION
    },
    { invokeMethodAsync: async (_method, message) => { messages.push(message); } });
  return { controller, root, messages };
}

test("document drag crosses threshold previews and commits one host-scoped intent", async () => {
  const fixture = dragFixture("document-tab");
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 31, clientX: 50, clientY: 50, button: 0, isPrimary: true
  }));
  assert.equal(fixture.root.rectReads, 0);
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 31, clientX: 56, clientY: 50
  }));
  const geometryReadsAtStart = fixture.root.rectReads;
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 31, clientX: 58, clientY: 50
  }));
  assert.equal(fixture.messages.length, 0);
  assert.equal(fixture.root.dataset.atlasDropPreview, "document-center");
  assert.equal(fixture.root.rectReads, geometryReadsAtStart);
  fixture.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 31, clientX: 56, clientY: 50
  }));
  await flushCallbacks();

  assert.equal(fixture.messages.length, 1);
  assert.deepEqual(fixture.messages[0], {
    type: "dock-item",
    protocolVersion: 1,
    sessionId: "drag-host:1",
    hostId: "drag-host",
    workspaceId: "drag-workspace",
    generation: 5,
    itemId: "document",
    sourceKind: "document-tab",
    baseRevision: 12,
    targetKind: "document-center",
    targetGroupId: "documents",
    insertionIndex: 1
  });
  assert.equal(fixture.root.dataset.atlasDropPreview, undefined);
  fixture.controller.dispose();
});

test("drag terminal paths cancel without callback and generation update expires session", async () => {
  for (const terminal of ["pointercancel", "lostpointercapture"] as const) {
    const fixture = dragFixture("panel-header");
    fixture.root.dispatchEvent(interactionEvent("pointerdown", {
      pointerId: 41, clientX: 0, clientY: 0, button: 0, isPrimary: true
    }));
    fixture.root.dispatchEvent(interactionEvent("pointermove", {
      pointerId: 41, clientX: 5, clientY: 0
    }));
    fixture.root.dispatchEvent(interactionEvent(terminal, { pointerId: 41 }));
    fixture.root.dispatchEvent(interactionEvent("pointerup", { pointerId: 41 }));
    await flushCallbacks();
    assert.equal(fixture.messages.length, 0);
    fixture.controller.dispose();
  }

  const escaped = dragFixture("document-tab");
  escaped.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 43, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  escaped.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 43, clientX: 5, clientY: 0
  }));
  escaped.root.dispatchEvent(interactionEvent("keydown", {
    code: "Escape", ctrlKey: false, shiftKey: false, altKey: false, metaKey: false
  }));
  escaped.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 43, clientX: 50, clientY: 50
  }));
  await flushCallbacks();
  assert.equal(escaped.messages.length, 0);
  assert.equal(escaped.root.dataset.atlasDropPreview, undefined);
  escaped.controller.dispose();

  const stale = dragFixture("document-tab");
  stale.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 42, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  stale.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 42, clientX: 5, clientY: 0
  }));
  stale.controller.update({
    hostId: "drag-host",
    workspaceId: "drag-workspace",
    generation: 6,
    protocolVersion: ATLAS_PROTOCOL_VERSION
  });
  stale.root.dispatchEvent(interactionEvent("pointerup", { pointerId: 42 }));
  await flushCallbacks();
  assert.equal(stale.messages.length, 0);
  stale.controller.dispose();
});

test("panel drag uses approved header target while threshold click remains untouched", async () => {
  const click = dragFixture("panel-header");
  click.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 50, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  click.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 50, clientX: 3, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(click.messages.length, 0);
  click.controller.dispose();

  const drag = dragFixture("panel-header");
  drag.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 51, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  drag.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 51, clientX: 4, clientY: 0
  }));
  drag.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 51, clientX: 4, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(drag.messages.length, 1);
  assert.deepEqual(drag.messages[0], {
    type: "dock-item",
    protocolVersion: 1,
    sessionId: "drag-host:1",
    hostId: "drag-host",
    workspaceId: "drag-workspace",
    generation: 5,
    itemId: "tool",
    sourceKind: "panel-header",
    baseRevision: 12,
    targetKind: "panel-header-insertion",
    targetGroupId: "tools",
    insertionIndex: 0
  });
  drag.controller.dispose();
});

test("pointerup re-resolves target and rejects a stale preview from another host", async () => {
  const fixture = dragFixture("document-tab");
  const document = new FakeDocument();
  fixture.root.ownerDocument = document;
  document.pointElement = fixture.root;
  fixture.controller.dispose();

  const messages: AtlasInteractionMessage[] = [];
  const controller = new AtlasInteractionController(
    fixture.root as unknown as HTMLElement,
    {
      hostId: "drag-host",
      workspaceId: "drag-workspace",
      generation: 5,
      protocolVersion: ATLAS_PROTOCOL_VERSION
    },
    { invokeMethodAsync: async (_method, message) => { messages.push(message); } });
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 60, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 60, clientX: 5, clientY: 0
  }));
  assert.equal(fixture.root.dataset.atlasDropPreview, "document-center");

  const foreignRoot = new FakeElement();
  foreignRoot.dataset.atlasWorkspaceId = "other-workspace";
  foreignRoot.dataset.atlasGroupKind = "document";
  foreignRoot.dataset.atlasGroupId = "other-documents";
  document.pointElement = foreignRoot;
  fixture.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 60, clientX: 50, clientY: 50
  }));
  await flushCallbacks();

  assert.equal(messages.length, 0);
  assert.equal(fixture.root.dataset.atlasDropPreview, undefined);
  assert.equal(fixture.root.dataset.atlasDragging, undefined);
  controller.dispose();
});
