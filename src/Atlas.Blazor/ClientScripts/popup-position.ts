export interface PopupAnchorRect {
  readonly left: number;
  readonly top: number;
  readonly right: number;
  readonly bottom: number;
  readonly width: number;
  readonly height: number;
}

export interface PopupSize {
  readonly width: number;
  readonly height: number;
}

export interface PopupViewport {
  readonly width: number;
  readonly height: number;
}

export interface PopupPosition {
  readonly left: number;
  readonly top: number;
}

/**
 * 计算菜单相对锚点的视口内物理坐标：优先在锚点下方展开，
 * 下方空间不足时翻转到上方，最后把菜单完整钳制在视口内。
 * 使用物理坐标，因此 LTR/RTL 共用同一套边界判定。
 */
export function clampPopupPosition(
  anchor: PopupAnchorRect,
  menu: PopupSize,
  viewport: PopupViewport,
  gap = 4): PopupPosition {
  const menuWidth = Number.isFinite(menu.width) && menu.width > 0 ? menu.width : 0;
  const menuHeight = Number.isFinite(menu.height) && menu.height > 0 ? menu.height : 0;
  const maxLeft = Math.max(0, viewport.width - menuWidth);
  const maxTop = Math.max(0, viewport.height - menuHeight);

  const below = anchor.bottom + gap;
  const above = anchor.top - gap - menuHeight;
  const top = below + menuHeight > viewport.height && above >= 0 ? above : below;

  return {
    left: Math.min(Math.max(0, anchor.left), maxLeft),
    top: Math.min(Math.max(0, top), maxTop)
  };
}

/**
 * 键盘打开菜单时按锚点测量并写入物理 left/top，同时清除 CSS 回退定位的
 * 逻辑 inset，确保菜单完整落在视口内。
 */
export function positionAtlasPopup(
  menu: HTMLElement,
  anchor: HTMLElement): void {
  const anchorRect = anchor.getBoundingClientRect();
  const menuRect = menu.getBoundingClientRect();
  const viewport = {
    width: typeof window === "undefined" ? menuRect.width : window.innerWidth,
    height: typeof window === "undefined" ? menuRect.height : window.innerHeight
  };
  const position = clampPopupPosition(
    anchorRect,
    { width: menuRect.width, height: menuRect.height },
    viewport);

  menu.style.setProperty("inset-inline-end", "auto");
  menu.style.setProperty("inset-block-start", "auto");
  menu.style.left = `${position.left}px`;
  menu.style.top = `${position.top}px`;
}
