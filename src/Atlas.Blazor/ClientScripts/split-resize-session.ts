import {
  basisFor,
  clamp,
  readLimits,
  readRatio,
  readRevision,
  type SplitBasisDescriptor
} from "./split-geometry";

interface SplitResizeState {
  readonly splitter: HTMLElement;
  readonly split: HTMLElement;
  readonly pointerId: number;
  readonly startCoordinate: number;
  readonly startRatio: number;
  readonly extent: number;
  readonly minimumRatio: number;
  readonly maximumRatio: number;
  readonly baseRevision: number;
  readonly basis: SplitBasisDescriptor;
  currentRatio: number;
}

export interface SplitResizeCallbacks {
  preview(split: HTMLElement, ratio: number): void;
  clearPreview(split: HTMLElement): void;
  commit(
    split: HTMLElement,
    ratio: number,
    mode: "resize",
    baseRevision: number,
    basis: SplitBasisDescriptor): void;
}

export class SplitResizeSession {
  private state: SplitResizeState | undefined;

  public constructor(private readonly callbacks: SplitResizeCallbacks) {
  }

  public get active(): boolean {
    return this.state !== undefined;
  }

  public begin(
    splitter: HTMLElement,
    split: HTMLElement,
    event: PointerEvent): boolean {
    const rect = split.getBoundingClientRect();
    const inline = split.dataset.atlasSplitAxis === "InlineChildren";
    const extent = inline ? rect.width : rect.height;
    if (!Number.isFinite(extent) || extent <= 0) {
      return false;
    }

    const startRatio = readRatio(split);
    const limits = readLimits(split, extent);
    this.state = {
      splitter,
      split,
      pointerId: event.pointerId,
      startCoordinate: inline ? event.clientX : event.clientY,
      startRatio,
      extent,
      minimumRatio: limits.minimum,
      maximumRatio: limits.maximum,
      baseRevision: readRevision(split),
      basis: basisFor(split),
      currentRatio: startRatio
    };
    splitter.setPointerCapture?.(event.pointerId);
    split.dataset.atlasResizing = "true";
    event.preventDefault();
    return true;
  }

  public move(event: PointerEvent): void {
    const state = this.state;
    if (state === undefined || event.pointerId !== state.pointerId) {
      return;
    }

    const inline = state.split.dataset.atlasSplitAxis === "InlineChildren";
    const coordinate = inline ? event.clientX : event.clientY;
    state.currentRatio = clamp(
      state.startRatio + ((coordinate - state.startCoordinate) / state.extent),
      state.minimumRatio,
      state.maximumRatio);
    this.callbacks.preview(state.split, state.currentRatio);
  }

  public finish(event: PointerEvent): void {
    const state = this.state;
    if (state === undefined || event.pointerId !== state.pointerId) {
      return;
    }

    this.state = undefined;
    state.split.removeAttribute("data-atlas-resizing");
    state.splitter.releasePointerCapture?.(event.pointerId);
    if (state.currentRatio !== state.startRatio) {
      // 在提交往返 Blazor 期间保持预览（拖拽位置）不动。若在此同步清除，
      // 分割会退回先前比例，直到 Blazor 用已提交比例重新渲染——这就是释放
      // 时可见的跳动。Blazor 在提交结果可知后通过 clearAtlasSplitPreview
      // 确认清除。
      this.callbacks.commit(
        state.split,
        state.currentRatio,
        "resize",
        state.baseRevision,
        state.basis);
    } else {
      this.callbacks.clearPreview(state.split);
    }
  }

  public cancel(): void {
    const state = this.state;
    this.state = undefined;
    if (state === undefined) {
      return;
    }

    state.split.removeAttribute("data-atlas-resizing");
    this.callbacks.clearPreview(state.split);
    state.splitter.releasePointerCapture?.(state.pointerId);
  }

  public isResizingSplit(split: HTMLElement): boolean {
    return this.state?.split === split;
  }

  public matchesPointer(pointerId: number): boolean {
    return this.state?.pointerId === pointerId;
  }
}
