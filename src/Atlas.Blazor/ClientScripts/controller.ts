import {
  ATLAS_PROTOCOL_VERSION,
  type AtlasControllerIdentity,
  type AtlasControllerOptions,
  type AtlasDotNetReceiver,
  type AtlasDragSourceKind,
  type AtlasDropTargetKind,
  type AtlasKeyboardCommand,
  type AtlasKeyboardBinding,
  type AtlasInteractionMessage,
  isValidControllerIdentity
} from "./protocol";
import domMetadata from "./dom-metadata-manifest.json";
import { AtlasSandboxController } from "./sandbox";

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

interface DropTarget {
  readonly element: HTMLElement;
  readonly kind: AtlasDropTargetKind;
  readonly groupId: string;
  readonly targetItemId?: string;
  readonly logicalRegion?: string;
  readonly insertionIndex?: number;
}

interface ActiveDrag {
  readonly source: HTMLElement;
  readonly pointerId: number;
  readonly startX: number;
  readonly startY: number;
  readonly sessionId: string;
  readonly generation: number;
  readonly baseRevision: number;
  readonly itemId: string;
  readonly sourceKind: AtlasDragSourceKind;
  readonly sourceGroupId?: string;
  documentGeometry: ReadonlyMap<HTMLElement, DOMRect>;
  documentTabGeometry: ReadonlyMap<HTMLElement, DOMRect>;
  dragging: boolean;
  preview: HTMLElement | undefined;
  target?: DropTarget;
}

export class AtlasInteractionController {
  private keyboardStepPixels: number;
  private keyboardCommitDelayMilliseconds: number;
  private keyboardBindings: readonly AtlasKeyboardBinding[];
  private readonly ownerDocument: Document | undefined;
  private readonly ownerWindow: Window | null | undefined;
  private readonly reducedMotionQuery: MediaQueryList | undefined;
  private readonly sandboxController: AtlasSandboxController;
  private activeResize: ActiveResize | undefined;
  private activeDrag: ActiveDrag | undefined;
  private dragSequence = 0;
  private dragPreviewFrame: number | undefined;
  private pendingDragPreviewPosition:
    { readonly preview: HTMLElement; readonly x: number; readonly y: number } | undefined;
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
    this.reducedMotionQuery = this.ownerWindow?.matchMedia(
      "(prefers-reduced-motion: reduce)");
    this.sandboxController = new AtlasSandboxController(root, identity);
    this.root.addEventListener("pointerdown", this.onPointerDown);
    this.root.addEventListener("pointermove", this.onPointerMove);
    this.root.addEventListener("pointerup", this.onPointerUp);
    this.root.addEventListener("pointercancel", this.onPointerCancel);
    this.root.addEventListener("lostpointercapture", this.onPointerCancel);
    this.root.addEventListener("dblclick", this.onDoubleClick);
    this.root.addEventListener("keydown", this.onKeyDown);
    this.root.addEventListener("keyup", this.onKeyUp);
    this.ownerWindow?.addEventListener("resize", this.onEnvironmentInvalidated);
    this.ownerWindow?.addEventListener("blur", this.onEnvironmentInvalidated);
    this.ownerWindow?.addEventListener("pointerup", this.onPointerUp);
    this.ownerWindow?.addEventListener("pointercancel", this.onPointerCancel);
    this.ownerDocument?.addEventListener(
      "visibilitychange",
      this.onVisibilityChanged);
    this.reducedMotionQuery?.addEventListener("change", this.onReducedMotionChanged);
    this.projectReducedMotion();
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
    if (identity.generation !== this.identity.generation) {
      this.cancelDrag();
      this.cancelResize();
      this.clearKeyboardCommit();
    }
    this.identity = identity;
    this.sandboxController.update(identity);
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
    this.cancelDrag();
    this.clearKeyboardCommit();
    this.sandboxController.dispose();
    this.root.removeEventListener("pointerdown", this.onPointerDown);
    this.root.removeEventListener("pointermove", this.onPointerMove);
    this.root.removeEventListener("pointerup", this.onPointerUp);
    this.root.removeEventListener("pointercancel", this.onPointerCancel);
    this.root.removeEventListener("lostpointercapture", this.onPointerCancel);
    this.root.removeEventListener("dblclick", this.onDoubleClick);
    this.root.removeEventListener("keydown", this.onKeyDown);
    this.root.removeEventListener("keyup", this.onKeyUp);
    this.ownerWindow?.removeEventListener("resize", this.onEnvironmentInvalidated);
    this.ownerWindow?.removeEventListener("blur", this.onEnvironmentInvalidated);
    this.ownerWindow?.removeEventListener("pointerup", this.onPointerUp);
    this.ownerWindow?.removeEventListener("pointercancel", this.onPointerCancel);
    this.ownerDocument?.removeEventListener(
      "visibilitychange",
      this.onVisibilityChanged);
    this.reducedMotionQuery?.removeEventListener("change", this.onReducedMotionChanged);
  }

  private readonly onReducedMotionChanged = (): void => {
    this.projectReducedMotion();
  };

  private projectReducedMotion(): void {
    this.root.dataset.atlasReducedMotion = this.reducedMotionQuery?.matches
      ? "true"
      : "false";
  }

  private readonly onPointerDown = (event: PointerEvent): void => {
    if (event.button !== 0 || !event.isPrimary
      || this.isCollapseControl(event.target)) {
      return;
    }

    const dragSource = this.dragSourceFrom(event.target);
    if (!this.disposed && dragSource && this.canMove(dragSource)) {
      const itemId = dragSource.dataset.atlasItemId;
      const sourceKind = dragSource.dataset.atlasDragKind as AtlasDragSourceKind;
      if (itemId && isDragSourceKind(sourceKind)) {
        const sourceGroupId = this.groupIdFrom(dragSource);
        this.cancelResize();
        this.dragSequence += 1;
        this.activeDrag = {
          source: dragSource,
          pointerId: event.pointerId,
          startX: event.clientX,
          startY: event.clientY,
          sessionId: `${this.identity.hostId}:${this.dragSequence}`,
          generation: this.identity.generation,
          baseRevision: this.readRootRevision(),
          itemId,
          sourceKind,
          ...(sourceGroupId === undefined
            ? {}
            : { sourceGroupId }),
          documentGeometry: new Map(),
          documentTabGeometry: new Map(),
          dragging: false,
          preview: undefined
        };
        dragSource.setPointerCapture?.(event.pointerId);
        event.preventDefault();
        return;
      }
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
    const drag = this.activeDrag;
    if (drag && event.pointerId === drag.pointerId) {
      let previewCreated = false;
      if (!drag.dragging
        && Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) >= 4) {
        drag.documentGeometry = this.captureDocumentGeometry();
        drag.documentTabGeometry = this.captureDocumentTabGeometry();
        drag.dragging = true;
        drag.preview = this.createDragPreview(drag, event.clientX, event.clientY);
        previewCreated = true;
        this.root.dataset.atlasDragging = "true";
      }

      if (drag.dragging) {
        if (!previewCreated) {
          this.positionDragPreview(drag.preview, event.clientX, event.clientY);
        }
        this.previewDropTarget(
          drag,
          this.resolveDropTarget(event.clientX, event.clientY, event.target, drag));
      }
      return;
    }

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
    const drag = this.activeDrag;
    if (drag && event.pointerId === drag.pointerId) {
      const target = drag.dragging
        ? this.resolveDropTarget(
            event.clientX,
            event.clientY,
            event.target,
            drag)
        : undefined;
      this.finishDrag();
      if (target && drag.generation === this.identity.generation) {
        const directional = target.kind.startsWith("document-")
          && target.kind !== "document-center";
        this.send({
          type: "dock-item",
          protocolVersion: ATLAS_PROTOCOL_VERSION,
          sessionId: drag.sessionId,
          hostId: this.identity.hostId,
          workspaceId: this.identity.workspaceId,
          generation: drag.generation,
          itemId: drag.itemId,
          sourceKind: drag.sourceKind,
          baseRevision: drag.baseRevision,
          targetKind: target.kind,
          targetGroupId: target.groupId,
          ...(target.targetItemId === undefined
            ? {}
            : { targetItemId: target.targetItemId }),
          ...(target.logicalRegion === undefined
            ? {}
            : { logicalRegion: target.logicalRegion }),
          ...(target.insertionIndex === undefined
            ? {}
            : { insertionIndex: target.insertionIndex }),
          ...(directional
            ? {
                newGroupId: `atlas-${this.identity.hostId}-${this.dragSequence}-group`,
                newSplitId: `atlas-${this.identity.hostId}-${this.dragSequence}-split`
              }
            : {})
        });
      }
      return;
    }

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
    if (this.activeDrag?.pointerId === event.pointerId) {
      this.cancelDrag();
    }
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
    if (event.code === "Escape" && this.activeDrag) {
      event.preventDefault();
      this.cancelDrag();
      return;
    }

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
    this.cancelDrag();
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

  private cancelDrag(): void {
    if (this.activeDrag) {
      this.finishDrag();
    }
  }

  private finishDrag(): void {
    const drag = this.activeDrag;
    this.activeDrag = undefined;
    if (!drag) {
      return;
    }

    this.clearDropPreview(drag.target);
    this.clearDragPreview(drag.preview);
    this.root.removeAttribute("data-atlas-dragging");
    drag.source.releasePointerCapture?.(drag.pointerId);
  }

  private createDragPreview(
    drag: ActiveDrag,
    x: number,
    y: number): HTMLElement | undefined {
    const document = this.ownerDocument;
    const overlayRoot = this.findOverlayRoot();
    if (!document || !overlayRoot) {
      return undefined;
    }

    overlayRoot.dataset.atlasDragSessionId = drag.sessionId;

    const preview = document.createElement("div");
    preview.className = "atlas-v2-drag-preview";
    preview.dataset.atlasDragPreview = drag.sourceKind;
    preview.setAttribute("aria-hidden", "true");
    preview.setAttribute("role", "presentation");
    preview.style.setProperty("pointer-events", "none");
    this.writeDragPreviewPosition(preview, x, y);

    const label = drag.source.getAttribute("aria-label")?.trim()
      || drag.source.textContent?.trim()
      || drag.itemId;

    const iconKey = drag.source
      .querySelector<HTMLElement>(".atlas-v2-item-icon[data-atlas-icon]")
      ?.dataset.atlasIcon;
    if (iconKey) {
      const icon = document.createElement("span");
      icon.className = "atlas-v2-item-icon";
      icon.dataset.atlasIcon = iconKey;
      preview.appendChild(icon);
    }

    const title = document.createElement("span");
    title.className = "atlas-v2-drag-preview-title";
    title.textContent = label;
    preview.appendChild(title);

    overlayRoot.appendChild(preview);
    return preview;
  }

  private positionDragPreview(
    preview: HTMLElement | undefined,
    x: number,
    y: number): void {
    if (!preview) {
      return;
    }

    this.pendingDragPreviewPosition = { preview, x, y };
    if (this.dragPreviewFrame !== undefined) {
      return;
    }

    const flush = (): void => {
      this.dragPreviewFrame = undefined;
      const pending = this.pendingDragPreviewPosition;
      this.pendingDragPreviewPosition = undefined;
      if (pending) {
        this.writeDragPreviewPosition(pending.preview, pending.x, pending.y);
      }
    };

    if (this.ownerWindow) {
      this.dragPreviewFrame = this.ownerWindow.requestAnimationFrame(flush);
    } else {
      flush();
    }
  }

  private writeDragPreviewPosition(preview: HTMLElement, x: number, y: number): void {
    preview.style.setProperty(
      "transform",
      `translate3d(${x + 12}px, ${y + 12}px, 0)`);
  }

  private clearDragPreview(preview: HTMLElement | undefined): void {
    if (this.dragPreviewFrame !== undefined) {
      this.ownerWindow?.cancelAnimationFrame(this.dragPreviewFrame);
      this.dragPreviewFrame = undefined;
    }

    this.pendingDragPreviewPosition = undefined;
    const overlayRoot = this.findOverlayRoot();
    preview?.remove();
    overlayRoot?.removeAttribute("data-atlas-drag-session-id");
  }

  private findOverlayRoot(): HTMLElement | undefined {
    const hostId = this.root.dataset.atlasHostId;
    if (!hostId || hostId !== this.identity.hostId) {
      return undefined;
    }

    for (const child of Array.from(this.root.children)) {
      const candidate = child as HTMLElement;
      if (candidate.dataset.atlasOverlayRoot === "true"
        && candidate.dataset.atlasHostId === hostId) {
        return candidate;
      }
    }

    return undefined;
  }

  private previewDropTarget(drag: ActiveDrag, target: DropTarget | undefined): void {
    if (drag.target?.element === target?.element && drag.target?.kind === target?.kind) {
      return;
    }

    this.clearDropPreview(drag.target);
    if (target) {
      drag.target = target;
      target.element.dataset.atlasDropPreview = target.kind;
    } else {
      delete drag.target;
    }
  }

  private clearDropPreview(target: DropTarget | undefined): void {
    target?.element.removeAttribute("data-atlas-drop-preview");
  }

  private resolveDropTarget(
    x: number,
    y: number,
    eventTarget: EventTarget | null,
    drag: ActiveDrag): DropTarget | undefined {
    const hit = this.ownerDocument?.elementFromPoint(x, y)
      ?? (eventTarget instanceof Element ? eventTarget : null);
    if (!(hit instanceof Element)
      || hit.closest<HTMLElement>(domMetadata.workspaceRoot.selector) !== this.root) {
      return undefined;
    }

    if (drag.sourceKind === "document-tab") {
      const group = hit.closest<HTMLElement>("[data-atlas-group-kind=document][data-atlas-group-id]");
      const groupId = group?.dataset.atlasGroupId;
      if (!group || !groupId) {
        return undefined;
      }

      const tab = hit.closest<HTMLElement>("[data-atlas-drag-kind=document-tab]");
      const tabIndex = readNonNegativeInteger(tab?.dataset.atlasInsertionIndex);
      if (tab
        && tabIndex !== undefined
        && groupId === drag.sourceGroupId) {
        const rect = drag.documentTabGeometry.get(tab) ?? tab.getBoundingClientRect();
        return {
          element: tab,
          kind: "document-center",
          groupId,
          insertionIndex: tabIndex + (x >= rect.left + (rect.width / 2) ? 1 : 0)
        };
      }

      if (tab) {
        return { element: tab, kind: "document-center", groupId };
      }

      return {
        element: group,
        kind: documentTargetKind(
          drag.documentGeometry.get(group) ?? group.getBoundingClientRect(),
          x,
          y),
        groupId
      };
    }

    const panelHeader = hit.closest<HTMLElement>("[data-atlas-panel-header]");
    if (panelHeader?.dataset.atlasGroupId) {
      if (panelHeader.dataset.atlasGroupId === drag.sourceGroupId) {
        return undefined;
      }

      return {
        element: panelHeader,
        kind: "panel-header-insertion",
        groupId: panelHeader.dataset.atlasGroupId,
        insertionIndex: readNonNegativeInteger(panelHeader.dataset.atlasInsertionIndex) ?? 0
      };
    }

    const toolbarEntry = hit.closest<HTMLElement>("[data-atlas-toolbar-entry]");
    if (toolbarEntry?.dataset.atlasGroupId) {
      const targetItemId = toolbarEntry.dataset.atlasItemId;
      if (!targetItemId) {
        return undefined;
      }

      const entryIndex = readNonNegativeInteger(
        toolbarEntry.dataset.atlasInsertionIndex);
      const rect = toolbarEntry.getBoundingClientRect();
      const insertionIndex = entryIndex === undefined
        ? 0
        : rect.height > 0 && y >= rect.top + (rect.height / 2)
          ? entryIndex + 1
          : entryIndex;
      return {
        element: toolbarEntry,
        kind: "toolbar-insertion",
        groupId: toolbarEntry.dataset.atlasGroupId,
        targetItemId,
        ...(toolbarEntry.dataset.atlasToolbarRegion === undefined
          ? {}
          : { logicalRegion: toolbarEntry.dataset.atlasToolbarRegion }),
        insertionIndex
      };
    }

    const scaffold = hit.closest<HTMLElement>("[data-atlas-drop-scaffold=\"true\"]");
    if (scaffold?.dataset.atlasGroupId) {
      if (drag.sourceKind === "panel-header"
        && scaffold.dataset.atlasGroupId === drag.sourceGroupId) {
        return undefined;
      }

      const insertionIndex = readNonNegativeInteger(
        scaffold.dataset.atlasInsertionIndex);
      return {
        element: scaffold,
        kind: "drop-scaffold",
        groupId: scaffold.dataset.atlasGroupId,
        ...(scaffold.dataset.atlasToolbarRegion === undefined
          ? {}
          : { logicalRegion: scaffold.dataset.atlasToolbarRegion }),
        ...(insertionIndex === undefined ? {} : { insertionIndex })
      };
    }

    const toolbarRegion = hit.closest<HTMLElement>("[data-atlas-toolbar-region]");
    if (toolbarRegion?.dataset.atlasGroupId) {
      if (drag.sourceKind === "panel-header"
        && toolbarRegion.dataset.atlasGroupId === drag.sourceGroupId) {
        return undefined;
      }

      return {
        element: toolbarRegion,
        kind: "toolbar-region",
        groupId: toolbarRegion.dataset.atlasGroupId,
        ...(toolbarRegion.dataset.atlasToolbarRegion === undefined
          ? {}
          : { logicalRegion: toolbarRegion.dataset.atlasToolbarRegion })
      };
    }

    return undefined;
  }

  private dragSourceFrom(target: EventTarget | null): HTMLElement | null {
    return target instanceof Element
      ? target.closest<HTMLElement>("[data-atlas-drag-kind]")
      : null;
  }

  private captureDocumentGeometry(): ReadonlyMap<HTMLElement, DOMRect> {
    return new Map(
      Array.from(
        this.root.querySelectorAll<HTMLElement>(
          "[data-atlas-group-kind=document][data-atlas-group-id]"))
        .map(group => [group, group.getBoundingClientRect()] as const));
  }

  private captureDocumentTabGeometry(): ReadonlyMap<HTMLElement, DOMRect> {
    return new Map(
      Array.from(
        this.root.querySelectorAll<HTMLElement>(
          "[data-atlas-drag-kind=document-tab][data-atlas-insertion-index]"))
        .map(tab => [tab, tab.getBoundingClientRect()] as const));
  }

  private canMove(source: HTMLElement): boolean {
    const capabilities = Number(source.dataset.atlasCommandCapabilities);
    return Number.isSafeInteger(capabilities)
      && capabilities >= 0
      && (capabilities & 2) === 2;
  }

  private readRootRevision(): number {
    const revision = Number(this.root.dataset.atlasRevision);
    return Number.isSafeInteger(revision) && revision >= 0 ? revision : 0;
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
  if (identity.protocolVersion !== ATLAS_PROTOCOL_VERSION) {
    throw new Error("ATLAS-INTERACTION-ATTACH-REJECTED");
  }

  const previous = controllers.get(identity.hostId);
  if (previous !== undefined) {
    disposeAtlasController(previous);
  }

  const controller = new AtlasInteractionController(
    resolveHostElement(root, identity.hostId),
    identity,
    receiver,
    options);
  controllers.set(identity.hostId, controller);
  return controller;
}

/**
 * Resolves the Host root element handed over by Blazor.
 *
 * Blazor's ElementReference reviver resolves `{__internalId: ...}` wrappers by
 * querying the element's `_bl_<id>` capture attribute. In WebAssembly the
 * reviver can run before the current render batch has applied that attribute,
 * which produces `null` even though the workspace element is about to appear
 * in the DOM. Fall back to the Host's stable identity attribute, and signal a
 * retry when neither lookup can find the element yet.
 */
function resolveHostElement(root: HTMLElement, hostId: string): HTMLElement {
  if (root instanceof HTMLElement) {
    return root;
  }

  const host = document.querySelector(`[data-atlas-host-id="${hostId}"]`);
  if (host instanceof HTMLElement) {
    return host;
  }

  throw new Error("ATLAS-INTERACTION-ATTACH-RETRY");
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
  for (const [hostId, registered] of controllers) {
    if (registered === controller) {
      controllers.delete(hostId);
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

function isDragSourceKind(value: string): value is AtlasDragSourceKind {
  return value === "document-tab"
    || value === "panel-header"
    || value === "toolbar-entry";
}

function readNonNegativeInteger(value: string | undefined): number | undefined {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : undefined;
}

function documentTargetKind(rect: DOMRect, x: number, y: number): AtlasDropTargetKind {
  const localX = x - rect.left;
  const localY = y - rect.top;
  const inlineBand = rect.width * 0.25;
  const blockBand = rect.height * 0.25;
  const distances = [
    { kind: "document-inline-start" as const, distance: localX, enabled: localX < inlineBand },
    { kind: "document-inline-end" as const, distance: rect.width - localX, enabled: localX > rect.width - inlineBand },
    { kind: "document-block-start" as const, distance: localY, enabled: localY < blockBand },
    { kind: "document-block-end" as const, distance: rect.height - localY, enabled: localY > rect.height - blockBand }
  ].filter(candidate => candidate.enabled)
    .sort((left, right) => left.distance - right.distance);
  return distances[0]?.kind ?? "document-center";
}
