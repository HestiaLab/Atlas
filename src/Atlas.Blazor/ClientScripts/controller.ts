import {
  ATLAS_PROTOCOL_VERSION,
  type AtlasControllerIdentity,
  type AtlasControllerOptions,
  type AtlasDotNetReceiver,
  type AtlasDragSourceKind,
  type AtlasKeyboardCommand,
  type AtlasKeyboardBinding,
  type AtlasInteractionMessage,
  isValidControllerIdentity
} from "./protocol";
import domMetadata from "./dom-metadata-manifest.json";
import { resolveDropTarget } from "./drop-target";
import {
  captureDocumentGeometry,
  captureDocumentTabGeometry,
  clearDropPreview,
  createDragSession,
  DragPreview,
  previewDropTarget,
  type DragSession
} from "./drag-session";
import { AtlasSandboxController } from "./sandbox";
import {
  basisFor,
  clamp,
  extentFor,
  readLimits,
  readRatio,
  readRevision
} from "./split-geometry";
import { SplitResizeSession } from "./split-resize-session";

// 跨控制器实例共享的单调拖拽会话计数器。提交变更后 Blazor 会替换 Host
// 根元素，从而释放并重新挂载控制器；若使用实例内序号，它会从 1 重新开始
// 并复用 Host 已消费的会话 ID，导致第二次拖拽被当作重复会话拒绝
// （consumedDragSessions）。
let globalDragSequence = 0;
// 内置键盘默认值。C# AtlasKeyboardCommandMap.Default 持有 Host 驱动控制器
// 的权威映射，并始终发送其绑定；此副本仅服务于由静态浏览器夹具直接挂载
// 的控制器。两份列表必须保持同步。
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

export class AtlasInteractionController {
  private keyboardStepPixels: number;
  private keyboardCommitDelayMilliseconds: number;
  private keyboardBindings: readonly AtlasKeyboardBinding[];
  private readonly ownerDocument: Document | undefined;
  private readonly ownerWindow: Window | null | undefined;
  private readonly reducedMotionQuery: MediaQueryList | undefined;
  private readonly sandboxController: AtlasSandboxController;
  private readonly resizeSession: SplitResizeSession;
  private readonly dragPreview: DragPreview;
  private activeDrag: DragSession | undefined;
  private dragSequence = 0;
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
    this.dragPreview = new DragPreview(this.ownerDocument, this.ownerWindow);
    this.resizeSession = new SplitResizeSession({
      preview: (split, ratio) => this.preview(split, ratio),
      clearPreview: split => this.clearPreview(split),
      commit: (split, ratio, mode, baseRevision, basis) =>
        this.commitResize(split, ratio, mode, baseRevision, basis)
    });
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
      this.resizeSession.cancel();
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
    this.resizeSession.cancel();
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
        this.resizeSession.cancel();
        globalDragSequence += 1;
        this.dragSequence = globalDragSequence;
        this.activeDrag = createDragSession({
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
            : { sourceGroupId })
        });
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

    this.resizeSession.begin(splitter, split, event);
  };

  private readonly onPointerMove = (event: PointerEvent): void => {
    const drag = this.activeDrag;
    if (drag && event.pointerId === drag.pointerId) {
      let previewCreated = false;
      if (drag.phase === "pressed"
        && Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) >= 4) {
        drag.documentGeometry = captureDocumentGeometry(this.root);
        drag.documentTabGeometry = captureDocumentTabGeometry(this.root);
        drag.phase = "dragging";
        drag.preview = this.dragPreview.create(
          drag,
          event.clientX,
          event.clientY,
          this.findOverlayRoot());
        previewCreated = true;
        this.root.dataset.atlasDragging = "true";
      }

      if (drag.phase === "dragging") {
        if (!previewCreated) {
          this.dragPreview.position(drag.preview, event.clientX, event.clientY);
        }
        previewDropTarget(
          drag,
          resolveDropTarget(
            this.root,
            this.ownerDocument,
            event.clientX,
            event.clientY,
            event.target,
            drag));
      }
      return;
    }

    this.resizeSession.move(event);
  };

  private readonly onPointerUp = (event: PointerEvent): void => {
    const drag = this.activeDrag;
    if (drag && event.pointerId === drag.pointerId) {
      const target = drag.phase === "dragging"
        ? resolveDropTarget(
            this.root,
            this.ownerDocument,
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

    this.resizeSession.finish(event);
  };

  private readonly onPointerCancel = (event: PointerEvent): void => {
    if (this.activeDrag?.pointerId === event.pointerId) {
      this.cancelDrag();
    }
    if (this.resizeSession.matchesPointer(event.pointerId)) {
      this.resizeSession.cancel();
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

    this.resizeSession.cancel();
    const baseRevision = readRevision(split);
    const rect = split.getBoundingClientRect();
    const inline = split.dataset.atlasSplitAxis === "InlineChildren";
    const extent = inline ? rect.width : rect.height;
    if (!Number.isFinite(extent) || extent <= 0) {
      return;
    }

    const limits = readLimits(split, extent);
    // 均分到最近的合法分割位置：原始 0.5 可能超出分割的像素约束
    // （例如带最大宽度的 Dock 面板），从而提交超出有效范围的布局。
    const ratio = clamp(0.5, limits.minimum, limits.maximum);
    this.preview(split, ratio);
    this.commitResize(split, ratio, "equalize", baseRevision, basisFor(split));
  };

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.code === "Escape" && this.activeDrag) {
      event.preventDefault();
      this.cancelDrag();
      return;
    }

    if (event.code === "Escape" && this.resizeSession.active) {
      event.preventDefault();
      this.resizeSession.cancel();
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
            : readRatio(split))
              + (direction * (this.keyboardStepPixels / extentFor(split)));
      const rect = split.getBoundingClientRect();
      const extent = split.dataset.atlasSplitAxis === "InlineChildren"
        ? rect.width
        : rect.height;
      const limits = readLimits(split, extent);
      this.keyboardSplit = split;
      this.keyboardRatio = clamp(requested, limits.minimum, limits.maximum);
      this.preview(split, this.keyboardRatio);
      this.commitResize(split, this.keyboardRatio, "keyboard", null, basisFor(split));
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
    this.resizeSession.cancel();
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

  public clearSplitPreview(splitId: string): void {
    if (this.disposed) {
      return;
    }

    const split = this.root.querySelector<HTMLElement>(
      `[data-atlas-node-id="${splitId}"]`);
    if (!split || this.resizeSession.isResizingSplit(split)) {
      // 未知分割，或该分割正在拖拽：不要清除活动预览。
      return;
    }

    this.clearPreview(split);
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

    clearDropPreview(drag.target);
    this.dragPreview.clear(drag.preview, this.findOverlayRoot());
    this.root.removeAttribute("data-atlas-dragging");
    drag.source.releasePointerCapture?.(drag.pointerId);
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

  private dragSourceFrom(target: EventTarget | null): HTMLElement | null {
    return target instanceof Element
      ? target.closest<HTMLElement>("[data-atlas-drag-kind]")
      : null;
  }

  private canMove(source: HTMLElement): boolean {
    // 冻结的能力契约：data-atlas-command-capabilities 携带 Core
    // DockItemCapabilities 标志枚举的数值（ADR-0002）。1<<1 位是 CanMove；
    // 在 Core 中新增或重新编号能力标志属于公共 API 变更，必须保持该位稳定。
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
    baseRevision: number | null,
    basis?: { readonly basisKind: "proportional" | "fixed-pixels"; readonly anchor: string | undefined }): void {
    const splitId = split.dataset.atlasNodeId;
    if (!splitId) {
      return;
    }

    const extent = extentFor(split);
    const rawPixelExtent = basis?.basisKind === "fixed-pixels" && extent > 0
      ? (basis.anchor === "Second" ? (1 - firstRatio) * extent : firstRatio * extent)
      : undefined;
    // 四舍五入到 0.001px：上面的比例计算会累积双精度噪声
    // （例如 0.25 + 30/100 → 55.00000000000001），远低于视觉分辨率，
    // 但会损害持久化的像素值。
    const pixelExtent = rawPixelExtent === undefined
      ? undefined
      : Math.round(rawPixelExtent * 1000) / 1000;
    this.send({
      type: "resize-split",
      splitId,
      firstRatio,
      mode,
      generation: this.identity.generation,
      baseRevision,
      ...(pixelExtent === undefined
        ? {}
        : { basisKind: "fixed-pixels" as const, pixelExtent })
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

  private isEditable(target: EventTarget | null): boolean {
    return target instanceof Element
      && target.closest("input,textarea,select,[contenteditable=true]") !== null;
  }

  private isCollapseControl(target: EventTarget | null): boolean {
    return target instanceof Element
      && target.closest(domMetadata.collapseControl.selector) !== null;
  }

}

/**
 * 确认分割尺寸提交的结果已可知：Blazor 在已应用提交的渲染落地后调用
 * （此时新样式属性已替换预览内联样式，因此是空操作），或在提交被拒绝时
 * 立即调用，使预览回到已提交比例。
 */
export function clearAtlasSplitPreview(
  controller: AtlasInteractionController,
  splitId: string): void {
  controller.clearSplitPreview(splitId);
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

export {
  attachAtlasController,
  disposeAtlasController,
  updateAtlasController
} from "./controller-registry";
export { documentTargetKind } from "./drop-target";
