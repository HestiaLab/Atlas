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
import {
  attachAtlasController,
  clearAtlasSplitPreview,
  disposeAtlasController,
  updateAtlasController
} from "../ClientScripts/public-api";

test("public api exposes every C#-invokable controller entry point", () => {
  // C# invokes these by name via module.InvokeVoidAsync / InvokeAsync; a
  // missing re-export silently passes bUnit's loose JSRuntime but fails in a
  // real browser ("Could not find '...'").
  assert.equal(typeof attachAtlasController, "function");
  assert.equal(typeof updateAtlasController, "function");
  assert.equal(typeof disposeAtlasController, "function");
  assert.equal(typeof clearAtlasSplitPreview, "function");
});

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
  public defaultView: Window | null = null;
  public hidden = false;
  public activeElement: FakeElement | null = null;
  public pointElement: FakeElement | null = null;
  public readonly body = new FakeElement();

  public constructor() {
    super();
    this.body.ownerDocument = this;
  }

  public createElement(_tagName: string): FakeElement {
    const element = new FakeElement();
    element.ownerDocument = this;
    return element;
  }

  public elementFromPoint(_x: number, _y: number): FakeElement | null {
    return this.pointElement;
  }
}

class FakeMediaQueryList extends EventTarget {
  public matches = false;
  public readonly media = "(prefers-reduced-motion: reduce)";
}

class FakeWindow extends EventTarget {
  public readonly reducedMotion = new FakeMediaQueryList();
  private readonly animationFrames = new Map<number, FrameRequestCallback>();
  private nextAnimationFrameId = 1;

  public matchMedia(_query: string): MediaQueryList {
    return this.reducedMotion as unknown as MediaQueryList;
  }

  public getComputedStyle(_element: Element): CSSStyleDeclaration {
    return { direction: "ltr" } as CSSStyleDeclaration;
  }

  public requestAnimationFrame(callback: FrameRequestCallback): number {
    const id = this.nextAnimationFrameId++;
    this.animationFrames.set(id, callback);
    return id;
  }

  public cancelAnimationFrame(id: number): void {
    this.animationFrames.delete(id);
  }

  public get pendingAnimationFrameCount(): number {
    return this.animationFrames.size;
  }

  public peekAnimationFrame(): FrameRequestCallback | undefined {
    return this.animationFrames.values().next().value;
  }

  public flushAnimationFrames(): void {
    const callbacks = [...this.animationFrames.values()];
    this.animationFrames.clear();
    for (const callback of callbacks) {
      callback(0);
    }
  }
}

class FakeElement extends EventTarget {
  public readonly dataset: Record<string, string> = {};
  public readonly style = new FakeStyle();
  public readonly children: FakeElement[] = [];
  public className = "";
  public textContent: string | null = null;
  public parentElement: FakeElement | null = null;
  public capturedPointer?: number;
  public releasedPointer?: number;
  public editable = false;
  public ownerDocument?: FakeDocument;
  public rect = { left: 0, top: 0, width: 100, height: 100 };
  public rectReads = 0;
  private readonly attributes = new Map<string, string>();

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

    if (selector === "[data-atlas-drop-scaffold=\"true\"]") {
      return this.dataset.atlasDropScaffold === "true" ? this as unknown as T : null;
    }

    if (selector === ".atlas-v2-workspace") {
      return this.dataset.atlasWorkspaceId ? this as unknown as T : null;
    }

    if (selector.startsWith("input,")) {
      return this.editable ? this as unknown as T : null;
    }

    return null;
  }

  public querySelector<T extends Element>(selector: string): T | null {
    if (selector === "[data-atlas-overlay-root=\"true\"]") {
      const findOverlay = (element: FakeElement): FakeElement | undefined => {
        for (const child of element.children) {
          if (child.dataset.atlasOverlayRoot === "true") {
            return child;
          }

          const nested = findOverlay(child);
          if (nested) {
            return nested;
          }
        }

        return undefined;
      };
      return (findOverlay(this) ?? null) as T | null;
    }

    const nodeIdMatch = /^\[data-atlas-node-id="([^"]+)"\]$/.exec(selector);
    if (nodeIdMatch) {
      const find = (element: FakeElement): FakeElement | undefined => {
        for (const child of element.children) {
          if (child.dataset.atlasNodeId === nodeIdMatch[1]) {
            return child;
          }

          const nested = find(child);
          if (nested) {
            return nested;
          }
        }

        return undefined;
      };
      return (find(this) ?? null) as T | null;
    }

    if (selector === ".atlas-v2-item-icon[data-atlas-icon]") {
      return (this.children.find(child =>
        child.className.split(" ").includes("atlas-v2-item-icon")
        && child.dataset.atlasIcon) ?? null) as T | null;
    }

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

  public appendChild<T extends FakeElement>(child: T): T {
    child.parentElement = this;
    this.children.push(child);
    return child;
  }

  public remove(): void {
    const siblings = this.parentElement?.children;
    if (siblings) {
      const index = siblings.indexOf(this);
      if (index >= 0) {
        siblings.splice(index, 1);
      }
    }

    this.parentElement = null;
  }

  public setAttribute(name: string, value: string): void {
    this.attributes.set(name, value);
  }

  public getAttribute(name: string): string | null {
    return this.attributes.get(name) ?? null;
  }

  public removeAttribute(name: string): void {
    if (name.startsWith("data-")) {
      const property = name
        .slice(5)
        .replace(/-([a-z])/g, (_, character: string) => character.toUpperCase());
      delete this.dataset[property];
      return;
    }

    this.attributes.delete(name);
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
  window: FakeWindow;
} {
  const splitter = new FakeElement();
  const split = new FakeElement();
  const ownerWindow = new FakeWindow();
  const ownerDocument = new FakeDocument();
  ownerDocument.defaultView = ownerWindow as unknown as Window;
  splitter.ownerDocument = ownerDocument;
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
  return { controller, splitter, split, messages, window: ownerWindow };
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

test("pointerup keeps the preview in place until Blazor confirms the commit", async () => {
  const fixture = controllerFixture();
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerdown",
    { pointerId: 13, clientX: 10, clientY: 0, button: 0, isPrimary: true }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 13, clientX: 30, clientY: 0 }));
  assert.equal(fixture.split.style.get("--atlas-split-preview"), "0.7");

  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerup",
    { pointerId: 13, clientX: 30, clientY: 0 }));
  await flushCallbacks();

  // The commit is in flight; the preview must stay in place so the split
  // does not bounce back to the pre-drag ratio before Blazor re-renders.
  assert.equal(fixture.messages.length, 1);
  assert.equal(fixture.split.style.get("--atlas-split-preview"), "0.7");
  assert.equal(
    fixture.split.style.get("grid-template-columns"),
    "minmax(0, 0.7fr) var(--atlas-splitter-size) minmax(0, 0.30000000000000004fr)");

  // Blazor confirms the commit via clearAtlasSplitPreview once the result is
  // known; for an applied commit the re-render has already replaced the style
  // attribute, so this clear is a no-op safety net.
  fixture.splitter.children.push(fixture.split);
  fixture.controller.clearSplitPreview("root");
  assert.equal(fixture.split.style.get("--atlas-split-preview"), undefined);
  fixture.controller.dispose();
});

test("clearSplitPreview skips a split being resized and clears after release", async () => {
  const fixture = controllerFixture();
  fixture.splitter.children.push(fixture.split);
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerdown",
    { pointerId: 14, clientX: 10, clientY: 0, button: 0, isPrimary: true }));
  fixture.splitter.dispatchEvent(interactionEvent(
    "pointermove",
    { pointerId: 14, clientX: 30, clientY: 0 }));
  assert.equal(fixture.split.style.get("--atlas-split-preview"), "0.7");

  // A commit notification arriving while the same split is being dragged
  // must not wipe the live preview.
  fixture.controller.clearSplitPreview("root");
  assert.equal(fixture.split.style.get("--atlas-split-preview"), "0.7");

  fixture.splitter.dispatchEvent(interactionEvent(
    "pointerup",
    { pointerId: 14, clientX: 30, clientY: 0 }));
  await flushCallbacks();
  fixture.controller.clearSplitPreview("root");
  assert.equal(fixture.split.style.get("--atlas-split-preview"), undefined);
  fixture.controller.dispose();
});

test("clearSplitPreview ignores an unknown split id", () => {
  const fixture = controllerFixture();
  fixture.splitter.style.setProperty("--atlas-split-preview", "0.5");
  fixture.controller.clearSplitPreview("missing");
  assert.equal(fixture.splitter.style.get("--atlas-split-preview"), "0.5");
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

test("reduced motion follows the host media query and releases its listener", () => {
  const root = new FakeElement();
  const document = new FakeDocument();
  const ownerWindow = new FakeWindow();
  document.defaultView = ownerWindow as unknown as Window;
  root.ownerDocument = document;
  root.dataset.atlasHostId = "motion-host";
  root.dataset.atlasWorkspaceId = "motion-workspace";
  const controller = new AtlasInteractionController(
    root as unknown as HTMLElement,
    {
      hostId: "motion-host",
      workspaceId: "motion-workspace",
      generation: 1,
      protocolVersion: ATLAS_PROTOCOL_VERSION
    },
    { invokeMethodAsync: async () => undefined });

  assert.equal(root.dataset.atlasReducedMotion, "false");
  ownerWindow.reducedMotion.matches = true;
  ownerWindow.reducedMotion.dispatchEvent(new Event("change"));
  assert.equal(root.dataset.atlasReducedMotion, "true");

  controller.dispose();
  ownerWindow.reducedMotion.matches = false;
  ownerWindow.reducedMotion.dispatchEvent(new Event("change"));
  assert.equal(root.dataset.atlasReducedMotion, "true");
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

function dragFixture(
  kind: "document-tab" | "panel-header" | "toolbar-entry",
  useAnimationFrame = false): {
  controller: AtlasInteractionController;
  document: FakeDocument;
  root: FakeElement;
  messages: AtlasInteractionMessage[];
  window: FakeWindow | undefined;
} {
  const document = new FakeDocument();
  const window = useAnimationFrame ? new FakeWindow() : undefined;
  if (window) {
    document.defaultView = window as unknown as Window;
  }
  const root = new FakeElement();
  root.ownerDocument = document;
  root.dataset.atlasHostId = "drag-host";
  root.dataset.atlasWorkspaceId = "drag-workspace";
  root.dataset.atlasRevision = "12";
  root.dataset.atlasDragKind = kind;
  root.dataset.atlasItemId = kind === "document-tab" ? "document" : "tool";
  root.dataset.atlasGroupId = kind === "document-tab"
    ? "documents"
    : kind === "toolbar-entry"
      ? "left-upper"
      : "tools";
  root.dataset.atlasCommandCapabilities = "2";
  root.dataset.atlasInsertionIndex = "0";
  if (kind === "document-tab") {
    root.dataset.atlasGroupKind = "document";
  } else if (kind === "panel-header") {
    root.dataset.atlasPanelHeader = "true";
  } else {
    root.dataset.atlasToolbarEntry = "true";
    root.dataset.atlasToolbarRegion = "InlineStartUpper";
  }
  root.setAttribute("aria-label", kind === "toolbar-entry" ? "Explorer" : "Source");
  const overlayRoot = new FakeElement();
  overlayRoot.ownerDocument = document;
  overlayRoot.className = "atlas-v2-overlay-root";
  overlayRoot.dataset.atlasOverlayRoot = "true";
  overlayRoot.dataset.atlasHostId = "drag-host";
  root.appendChild(overlayRoot);

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
  return { controller, document, root, messages, window };
}

function dragOverlay(fixture: ReturnType<typeof dragFixture>): FakeElement | undefined {
  return fixture.root.children.find(child =>
    child.dataset.atlasOverlayRoot === "true"
    && child.dataset.atlasHostId === "drag-host");
}

function dragPreview(fixture: ReturnType<typeof dragFixture>): FakeElement | undefined {
  return dragOverlay(fixture)?.children.find(
    child => child.className === "atlas-v2-drag-preview");
}

test("toolbar drag preview appears after the threshold, follows the pointer, and cleans up", () => {
  const fixture = dragFixture("toolbar-entry");
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 29, clientX: 10, clientY: 10, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 29, clientX: 13, clientY: 10
  }));
  assert.equal(dragPreview(fixture), undefined);

  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 29, clientX: 20, clientY: 24
  }));
  assert.equal(fixture.document.body.children.length, 0);
  const overlay = dragOverlay(fixture)!;
  assert.equal(overlay.dataset.atlasHostId, "drag-host");
  assert.match(overlay.dataset.atlasDragSessionId ?? "", /^drag-host:\d+$/);
  const preview = dragPreview(fixture)!;
  assert.equal(preview.className, "atlas-v2-drag-preview");
  assert.equal(preview.dataset.atlasDragPreview, "toolbar-entry");
  assert.equal(preview.getAttribute("aria-hidden"), "true");
  assert.equal(preview.children.length, 1);
  assert.equal(preview.children[0]?.className, "atlas-v2-drag-preview-title");
  assert.equal(preview.children[0]?.textContent, "Explorer");
  assert.equal(preview.style.get("transform"), "translate3d(32px, 36px, 0)");

  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 29, clientX: 30, clientY: 40
  }));
  assert.equal(preview.style.get("transform"), "translate3d(42px, 52px, 0)");

  fixture.root.dispatchEvent(interactionEvent("pointercancel", { pointerId: 29 }));
  assert.equal(dragPreview(fixture), undefined);
  assert.equal(overlay.dataset.atlasDragSessionId, undefined);
  fixture.controller.dispose();
});

test("drag preview coalesces animation frames and invalidates a queued frame on cancel", () => {
  const fixture = dragFixture("toolbar-entry", true);
  const window = fixture.window!;
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 32, clientX: 10, clientY: 10, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 32, clientX: 20, clientY: 20
  }));

  const preview = dragPreview(fixture)!;
  assert.equal(preview.style.get("transform"), "translate3d(32px, 32px, 0)");
  assert.equal(window.pendingAnimationFrameCount, 0);

  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 32, clientX: 30, clientY: 40
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 32, clientX: 50, clientY: 60
  }));
  assert.equal(window.pendingAnimationFrameCount, 1);
  assert.equal(preview.style.get("transform"), "translate3d(32px, 32px, 0)");

  window.flushAnimationFrames();
  assert.equal(preview.style.get("transform"), "translate3d(62px, 72px, 0)");

  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 32, clientX: 70, clientY: 80
  }));
  const lateFrame = window.peekAnimationFrame();
  assert.equal(window.pendingAnimationFrameCount, 1);
  fixture.root.dispatchEvent(interactionEvent("pointercancel", { pointerId: 32 }));
  assert.equal(window.pendingAnimationFrameCount, 0);
  lateFrame?.(0);
  assert.equal(dragPreview(fixture), undefined);
  assert.equal(preview.style.get("transform"), "translate3d(62px, 72px, 0)");
  fixture.controller.dispose();
});

test("drag preview ignores nested and foreign-host overlay markers", () => {
  const fixture = dragFixture("toolbar-entry");
  const nestedContainer = new FakeElement();
  const nestedOverlay = new FakeElement();
  nestedOverlay.dataset.atlasOverlayRoot = "true";
  nestedOverlay.dataset.atlasHostId = "drag-host";
  nestedContainer.appendChild(nestedOverlay);
  const foreignOverlay = new FakeElement();
  foreignOverlay.dataset.atlasOverlayRoot = "true";
  foreignOverlay.dataset.atlasHostId = "other-host";
  fixture.root.children.unshift(nestedContainer, foreignOverlay);

  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 33, clientX: 10, clientY: 10, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 33, clientX: 20, clientY: 20
  }));

  assert.equal(nestedOverlay.children.length, 0);
  assert.equal(foreignOverlay.children.length, 0);
  assert.notEqual(dragPreview(fixture), undefined);
  fixture.root.dispatchEvent(interactionEvent("pointercancel", { pointerId: 33 }));
  assert.equal(dragPreview(fixture), undefined);
  fixture.controller.dispose();
});

test("document drag preview preserves the standard source icon", () => {
  const fixture = dragFixture("document-tab");
  const icon = new FakeElement();
  icon.className = "atlas-v2-item-icon";
  icon.dataset.atlasIcon = "file";
  fixture.root.appendChild(icon);

  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 30, clientX: 10, clientY: 10, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 30, clientX: 20, clientY: 20
  }));

  const preview = dragPreview(fixture)!;
  assert.equal(preview.children.length, 2);
  assert.equal(preview.children[0]?.className, "atlas-v2-item-icon");
  assert.equal(preview.children[0]?.dataset.atlasIcon, "file");
  assert.equal(preview.children[1]?.className, "atlas-v2-drag-preview-title");
  assert.equal(preview.children[1]?.textContent, "Source");
  fixture.controller.dispose();
  assert.equal(dragPreview(fixture), undefined);
  assert.equal(dragOverlay(fixture)?.dataset.atlasDragSessionId, undefined);
});

test("page visibility invalidation clears the drag preview without committing", () => {
  const fixture = dragFixture("toolbar-entry");
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 31, clientX: 10, clientY: 10, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 31, clientX: 20, clientY: 20
  }));
  assert.notEqual(dragPreview(fixture), undefined);

  fixture.document.hidden = true;
  fixture.document.dispatchEvent(new Event("visibilitychange"));

  assert.equal(dragPreview(fixture), undefined);
  assert.equal(dragOverlay(fixture)?.dataset.atlasDragSessionId, undefined);
  assert.equal(fixture.messages.length, 0);
  fixture.controller.dispose();
});

test("toolbar tail scaffold wins over its parent region and commits the tail index", async () => {
  const fixture = dragFixture("toolbar-entry");
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 30, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));

  delete fixture.root.dataset.atlasToolbarEntry;
  fixture.root.dataset.atlasDropScaffold = "true";
  fixture.root.dataset.atlasGroupId = "right-upper";
  fixture.root.dataset.atlasToolbarRegion = "InlineEndUpper";
  fixture.root.dataset.atlasInsertionIndex = "1";
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 30, clientX: 5, clientY: 0
  }));

  assert.equal(fixture.root.dataset.atlasDropPreview, "drop-scaffold");
  fixture.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 30, clientX: 5, clientY: 0
  }));
  await flushCallbacks();

  assert.equal(fixture.messages.length, 1);
  const scaffoldMessage = fixture.messages[0]!;
  assert.equal(scaffoldMessage.type, "dock-item");
  assert.equal(scaffoldMessage.protocolVersion, 1);
  assert.match(scaffoldMessage.sessionId, /^drag-host:\d+$/);
  assert.equal(scaffoldMessage.hostId, "drag-host");
  assert.equal(scaffoldMessage.workspaceId, "drag-workspace");
  assert.equal(scaffoldMessage.generation, 5);
  assert.equal(scaffoldMessage.itemId, "tool");
  assert.equal(scaffoldMessage.sourceKind, "toolbar-entry");
  assert.equal(scaffoldMessage.baseRevision, 12);
  assert.equal(scaffoldMessage.targetKind, "drop-scaffold");
  assert.equal(scaffoldMessage.targetGroupId, "right-upper");
  assert.equal(scaffoldMessage.logicalRegion, "InlineEndUpper");
  assert.equal(scaffoldMessage.insertionIndex, 1);
  fixture.controller.dispose();
});

test("toolbar entry bottom half commits the after index matching the tail scaffold", async () => {
  const fixture = dragFixture("toolbar-entry");
  fixture.root.dataset.atlasInsertionIndex = "1";
  fixture.root.rect = { left: 0, top: 0, width: 60, height: 24 };
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 33, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 33, clientX: 5, clientY: 20
  }));

  assert.equal(fixture.root.dataset.atlasDropPreview, "toolbar-insertion");
  fixture.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 33, clientX: 5, clientY: 20
  }));
  await flushCallbacks();

  assert.equal(fixture.messages.length, 1);
  const message = fixture.messages[0]!;
  assert.equal(message.type, "dock-item");
  const dock = message as { targetKind: string; insertionIndex?: number };
  assert.equal(dock.targetKind, "toolbar-insertion");
  assert.equal(dock.insertionIndex, 2);
  fixture.controller.dispose();
});

test("re-attached controller does not reuse a consumed drag session id", async () => {
  // The Host root element is replaced after a committed workspace change, so
  // the controller is disposed and a fresh instance is attached. Drag session
  // ids must stay monotonic across instances; otherwise the host rejects the
  // second drag as a duplicate session (consumedDragSessions).
  const first = dragFixture("toolbar-entry");
  first.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 40, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  first.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 40, clientX: 5, clientY: 0
  }));
  first.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 40, clientX: 5, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(first.messages.length, 1);
  const firstMessage = first.messages[0] as { sessionId: string };
  assert.match(firstMessage.sessionId, /^drag-host:\d+$/);
  first.controller.dispose();

  const second = dragFixture("toolbar-entry");
  second.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 41, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  second.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 41, clientX: 5, clientY: 0
  }));
  second.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 41, clientX: 5, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(second.messages.length, 1);
  const secondMessage = second.messages[0] as { sessionId: string };
  assert.match(secondMessage.sessionId, /^drag-host:\d+$/);
  assert.notEqual(secondMessage.sessionId, firstMessage.sessionId);
  const firstSequence = Number(firstMessage.sessionId.split(":").pop());
  const secondSequence = Number(secondMessage.sessionId.split(":").pop());
  assert.equal(secondSequence, firstSequence + 1);
  second.controller.dispose();
});

test("toolbar entry top half commits the before index", async () => {
  const fixture = dragFixture("toolbar-entry");
  fixture.root.dataset.atlasInsertionIndex = "1";
  fixture.root.rect = { left: 0, top: 0, width: 60, height: 24 };
  fixture.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 34, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  fixture.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 34, clientX: 5, clientY: 5
  }));
  fixture.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 34, clientX: 5, clientY: 5
  }));
  await flushCallbacks();

  assert.equal(fixture.messages.length, 1);
  const message = fixture.messages[0]!;
  assert.equal(message.type, "dock-item");
  const dock = message as { targetKind: string; insertionIndex?: number };
  assert.equal(dock.targetKind, "toolbar-insertion");
  assert.equal(dock.insertionIndex, 1);
  fixture.controller.dispose();
});

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
  const documentMessage = fixture.messages[0]!;
  assert.equal(documentMessage.type, "dock-item");
  assert.equal(documentMessage.protocolVersion, 1);
  assert.match(documentMessage.sessionId, /^drag-host:\d+$/);
  assert.equal(documentMessage.hostId, "drag-host");
  assert.equal(documentMessage.workspaceId, "drag-workspace");
  assert.equal(documentMessage.generation, 5);
  assert.equal(documentMessage.itemId, "document");
  assert.equal(documentMessage.sourceKind, "document-tab");
  assert.equal(documentMessage.baseRevision, 12);
  assert.equal(documentMessage.targetKind, "document-center");
  assert.equal(documentMessage.targetGroupId, "documents");
  assert.equal(documentMessage.insertionIndex, 1);
  assert.equal(fixture.root.dataset.atlasDropPreview, undefined);
  assert.equal(dragPreview(fixture), undefined);
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
    assert.equal(dragPreview(fixture), undefined);
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
  assert.equal(dragPreview(escaped), undefined);
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
  assert.equal(dragPreview(stale), undefined);
  stale.controller.dispose();
});

test("panel drag rejects its same-group header while threshold click remains untouched", async () => {
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
  assert.equal(drag.messages.length, 0);
  drag.controller.dispose();
});

test("same-group panel header drag targets a toolbar entry while the reverse drag stays rejected", async () => {
  const panel = dragFixture("panel-header");
  panel.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 52, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  delete panel.root.dataset.atlasPanelHeader;
  panel.root.dataset.atlasToolbarEntry = "true";
  panel.root.dataset.atlasToolbarRegion = "InlineStartUpper";
  panel.root.dataset.atlasItemId = "target-tool";
  panel.root.dataset.atlasInsertionIndex = "1";
  panel.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 52, clientX: 5, clientY: 0
  }));
  panel.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 52, clientX: 5, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(panel.messages.length, 1);
  assert.equal(
    (panel.messages[0] as { targetKind: string }).targetKind,
    "toolbar-insertion");
  assert.equal(
    (panel.messages[0] as { targetItemId?: string }).targetItemId,
    "target-tool");
  panel.controller.dispose();

  const panelAfter = dragFixture("panel-header");
  panelAfter.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 56, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  delete panelAfter.root.dataset.atlasPanelHeader;
  panelAfter.root.dataset.atlasToolbarEntry = "true";
  panelAfter.root.dataset.atlasToolbarRegion = "InlineStartUpper";
  panelAfter.root.dataset.atlasItemId = "target-tool";
  panelAfter.root.dataset.atlasInsertionIndex = "1";
  panelAfter.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 56, clientX: 5, clientY: 75
  }));
  panelAfter.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 56, clientX: 5, clientY: 75
  }));
  await flushCallbacks();
  assert.equal(panelAfter.messages.length, 1);
  assert.equal(
    (panelAfter.messages[0] as { insertionIndex?: number }).insertionIndex,
    2);
  assert.equal(
    (panelAfter.messages[0] as { targetItemId?: string }).targetItemId,
    "target-tool");
  panelAfter.controller.dispose();

  const toolbar = dragFixture("toolbar-entry");
  toolbar.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 53, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  delete toolbar.root.dataset.atlasToolbarEntry;
  delete toolbar.root.dataset.atlasToolbarRegion;
  toolbar.root.dataset.atlasPanelHeader = "true";
  toolbar.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 53, clientX: 5, clientY: 0
  }));
  toolbar.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 53, clientX: 5, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(toolbar.messages.length, 0);
  toolbar.controller.dispose();
});

test("cross-group panel and toolbar drags retain migration targets", async () => {
  const panel = dragFixture("panel-header");
  panel.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 54, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  delete panel.root.dataset.atlasPanelHeader;
  panel.root.dataset.atlasToolbarEntry = "true";
  panel.root.dataset.atlasGroupId = "right-upper";
  panel.root.dataset.atlasToolbarRegion = "InlineEndUpper";
  panel.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 54, clientX: 5, clientY: 0
  }));
  panel.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 54, clientX: 5, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(panel.messages.length, 1);
  assert.equal(
    (panel.messages[0] as { targetKind: string }).targetKind,
    "toolbar-insertion");
  panel.controller.dispose();

  const toolbar = dragFixture("toolbar-entry");
  toolbar.root.dispatchEvent(interactionEvent("pointerdown", {
    pointerId: 55, clientX: 0, clientY: 0, button: 0, isPrimary: true
  }));
  delete toolbar.root.dataset.atlasToolbarEntry;
  delete toolbar.root.dataset.atlasToolbarRegion;
  toolbar.root.dataset.atlasPanelHeader = "true";
  toolbar.root.dataset.atlasGroupId = "right-upper";
  toolbar.root.dispatchEvent(interactionEvent("pointermove", {
    pointerId: 55, clientX: 5, clientY: 0
  }));
  toolbar.root.dispatchEvent(interactionEvent("pointerup", {
    pointerId: 55, clientX: 5, clientY: 0
  }));
  await flushCallbacks();
  assert.equal(toolbar.messages.length, 1);
  assert.equal(
    (toolbar.messages[0] as { targetKind: string }).targetKind,
    "panel-header-insertion");
  toolbar.controller.dispose();
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
