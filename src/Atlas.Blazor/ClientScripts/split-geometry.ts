export interface SplitLimits {
  readonly minimum: number;
  readonly maximum: number;
}

export interface SplitBasisDescriptor {
  readonly basisKind: "proportional" | "fixed-pixels";
  readonly anchor: string | undefined;
}

export interface ResolveBasis {
  readonly kind: "proportional" | "fixed-pixels";
  readonly value: number;
  readonly anchor: "First" | "Second" | undefined;
}

export interface ResolveConstraints {
  readonly firstMinimum: number;
  readonly firstMaximum: number | undefined;
  readonly secondMinimum: number;
  readonly secondMaximum: number | undefined;
}

export interface ResolvedSplit {
  readonly firstExtent: number;
  readonly secondExtent: number;
  readonly isClamped: boolean;
}

export function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(Math.max(value, minimum), maximum);
}

export function extentFor(split: HTMLElement): number {
  const rect = split.getBoundingClientRect();
  const extent = split.dataset.atlasSplitAxis === "InlineChildren"
    ? rect.width
    : rect.height;
  return Number.isFinite(extent) && extent > 0 ? extent : 1;
}

export function readRatio(split: HTMLElement): number {
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

export function readRevision(split: HTMLElement): number {
  const value = Number(split.dataset.atlasRevision);
  return Number.isSafeInteger(value) && value >= 0 ? value : 0;
}

/**
 * 由双方约束推导的可行第一子项比例范围。
 * 镜像 Atlas.Core `SplitLayoutResolver` 语义，使指针与键盘限制
 * 与已提交基准保持同步。
 */
export function feasibleRange(
  availableExtent: number,
  constraints: ResolveConstraints): SplitLimits {
  const firstMaximum = constraints.firstMaximum ?? Number.POSITIVE_INFINITY;
  const secondMaximum = constraints.secondMaximum ?? Number.POSITIVE_INFINITY;
  const minimumTotal = constraints.firstMinimum + constraints.secondMinimum;
  const maximumTotal = firstMaximum + secondMaximum;
  if (minimumTotal > availableExtent) {
    const ratio = minimumTotal === 0
      ? 0.5
      : constraints.firstMinimum / minimumTotal;
    return { minimum: ratio, maximum: ratio };
  }

  if (maximumTotal < availableExtent) {
    const ratio = maximumTotal === 0
      ? 0.5
      : firstMaximum / maximumTotal;
    return { minimum: ratio, maximum: ratio };
  }

  const minimum = Math.max(
    constraints.firstMinimum / availableExtent,
    Number.isFinite(secondMaximum) ? 1 - (secondMaximum / availableExtent) : 0);
  const maximum = Math.min(
    1 - (constraints.secondMinimum / availableExtent),
    Number.isFinite(firstMaximum) ? firstMaximum / availableExtent : 1);
  return {
    minimum: clamp(minimum, 0, 1),
    maximum: clamp(maximum, 0, 1)
  };
}

/**
 * 尺寸空间中的纯分割解析；精确镜像 Atlas.Core
 * `SplitLayoutResolver.Resolve`，以便共享夹具锁定双方实现。
 */
export function resolveSplit(
  availableExtent: number,
  basis: ResolveBasis,
  constraints: ResolveConstraints): ResolvedSplit {
  const preferredFirst = basis.kind === "fixed-pixels"
    ? basis.anchor === "Second"
      ? availableExtent - basis.value
      : basis.value
    : availableExtent * basis.value;
  const preferredSecond = availableExtent - preferredFirst;

  const firstMaximum = constraints.firstMaximum ?? Number.POSITIVE_INFINITY;
  const secondMaximum = constraints.secondMaximum ?? Number.POSITIVE_INFINITY;
  const minimumTotal = constraints.firstMinimum + constraints.secondMinimum;
  const maximumTotal = firstMaximum + secondMaximum;

  if (minimumTotal > availableExtent) {
    return {
      firstExtent: constraints.firstMinimum,
      secondExtent: constraints.secondMinimum,
      isClamped: true
    };
  }

  if (maximumTotal < availableExtent) {
    return {
      firstExtent: firstMaximum,
      secondExtent: secondMaximum,
      isClamped: true
    };
  }

  const feasibleFirstMinimum = Math.max(
    constraints.firstMinimum,
    availableExtent - secondMaximum);
  const feasibleFirstMaximum = Math.min(
    firstMaximum,
    availableExtent - constraints.secondMinimum);

  const resolvedFirst = clamp(
    preferredFirst,
    feasibleFirstMinimum,
    feasibleFirstMaximum);
  const resolvedSecond = availableExtent - resolvedFirst;
  return {
    firstExtent: resolvedFirst,
    secondExtent: resolvedSecond,
    isClamped: resolvedFirst !== preferredFirst
      || resolvedSecond !== preferredSecond
  };
}

export function readLimits(split: HTMLElement, extent: number): SplitLimits {
  return feasibleRange(extent, {
    firstMinimum: Number(split.dataset.atlasFirstMinimum) || 0,
    firstMaximum: toFiniteOrUndefined(split.dataset.atlasFirstMaximum),
    secondMinimum: Number(split.dataset.atlasSecondMinimum) || 0,
    secondMaximum: toFiniteOrUndefined(split.dataset.atlasSecondMaximum)
  });
}

export function basisFor(split: HTMLElement): SplitBasisDescriptor {
  return {
    basisKind: split.dataset.atlasSplitBasisKind === "FixedPixels"
      ? "fixed-pixels"
      : "proportional",
    anchor: split.dataset.atlasSplitAnchor
  };
}

function toFiniteOrUndefined(value: string | undefined): number | undefined {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}
