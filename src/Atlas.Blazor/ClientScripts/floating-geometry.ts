export interface AtlasFloatingGeometry {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

export interface AtlasRectLike {
  readonly left: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
}

export interface AtlasHostSize {
  readonly width: number;
  readonly height: number;
}

export const ATLAS_FLOATING_MIN_WIDTH = 240;
export const ATLAS_FLOATING_MIN_HEIGHT = 160;
export const ATLAS_FLOATING_DEFAULT_WIDTH = 360;
export const ATLAS_FLOATING_DEFAULT_HEIGHT = 480;

export function rootRelativeGeometry(
  root: AtlasRectLike,
  rect: AtlasRectLike): AtlasFloatingGeometry {
  return {
    x: rect.left - root.left,
    y: rect.top - root.top,
    width: rect.width,
    height: rect.height
  };
}

export function clampFloatingGeometry(
  geometry: AtlasFloatingGeometry,
  host: AtlasHostSize): AtlasFloatingGeometry {
  const width = Math.max(
    ATLAS_FLOATING_MIN_WIDTH,
    Math.min(finiteOr(geometry.width, ATLAS_FLOATING_DEFAULT_WIDTH), host.width));
  const height = Math.max(
    ATLAS_FLOATING_MIN_HEIGHT,
    Math.min(finiteOr(geometry.height, ATLAS_FLOATING_DEFAULT_HEIGHT), host.height));
  return {
    x: clampPosition(geometry.x, host.width, width),
    y: clampPosition(geometry.y, host.height, height),
    width,
    height
  };
}

export function resizeFloatingGeometry(
  start: AtlasFloatingGeometry,
  direction: string,
  deltaX: number,
  deltaY: number,
  host: AtlasHostSize): AtlasFloatingGeometry {
  const west = direction.includes("w");
  const east = direction.includes("e");
  const north = direction.includes("n");
  const south = direction.includes("s");
  let left = start.x;
  let top = start.y;
  let width = start.width;
  let height = start.height;

  if (east) {
    width = start.width + deltaX;
  }

  if (west) {
    width = start.width - deltaX;
    left = start.x + deltaX;
  }

  if (south) {
    height = start.height + deltaY;
  }

  if (north) {
    height = start.height - deltaY;
    top = start.y + deltaY;
  }

  // 越界时保持对侧边缘不动，先裁剪尺寸再钳制位置。
  if (west && left < 0) {
    width += left;
    left = 0;
  }

  if (north && top < 0) {
    height += top;
    top = 0;
  }

  width = Math.min(width, host.width - left);
  height = Math.min(height, host.height - top);
  if (width < ATLAS_FLOATING_MIN_WIDTH) {
    if (west) {
      left = Math.max(0, left - (ATLAS_FLOATING_MIN_WIDTH - width));
    }

    width = ATLAS_FLOATING_MIN_WIDTH;
  }

  if (height < ATLAS_FLOATING_MIN_HEIGHT) {
    if (north) {
      top = Math.max(0, top - (ATLAS_FLOATING_MIN_HEIGHT - height));
    }

    height = ATLAS_FLOATING_MIN_HEIGHT;
  }

  return clampFloatingGeometry({ x: left, y: top, width, height }, host);
}

function clampPosition(value: number, extent: number, size: number): number {
  const position = finiteOr(value, 0);
  return Math.max(0, Math.min(position, Math.max(0, extent - size)));
}

function finiteOr(value: number, fallback: number): number {
  return Number.isFinite(value) ? value : fallback;
}
