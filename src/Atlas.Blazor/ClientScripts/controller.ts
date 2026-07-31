import {
  ATLAS_PROTOCOL_VERSION,
  type AtlasControllerIdentity,
  type AtlasControllerOptions,
  type AtlasDotNetReceiver,
  type AtlasKeyboardCommand,
  type AtlasKeyboardBinding,
  type AtlasInteractionMessage,
  isValidControllerIdentity
} from "./protocol";
import domMetadata from "./dom-metadata-manifest.json";

const controllers = new Map<string, AtlasInteractionController>();
const defaultKeyboardBindings: readonly AtlasKeyboardBinding[] = [
  binding("Tab", "next-tab", true),
  binding("Tab", "previous-tab", true, true),
  binding("KeyW", "close-active-document", true),
  binding("F4", "close-active-document", true),
  binding("ArrowLeft", "focus-inline-start", false, false, true),
  binding("ArrowRight", "focus-inline-end", false, false, true),
  binding("ArrowUp", "focus-block-start", false, false, true),
  binding("ArrowDown", "focus-block-end", false, false, true)
];

interface ActiveResize {
  readonly splitter: HTMLElement;
  readonly split: HTMLElement;
  readonly pointerId: number;
  readonly startCoordinate: number;
  readonly startRatio: number;
  readonly extent: number;
  readonly minimumRatio: number;
  readonly maximumRatio: number;
  readonly baseRevision: number;
  currentRatio: number;
}

export class AtlasInteractionController {
  private keyboardStepPixels: number;
  private keyboardCommitDelayMilliseconds: number;
  private keyboardBindings: readonly AtlasKeyboardBinding[];
  private readonly ownerDocument: Document | undefined;
  private readonly ownerWindow: Window | null | undefined;
  private activeResize: ActiveResize | undefined;
  private keyboardTimer: ReturnType<typeof setTimeout> | undefined;
  private keyboardSplit: HTMLElement | undefined;
  private keyboardRatio: number | undefined;
  private callbackQueue: Promise<unknown> = Promise.resolve();
  private disposed = false;

  public constructor(
    private readonly root: HTMLElement,
    private identity: AtlasControllerIdentity,
    private readonly receiver: AtlasDotNetReceiver,
    options: AtlasControllerOptions = {})
  {
    if (!isValidControllerIdentity(identity)) {
      throw new Error("ATLAS-INTERACTION-PROTOCOL-INVALID");
    }

    validateOptions(options);
    this.keyboardStepPixels = options.keyboardStepPixels ?? 10;
    this.keyboardCommitDelayMilliseconds =
      options.keyboardCommitDelayMilliseconds ?? 500;
    this.keyboardBindings = options.keyboardBindings ?? defaultKeyboardBindings;
    this.ownerDocument = root.ownerDocument ?? undefined;
    this.ownerWindow = this.ownerDocument?.defaultView;
    this.root.addEventListener("pointerdown", this.onPointerDown);
    this.root.addEventListener("pointermove", this.onPointerMove);
    this.root.addEventListener("pointerup", this.onPointerUp);
    this.root.addEventListener("pointercancel", this.onPointerCancel);
    this.root.addEventListener("lostpointercapture", this.onPointerCancel);
    this.root.addEventListener("dblclick", this.onDoubleClick);
    this.root.addEventListener("keydown", this.onKeyDown);
    this.root.addEventListener("keyup", this.onKeyUp);
    this.ownerWindow?.addEventListener("resize", this.onEnvironmentInvalidated);
    this.ownerDocument?.addEventListener(
      "visibilitychange",
      this.onVisibilityChanged);
  }

  public update(
    identity: AtlasControllerIdentity,
    options: AtlasControllerOptions = {}): void {
    if (this.disposed || !isValidControllerIdentity(identity)
      || identity.hostId !== this.identity.hostId
      || identity.workspaceId !== this.identity.workspaceId
      || identity.generation < this.identity.generation) {
      throw new Error("ATLAS-INTERACTION-PROTOCOL-INVALID");
    }

    validateOptions(options);
    this.identity = identity;
    this.keyboardStepPixels =
      options.keyboardStepPixels ?? this.keyboardStepPixels;
    this.keyboardCommitDelayMilliseconds =
      options.keyboardCommitDelayMilliseconds
        ?? this.keyboardCommitDelayMilliseconds;
    this.keyboardBindings = options.keyboardBindings ?? this.keyboardBindings;
  }

  public dispose(): void {
    if (this.disposed) {
      return;
    }

    this.disposed = true;
    this.cancelResize();
    this.clearKeyboardCommit();
    this.root.removeEventListener("pointerdown", this.onPointerDown);
    this.root.removeEventListener("pointermove", this.onPointerMove);
    this.root.removeEventListener("pointerup", this.onPointerUp);
    this.root.removeEventListener("pointercancel", this.onPointerCancel);
    this.root.removeEventListener("lostpointercapture", this.onPointerCancel);
    this.root.removeEventListener("dblclick", this.onDoubleClick);
    this.root.removeEventListener("keydown", this.onKeyDown);
    this.root.removeEventListener("keyup", this.onKeyUp);
    this.ownerWindow?.removeEventListener("resize", this.onEnvironmentInvalidated);
    this.ownerDocument?.removeEventListener(
      "visibilitychange",
      this.onVisibilityChanged);
  }

  private readonly onPointerDown = (event: PointerEvent): void => {
    if (event.button !== 0 || !event.isPrimary
      || this.isCollapseControl(event.target)) {
      return;
    }

    const splitter = this.splitterFrom(event.target);
    const split = splitter?.parentElement;
    if (this.disposed || !splitter || !split || this.isLocked(split)) {
      return;
    }

    const rect = split.getBoundingClientRect();
    const inline = split.dataset.atlasSplitAxis === "InlineChildren";
    const extent = inline ? rect.width : rect.height;
    if (!Number.isFinite(extent) || extent <= 0) {
      return;
    }

    const startRatio = this.readRatio(split);
    const limits = this.readLimits(split, extent);
    this.activeResize = {
      splitter,
      split,
      pointerId: event.pointerId,
      startCoordinate: inline ? event.clientX : event.clientY,
      startRatio,
      extent,
      minimumRatio: limits.minimum,
      maximumRatio: limits.maximum,
      baseRevision: this.readRevision(split),
      currentRatio: startRatio
    };
    splitter.setPointerCapture?.(event.pointerId);
    split.dataset.atlasResizing = "true";
    event.preventDefault();
  };

  private readonly onPointerMove = (event: PointerEvent): void => {
    const active = this.activeResize;
    if (!active || event.pointerId !== active.pointerId) {
      return;
    }

    const inline = active.split.dataset.atlasSplitAxis === "InlineChildren";
    const coordinate = inline ? event.clientX : event.clientY;
    active.currentRatio = clamp(
      active.startRatio + ((coordinate - active.startCoordinate) / active.extent),
      active.minimumRatio,
      active.maximumRatio);
    this.preview(active.split, active.currentRatio);
  };

  private readonly onPointerUp = (event: PointerEvent): void => {
    const active = this.activeResize;
    if (!active || event.pointerId !== active.pointerId) {
      return;
    }

    this.activeResize = undefined;
    active.split.removeAttribute("data-atlas-resizing");
    this.clearPreview(active.split);
    active.splitter.releasePointerCapture?.(event.pointerId);
    if (active.currentRatio !== active.startRatio) {
      this.commitResize(
        active.split,
        active.currentRatio,
        "resize",
        active.baseRevision);
    }
  };

  private readonly onPointerCancel = (event: PointerEvent): void => {
    if (this.activeResize?.pointerId === event.pointerId) {
      this.cancelResize();
    }
  };

  private readonly onDoubleClick = (event: MouseEvent): void => {
    if (this.isCollapseControl(event.target)) {
      return;
    }

    const splitter = this.splitterFrom(event.target);
    const split = splitter?.parentElement;
    if (!splitter || !split || this.isLocked(split)) {
      return;
    }

    this.cancelResize();
    this.preview(split, 0.5);
    this.clearPreview(split);
    this.commitResize(
      split,
      0.5,
      "equalize",
      this.readRevision(split));
  };

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.code === "Escape" && this.activeResize) {
      event.preventDefault();
      this.cancelResize();
      return;
    }

    const splitter = this.splitterFrom(event.target);
    const split = splitter?.parentElement;
    if (splitter && split && !this.isLocked(split)
      && ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End"]
        .includes(event.code)) {
      event.preventDefault();
      const direction = event.code === "ArrowLeft" || event.code === "ArrowUp" ? -1 : 1;
      const requested = event.code === "Home"
        ? 0
        : event.code === "End"
          ? 1
          : (this.keyboardSplit === split && this.keyboardRatio !== undefined
            ? this.keyboardRatio
            : this.readRatio(split))
              + (direction * (this.keyboardStepPixels / extentFor(split)));
      const rect = split.getBoundingClientRect();
      const extent = split.dataset.atlasSplitAxis === "InlineChildren"
        ? rect.width
        : rect.height;
      const limits = this.readLimits(split, extent);
      this.keyboardSplit = split;
      this.keyboardRatio = clamp(requested, limits.minimum, limits.maximum);
      this.preview(split, this.keyboardRatio);
      this.commitResize(split, this.keyboardRatio, "keyboard", null);
      this.scheduleKeyboardCommit();
      return;
    }

    const binding = this.keyboardBinding(event);
    if (!binding || (this.isEditable(event.target) && !binding.isGlobal)) {
      return;
    }

    const command = this.resolveLogicalCommand(binding.command);
    const sourceGroupId = this.groupIdFrom(event.target);
    const targetGroupId = command.startsWith("focus-")
      ? this.resolveFocusTarget(command, event.target)
      : undefined;
    if (command.startsWith("focus-") && targetGroupId === undefined) {
      return;
    }

    event.preventDefault();
    this.send({
      type: "keyboard-command",
      command,
      generation: this.identity.generation,
      ...(sourceGroupId === undefined ? {} : { sourceGroupId }),
      ...(targetGroupId === undefined ? {} : { targetGroupId })
    });
  };

  private readonly onKeyUp = (event: KeyboardEvent): void => {
    if (this.keyboardSplit
      && this.splitterFrom(event.target)?.parentElement === this.keyboardSplit) {
      this.flushKeyboardCommit();
    }
  };

  private readonly onEnvironmentInvalidated = (): void => {
    this.cancelResize();
    this.clearKeyboardCommit();
  };

  private readonly onVisibilityChanged = (): void => {
    if (this.ownerDocument?.hidden) {
      this.onEnvironmentInvalidated();
    }
  };

  private keyboardBinding(event: KeyboardEvent): AtlasKeyboardBinding | undefined {
    return this.keyboardBindings.find(candidate =>
      candidate.code === event.code
      && candidate.control === Boolean(event.ctrlKey)
      && candidate.shift === Boolean(event.shiftKey)
      && candidate.alt === Boolean(event.altKey)
      && candidate.meta === Boolean(event.metaKey));
  }

  private resolveLogicalCommand(command: AtlasKeyboardCommand): AtlasKeyboardCommand {
    const direction = this.ownerWindow?.getComputedStyle(this.root).direction;
    if (direction !== "rtl") {
      return command;
    }

    if (command === "focus-inline-start") {
      return "focus-inline-end";
    }

    return command === "focus-inline-end" ? "focus-inline-start" : command;
  }

  private groupIdFrom(target: EventTarget | null): string | undefined {
    const source = target instanceof Element
      ? target.closest<HTMLElement>(domMetadata.group.selector)
      : null;
    const active = source ?? this.ownerDocument?.activeElement
      ?.closest<HTMLElement>(domMetadata.group.selector);
    return active?.dataset.atlasGroupId;
  }

  private resolveFocusTarget(
    command: AtlasKeyboardCommand,
    target: EventTarget | null): string | undefined {
    const source = target instanceof Element
      ? target.closest<HTMLElement>(domMetadata.group.selector)
      : null;
    const active = source ?? this.ownerDocument?.activeElement
      ?.closest<HTMLElement>(domMetadata.group.selector);
    if (!active) {
      return undefined;
    }

    const sourceRect = active.getBoundingClientRect();
    const sourceX = sourceRect.left + (sourceRect.width / 2);
    const sourceY = sourceRect.top + (sourceRect.height / 2);
    const inlineStart = command === "focus-inline-start";
    const inlineEnd = command === "focus-inline-end";
    const blockStart = command === "focus-block-start";
    const candidates = Array.from(
      this.root.querySelectorAll<HTMLElement>(
        domMetadata.toolGroup.selector));
    let best: { id: string; score: number } | undefined;
    for (const candidate of candidates) {
      if (candidate === active) {
        continue;
      }

      const rect = candidate.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) {
        continue;
      }

      const x = rect.left + (rect.width / 2);
      const y = rect.top + (rect.height / 2);
      const primary = inlineStart
        ? sourceX - x
        : inlineEnd
          ? x - sourceX
          : blockStart
            ? sourceY - y
            : y - sourceY;
      if (primary <= 0) {
        continue;
      }

      const perpendicular = inlineStart || inlineEnd
        ? Math.abs(y - sourceY)
        : Math.abs(x - sourceX);
      const score = primary + (perpendicular * 0.25);
      const id = candidate.dataset.atlasGroupId;
      if (id && (best === undefined || score < best.score)) {
        best = { id, score };
      }
    }

    return best?.id;
  }

  private scheduleKeyboardCommit(): void {
    if (this.keyboardTimer !== undefined) {
      clearTimeout(this.keyboardTimer);
    }

    this.keyboardTimer = setTimeout(
      () => this.flushKeyboardCommit(),
      this.keyboardCommitDelayMilliseconds);
  }

  private flushKeyboardCommit(): void {
    this.clearKeyboardCommit();
  }

  private clearKeyboardCommit(): void {
    if (this.keyboardTimer !== undefined) {
      clearTimeout(this.keyboardTimer);
    }

    this.keyboardTimer = undefined;
    if (this.keyboardSplit) {
      this.clearPreview(this.keyboardSplit);
    }
    this.keyboardSplit = undefined;
    this.keyboardRatio = undefined;
  }

  private cancelResize(): void {
    const active = this.activeResize;
    this.activeResize = undefined;
    if (active) {
      active.split.removeAttribute("data-atlas-resizing");
      this.clearPreview(active.split);
      active.splitter.releasePointerCapture?.(active.pointerId);
    }
  }

  private commitResize(
    split: HTMLElement,
    firstRatio: number,
    mode: "resize" | "keyboard" | "equalize",
    baseRevision: number | null): void {
    const splitId = split.dataset.atlasNodeId;
    if (!splitId) {
      return;
    }

    this.send({
      type: "resize-split",
      splitId,
      firstRatio,
      mode,
      generation: this.identity.generation,
      baseRevision
    });
  }

  private send(message: AtlasInteractionMessage): void {
    this.callbackQueue = this.callbackQueue.then(
      () => this.disposed
        ? undefined
        : this.receiver.invokeMethodAsync("OnInteraction", message),
      () => undefined);
  }

  private preview(split: HTMLElement, ratio: number): void {
    split.style.setProperty("--atlas-split-preview", ratio.toString());
    const tracks = `minmax(0, ${ratio}fr) var(--atlas-splitter-size) minmax(0, ${1 - ratio}fr)`;
    split.style.setProperty(
      split.dataset.atlasSplitAxis === "InlineChildren"
        ? "grid-template-columns"
        : "grid-template-rows",
      tracks);
  }

  private clearPreview(split: HTMLElement): void {
    split.style.removeProperty("--atlas-split-preview");
    split.style.removeProperty("grid-template-columns");
    split.style.removeProperty("grid-template-rows");
  }

  private splitterFrom(target: EventTarget | null): HTMLElement | null {
    return target instanceof Element
      ? target.closest<HTMLElement>(domMetadata.splitter.selector)
      : null;
  }

  private isLocked(split: HTMLElement): boolean {
    return split.dataset.atlasSplitLocked === "true";
  }

  private readRatio(split: HTMLElement): number {
    if (split.dataset.atlasSplitBasisKind === "FixedPixels") {
      const extent = extentFor(split);
      const pixels = Number(split.dataset.atlasSplitBasisValue);
      if (Number.isFinite(pixels)) {
        return clamp(
          split.dataset.atlasSplitAnchor === "Second"
            ? 1 - (pixels / extent)
            : pixels / extent,
          0,
          1);
      }
    }

    const value = Number(split.dataset.atlasSplitRatio);
    return Number.isFinite(value) ? clamp(value, 0, 1) : 0.5;
  }

  private readRevision(split: HTMLElement): number {
    const value = Number(split.dataset.atlasRevision);
    return Number.isSafeInteger(value) && value >= 0 ? value : 0;
  }

  private readLimits(split: HTMLElement, extent: number): {
    minimum: number;
    maximum: number;
  } {
    const firstMinimum = Number(split.dataset.atlasFirstMinimum) || 0;
    const secondMinimum = Number(split.dataset.atlasSecondMinimum) || 0;
    const firstMaximum = Number(split.dataset.atlasFirstMaximum);
    const secondMaximum = Number(split.dataset.atlasSecondMaximum);
    const minimumTotal = firstMinimum + secondMinimum;
    if (minimumTotal > extent) {
      const ratio = minimumTotal === 0 ? 0.5 : firstMinimum / minimumTotal;
      return { minimum: ratio, maximum: ratio };
    }

    if (Number.isFinite(firstMaximum)
      && Number.isFinite(secondMaximum)
      && firstMaximum + secondMaximum < extent) {
      const maximumTotal = firstMaximum + secondMaximum;
      const ratio = maximumTotal === 0 ? 0.5 : firstMaximum / maximumTotal;
      return { minimum: ratio, maximum: ratio };
    }

    const minimum = Math.max(
      firstMinimum / extent,
      Number.isFinite(secondMaximum) ? 1 - (secondMaximum / extent) : 0);
    const maximum = Math.min(
      1 - (secondMinimum / extent),
      Number.isFinite(firstMaximum) ? firstMaximum / extent : 1);
    return {
      minimum: clamp(minimum, 0, 1),
      maximum: clamp(maximum, 0, 1)
    };
  }

  private isEditable(target: EventTarget | null): boolean {
    return target instanceof Element
      && target.closest("input,textarea,select,[contenteditable=true]") !== null;
  }

  private isCollapseControl(target: EventTarget | null): boolean {
    return target instanceof Element
      && target.closest(domMetadata.collapseControl.selector) !== null;
  }

}

export function attachAtlasController(
  root: HTMLElement,
  identity: AtlasControllerIdentity,
  receiver: AtlasDotNetReceiver,
  options?: AtlasControllerOptions): AtlasInteractionController {
  if (identity.protocolVersion !== ATLAS_PROTOCOL_VERSION
    || controllers.has(identity.workspaceId)) {
    throw new Error("ATLAS-INTERACTION-ATTACH-REJECTED");
  }

  const controller = new AtlasInteractionController(root, identity, receiver, options);
  controllers.set(identity.workspaceId, controller);
  return controller;
}

export function updateAtlasController(
  controller: AtlasInteractionController,
  identity: AtlasControllerIdentity,
  options?: AtlasControllerOptions): void {
  controller.update(identity, options);
}

export function disposeAtlasController(
  controller: AtlasInteractionController): void {
  controller.dispose();
  for (const [workspaceId, registered] of controllers) {
    if (registered === controller) {
      controllers.delete(workspaceId);
      break;
    }
  }
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(Math.max(value, minimum), maximum);
}

function extentFor(split: HTMLElement): number {
  const rect = split.getBoundingClientRect();
  const extent = split.dataset.atlasSplitAxis === "InlineChildren"
    ? rect.width
    : rect.height;
  return Number.isFinite(extent) && extent > 0 ? extent : 1;
}

function binding(
  code: string,
  command: AtlasKeyboardCommand,
  control = false,
  shift = false,
  alt = false,
  meta = false): AtlasKeyboardBinding {
  return {
    code,
    command,
    control,
    shift,
    alt,
    meta,
    isGlobal: false
  };
}

function validateOptions(options: AtlasControllerOptions): void {
  if (options.keyboardStepPixels !== undefined
    && (!Number.isFinite(options.keyboardStepPixels)
      || options.keyboardStepPixels <= 0)) {
    throw new Error("ATLAS-INTERACTION-PROTOCOL-INVALID");
  }

  if (options.keyboardCommitDelayMilliseconds !== undefined
    && (!Number.isSafeInteger(options.keyboardCommitDelayMilliseconds)
      || options.keyboardCommitDelayMilliseconds < 0)) {
    throw new Error("ATLAS-INTERACTION-PROTOCOL-INVALID");
  }

  if (options.keyboardBindings?.some(candidate =>
    candidate.code.length === 0
    || typeof candidate.meta !== "boolean"
    || !defaultKeyboardBindings.some(
      known => known.command === candidate.command))) {
    throw new Error("ATLAS-INTERACTION-PROTOCOL-INVALID");
  }
}
