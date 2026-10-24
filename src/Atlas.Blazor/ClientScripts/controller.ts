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
// 自动隐藏 Flyout 的移出收起延迟（REQ-F-023 AC3）。
const autoHideCloseDelayMilliseconds = 300;
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
  // 跨过阈值的拖拽结束后，浏览器仍可能对拖拽源派发原生 click。该标记让
  // 控制器一次性拦截紧随其后的 click，避免 Showcase 等调用方把拖拽误判为
  // 普通点击（例如 ToolBar Entry 切换分组可见性）。
  private suppressNextClick = false;
  // ESC/失焦在指针仍按下时结束会话；此时真正的 click 要等到松开才发生，
  // 因此延迟到对应 pointerup 再安装监听，避免 setTimeout(0) 提前清理。
  private pendingClickPointerId: number | undefined;
  private clickSuppression:
    { readonly listener: (event: Event) => void; readonly timer: ReturnType<typeof setTimeout> } | undefined;
  private tabResizeObserver: ResizeObserver | undefined;
  private tabMutationObserver: MutationObserver | undefined;
  private readonly autoHideCloseTimers =
    new Map<HTMLElement, ReturnType<typeof setTimeout>>();

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
    this.root.addEventListener(
      "wheel",
      this.onWheel as EventListener,
      { passive: false } as AddEventListenerOptions);
    this.root.addEventListener("pointerover", this.onAutoHidePointerOver);
    this.root.addEventListener("pointerout", this.onAutoHidePointerOut);
    this.root.addEventListener("focusout", this.onAutoHideFocusOut);
    this.ownerWindow?.addEventListener("resize", this.onEnvironmentInvalidated);
    this.ownerWindow?.addEventListener("blur", this.onEnvironmentInvalidated);
    this.ownerWindow?.addEventListener("pointerup", this.onPointerUp);
    this.ownerWindow?.addEventListener("pointercancel", this.onPointerCancel);
    this.ownerDocument?.addEventListener(
      "visibilitychange",
      this.onVisibilityChanged);
    // 拖拽源（Panel Header、ToolBar Entry）通常不持有焦点，Escape 不会
    // 冒泡到宿主根；在 document 上补充监听，确保任何焦点位置都能取消。
    this.ownerDocument?.addEventListener("keydown", this.onDocumentKeyDown);
    this.reducedMotionQuery?.addEventListener("change", this.onReducedMotionChanged);
    this.projectReducedMotion();
    this.refreshTabOverflow();
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
    this.suppressNextClick = false;
    this.pendingClickPointerId = undefined;
    this.clearClickSuppression();
    this.clearKeyboardCommit();
    this.sandboxController.dispose();
    this.closeAllAutoHide();
    this.root.removeEventListener("pointerover", this.onAutoHidePointerOver);
    this.root.removeEventListener("pointerout", this.onAutoHidePointerOut);
    this.root.removeEventListener("focusout", this.onAutoHideFocusOut);
    this.root.removeEventListener("pointerdown", this.onPointerDown);
    this.root.removeEventListener("pointermove", this.onPointerMove);
    this.root.removeEventListener("pointerup", this.onPointerUp);
    this.root.removeEventListener("pointercancel", this.onPointerCancel);
    this.root.removeEventListener("lostpointercapture", this.onPointerCancel);
    this.root.removeEventListener("dblclick", this.onDoubleClick);
    this.root.removeEventListener("keydown", this.onKeyDown);
    this.root.removeEventListener("keyup", this.onKeyUp);
    this.root.removeEventListener("wheel", this.onWheel as EventListener);
    this.tabResizeObserver?.disconnect();
    this.tabResizeObserver = undefined;
    this.tabMutationObserver?.disconnect();
    this.tabMutationObserver = undefined;
    this.ownerWindow?.removeEventListener("resize", this.onEnvironmentInvalidated);
    this.ownerWindow?.removeEventListener("blur", this.onEnvironmentInvalidated);
    this.ownerWindow?.removeEventListener("pointerup", this.onPointerUp);
    this.ownerWindow?.removeEventListener("pointercancel", this.onPointerCancel);
    this.ownerDocument?.removeEventListener(
      "visibilitychange",
      this.onVisibilityChanged);
    this.ownerDocument?.removeEventListener("keydown", this.onDocumentKeyDown);
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
    // 新的指针交互开始：尚未消费的拖拽后 click 抑制立即失效，后续点击
    // 必须恢复正常。拖拽进行中的多指按下不打断当前会话。
    if (!this.activeDrag) {
      this.suppressNextClick = false;
      this.pendingClickPointerId = undefined;
      this.clearClickSuppression();
    }

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
        this.suppressNextClick = true;
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

    // 取消路径（ESC/失焦）在指针仍按下时结束会话；松开时浏览器会派发
    // click，必须在此刻同步安装一次性监听。
    if (this.pendingClickPointerId === event.pointerId) {
      this.pendingClickPointerId = undefined;
      this.suppressNextClick = false;
      this.armClickSuppression();
    }

    this.resizeSession.finish(event);
  };

  private readonly onPointerCancel = (event: PointerEvent): void => {
    if (this.activeDrag?.pointerId === event.pointerId) {
      // pointercancel / lostpointercapture 是终止路径，立即安装兜底监听。
      this.cancelDrag(false);
    } else if (event.type === "pointercancel"
      && this.pendingClickPointerId === event.pointerId) {
      // 指针被取消不会产生 click；清除延迟抑制。lostpointercapture 不能
      // 清除：ESC 释放捕获时它会在松开前触发。
      this.pendingClickPointerId = undefined;
      this.suppressNextClick = false;
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

  private readonly onWheel = (event: WheelEvent): void => {
    // 滚动溢出模式：纵向滚轮转换为标签条的水平滚动（REQ-F-043 AC1）。
    // 原生横向滚动（触控板/Shift+滚轮）保持浏览器默认行为。
    const strip = event.target instanceof Element
      ? event.target.closest<HTMLElement>(domMetadata.tabStrip.selector)
      : null;
    if (!strip || this.disposed
      || strip.dataset.atlasOverflowMode !== "scroll"
      || Math.abs(event.deltaY) <= Math.abs(event.deltaX)) {
      return;
    }

    if (strip.scrollWidth <= strip.clientWidth) {
      return;
    }

    const direction = this.ownerWindow?.getComputedStyle(strip).direction;
    strip.scrollLeft += tabOverflowWheelDelta(event.deltaY, direction);
    event.preventDefault();
  };

  private readonly onAutoHidePointerOver = (event: PointerEvent): void => {
    if (this.disposed) {
      return;
    }

    const owner = this.autoHideOwnerFrom(event.target);
    if (!owner) {
      return;
    }

    this.cancelAutoHideClose(owner);
    if (owner.dataset.atlasAutoHideOpen !== "true") {
      this.closeAllAutoHide(owner);
      this.openAutoHideOwner(owner);
    }
  };

  private readonly onAutoHidePointerOut = (event: PointerEvent): void => {
    if (this.disposed) {
      return;
    }

    const owner = this.autoHideOwnerFrom(event.target);
    if (!owner) {
      return;
    }

    // 指针从条目移入同一 Flyout 仍属于同一 owner，不触发收起。
    const related = event.relatedTarget;
    if (related !== null && typeof related === "object"
      && owner.contains(related as Node)) {
      return;
    }

    this.scheduleAutoHideClose(owner);
  };

  private readonly onAutoHideFocusOut = (event: FocusEvent): void => {
    if (this.disposed) {
      return;
    }

    const owner = this.autoHideOwnerFrom(event.target);
    if (!owner) {
      return;
    }

    const related = event.relatedTarget;
    if (related !== null && typeof related === "object"
      && owner.contains(related as Node)) {
      return;
    }

    this.scheduleAutoHideClose(owner);
  };

  private autoHideOwnerFrom(target: EventTarget | null): HTMLElement | null {
    return target instanceof Element
      ? target.closest<HTMLElement>(domMetadata.autoHideOwner.selector)
      : null;
  }

  private openAutoHideOwner(owner: HTMLElement): void {
    owner.dataset.atlasAutoHideOpen = "true";
    owner.querySelector<HTMLElement>(domMetadata.autoHideEntry.selector)
      ?.setAttribute("aria-expanded", "true");
    const flyout = owner.querySelector<HTMLElement>(
      domMetadata.autoHideFlyout.selector);
    if (flyout) {
      flyout.setAttribute("aria-hidden", "false");
      flyout.removeAttribute("inert");
    }
  }

  private closeAutoHideOwner(owner: HTMLElement): void {
    this.cancelAutoHideClose(owner);
    if (owner.dataset.atlasAutoHideOpen !== "true") {
      return;
    }

    delete owner.dataset.atlasAutoHideOpen;
    const entry = owner.querySelector<HTMLElement>(
      domMetadata.autoHideEntry.selector);
    entry?.setAttribute("aria-expanded", "false");
    const flyout = owner.querySelector<HTMLElement>(
      domMetadata.autoHideFlyout.selector);
    if (flyout) {
      flyout.setAttribute("aria-hidden", "true");
      flyout.setAttribute("inert", "");
      const active = this.ownerDocument?.activeElement;
      if (!this.disposed && active && flyout.contains(active)) {
        entry?.focus();
      }
    }
  }

  private scheduleAutoHideClose(owner: HTMLElement): void {
    if (owner.dataset.atlasAutoHideOpen !== "true") {
      return;
    }

    this.cancelAutoHideClose(owner);
    const timer = setTimeout(() => {
      this.autoHideCloseTimers.delete(owner);
      if (!this.disposed) {
        this.closeAutoHideOwner(owner);
      }
    }, autoHideCloseDelayMilliseconds);
    this.autoHideCloseTimers.set(owner, timer);
  }

  private cancelAutoHideClose(owner: HTMLElement): void {
    const timer = this.autoHideCloseTimers.get(owner);
    if (timer !== undefined) {
      clearTimeout(timer);
      this.autoHideCloseTimers.delete(owner);
    }
  }

  private closeAllAutoHide(except?: HTMLElement): void {
    for (const owner of Array.from(
      this.root.querySelectorAll<HTMLElement>(domMetadata.autoHideOwner.selector))) {
      if (owner === except) {
        continue;
      }

      this.closeAutoHideOwner(owner);
    }

    // 被移除的 owner 不会出现在查询结果中；一并清理其待决计时器。
    for (const owner of Array.from(this.autoHideCloseTimers.keys())) {
      if (owner !== except) {
        this.cancelAutoHideClose(owner);
      }
    }
  }

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    const autoHideOwner = this.autoHideOwnerFrom(event.target);
    if (autoHideOwner) {
      const entry = autoHideOwner.querySelector<HTMLElement>(
        domMetadata.autoHideEntry.selector);
      if ((event.code === "Enter" || event.code === "Space")
        && event.target === entry) {
        event.preventDefault();
        if (autoHideOwner.dataset.atlasAutoHideOpen === "true") {
          this.closeAutoHideOwner(autoHideOwner);
        } else {
          this.closeAllAutoHide(autoHideOwner);
          this.openAutoHideOwner(autoHideOwner);
        }

        return;
      }

      if (event.code === "Escape"
        && autoHideOwner.dataset.atlasAutoHideOpen === "true") {
        event.preventDefault();
        this.closeAutoHideOwner(autoHideOwner);
        entry?.focus();
        return;
      }
    }

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

  private readonly onDocumentKeyDown = (event: KeyboardEvent): void => {
    // 根节点已处理过的 Escape 会 preventDefault；避免重复取消同一次会话。
    if (event.code !== "Escape" || event.defaultPrevented) {
      return;
    }

    if (this.activeDrag) {
      event.preventDefault();
      this.cancelDrag();
      return;
    }

    if (this.resizeSession.active) {
      event.preventDefault();
      this.resizeSession.cancel();
    }
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

  private cancelDrag(deferClickSuppression = true): void {
    if (this.activeDrag) {
      this.finishDrag(deferClickSuppression);
    }
  }

  private finishDrag(deferClickSuppression = false): void {
    const drag = this.activeDrag;
    this.activeDrag = undefined;
    if (!drag) {
      return;
    }

    const suppressClick = this.suppressNextClick;
    this.suppressNextClick = false;
    clearDropPreview(drag.target);
    this.dragPreview.clear(drag.preview, this.findOverlayRoot());
    this.root.removeAttribute("data-atlas-dragging");
    drag.source.releasePointerCapture?.(drag.pointerId);
    if (!suppressClick) {
      return;
    }

    if (deferClickSuppression && drag.phase === "dragging") {
      this.pendingClickPointerId = drag.pointerId;
      this.suppressNextClick = true;
    } else {
      this.armClickSuppression();
    }
  }

  private armClickSuppression(): void {
    this.clearClickSuppression();
    const document = this.ownerDocument;
    if (!document) {
      return;
    }

    // 捕获阶段监听，确保在应用（如 Showcase 条目）的 click 处理之前拦截；
    // 只处理路径包含本 Host root 的事件，避免吞掉其他 Host 的合法点击。
    const listener = (event: Event): void => {
      if (!this.eventTouchesRoot(event)) {
        return;
      }

      this.clearClickSuppression();
      event.preventDefault();
      event.stopPropagation();
    };
    this.clickSuppression = {
      listener,
      // 某些取消路径不会产生 click；下一轮任务开始前清理，防止监听泄漏。
      timer: setTimeout(() => this.clearClickSuppression(), 0)
    };
    // 使用 options 对象而非布尔值：Node 的 EventTarget 在布尔 capture
    // 下移除监听不匹配，浏览器与测试环境需要一致语义。
    document.addEventListener("click", listener, { capture: true });
  }

  private clearClickSuppression(): void {
    const pending = this.clickSuppression;
    this.clickSuppression = undefined;
    if (!pending) {
      return;
    }

    clearTimeout(pending.timer);
    this.ownerDocument?.removeEventListener(
      "click",
      pending.listener,
      { capture: true });
  }

  private refreshTabOverflow(): void {
    if (this.disposed) {
      return;
    }

    // ResizeObserver 覆盖容器缩放、字体/标题变化与 DPI 缩放；
    // MutationObserver 捕获由 Blazor 动态增删的标签条与标签页。
    // 每次重建观察器，避免已移除标签页的观察目标长期滞留。
    if (typeof ResizeObserver !== "undefined") {
      this.tabResizeObserver?.disconnect();
      const observer = new ResizeObserver(entries => {
        if (this.disposed) {
          return;
        }

        const strips = new Set<HTMLElement>();
        for (const entry of entries) {
          const target = entry.target;
          const strip = target instanceof Element
            ? (target.matches(domMetadata.tabStrip.selector)
              ? target as HTMLElement
              : target.closest<HTMLElement>(domMetadata.tabStrip.selector))
            : null;
          if (strip) {
            strips.add(strip);
          }
        }

        for (const strip of strips) {
          if (this.root.contains(strip)) {
            this.measureTabStrip(strip);
          }
        }
      });
      this.tabResizeObserver = observer;
      for (const strip of Array.from(
        this.root.querySelectorAll<HTMLElement>(domMetadata.tabStrip.selector))) {
        observer.observe(strip);
        for (const tab of Array.from(
          strip.querySelectorAll<HTMLElement>("[role=\"tab\"]"))) {
          observer.observe(tab);
        }

        this.measureTabStrip(strip);
      }
    }

    if (typeof MutationObserver !== "undefined") {
      this.tabMutationObserver?.disconnect();
      const observer = new MutationObserver(() => {
        if (!this.disposed) {
          this.refreshTabOverflow();
        }
      });
      this.tabMutationObserver = observer;
      observer.observe(this.root, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["data-atlas-overflow-mode"]
      });
    }
  }

  private measureTabStrip(strip: HTMLElement): void {
    const tabs = Array.from(
      strip.querySelectorAll<HTMLElement>("[role=\"tab\"]"));
    // 溢出菜单渲染在标签条之外、同一 Group 内；按 Group 作用域匹配菜单项，
    // 避免把其他 Group 的菜单项标记写成同一集合。
    const menuScope = strip.closest<HTMLElement>(domMetadata.groupRegion.selector)
      ?? this.root;
    const menuItems = Array.from(
      menuScope.querySelectorAll<HTMLElement>("[data-atlas-tab-menu-item]"));
    if (strip.dataset.atlasOverflowMode !== "menu") {
      delete strip.dataset.atlasOverflowing;
      for (const tab of tabs) {
        delete tab.dataset.atlasTabClipped;
      }

      for (const item of menuItems) {
        delete item.dataset.atlasTabClipped;
      }

      return;
    }

    const stripRect = strip.getBoundingClientRect();
    let overflowing = false;
    const clipped = new Map<string, boolean>();
    for (const tab of tabs) {
      const itemId = tab.dataset.atlasItemId ?? "";
      const isClipped = isTabClipped(tab.getBoundingClientRect(), stripRect);
      overflowing = overflowing || isClipped;
      clipped.set(itemId, isClipped);
      tab.dataset.atlasTabClipped = isClipped ? "true" : "false";
    }

    strip.dataset.atlasOverflowing = overflowing ? "true" : "false";
    // 菜单项可能稍后由 Blazor 插入；MutationObserver 会在插入后再次测量。
    for (const item of menuItems) {
      const itemId = item.dataset.atlasItemId ?? "";
      item.dataset.atlasTabClipped = clipped.get(itemId) === true
        ? "true"
        : "false";
    }
  }

  private eventTouchesRoot(event: Event): boolean {
    const path = typeof event.composedPath === "function"
      ? event.composedPath()
      : undefined;
    if (path && path.length > 0) {
      return path.includes(this.root);
    }

    // 测试假对象或旧环境可能没有 composedPath；退化为 DOM 包含判断。
    const target = event.target;
    return target !== null
      && typeof this.root.contains === "function"
      && this.root.contains(target as Node);
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
    // 折叠按钮与 Panel Header 关闭按钮都是内嵌在拖拽源中的交互控件；
    // 按在这些控件上的指针不能启动拖拽。
    return target instanceof Element
      && (target.closest(domMetadata.collapseControl.selector) !== null
        || target.closest(domMetadata.panelClose.selector) !== null);
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

/**
 * 把纵向滚轮增量转换为标签条的水平滚动方向。RTL 下滚动起点在右侧，
 * 因此方向取反。
 */
export function tabOverflowWheelDelta(
  deltaY: number,
  direction: string | undefined): number {
  if (!Number.isFinite(deltaY) || deltaY === 0) {
    return 0;
  }

  return direction === "rtl" ? -deltaY : deltaY;
}

/**
 * 标签页是否未完全位于标签条可视区域内。使用物理矩形，因此 LTR/RTL
 * 与缩放环境共用同一判定；半像素容差吸收浏览器亚像素舍入。
 */
export function isTabClipped(
  tab: Readonly<{ left: number; right: number; width: number }>,
  strip: Readonly<{ left: number; right: number; width: number }>,
  tolerance = 0.5): boolean {
  if (!Number.isFinite(tab.width) || !Number.isFinite(strip.width)
    || tab.width <= 0 || strip.width <= 0) {
    return false;
  }

  return tab.left < strip.left - tolerance
    || tab.right > strip.right + tolerance;
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
