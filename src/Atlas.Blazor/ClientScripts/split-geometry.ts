export interface SplitLimits {
  readonly minimum: number;
  readonly maximum: number;
}

export interface SplitBasisDescriptor {
  readonly basisKind: "proportional" | "fixed-pixels";
  readonly anchor: string | undefined;
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

export function readLimits(split: HTMLElement, extent: number): SplitLimits {
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

export function basisFor(split: HTMLElement): SplitBasisDescriptor {
  return {
    basisKind: split.dataset.atlasSplitBasisKind === "FixedPixels"
      ? "fixed-pixels"
      : "proportional",
    anchor: split.dataset.atlasSplitAnchor
  };
}
