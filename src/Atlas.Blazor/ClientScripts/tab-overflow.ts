import domMetadata from "./dom-metadata-manifest.json";

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
 * 测量单个标签条并把结果写入 data-atlas-* 标记。菜单模式下标签页与
 * 同一 Group 作用域的菜单项共享裁剪标记；其他模式清除标记。
 */
export function measureTabStrip(strip: HTMLElement, root: HTMLElement): void {
  const tabs = Array.from(
    strip.querySelectorAll<HTMLElement>("[role=\"tab\"]"));
  // 溢出菜单渲染在标签条之外、同一 Group 内；按 Group 作用域匹配菜单项，
  // 避免把其他 Group 的菜单项标记写成同一集合。
  const menuScope = strip.closest<HTMLElement>(domMetadata.groupRegion.selector)
    ?? root;
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

/**
 * 标签条溢出的独立观察器。交互控制器启用时由它持有；交互被禁用时
 * 由 C# Host 经 IJSRuntime 单独挂载，保证任一 Host 同时只有一个 owner。
 */
export class AtlasTabOverflowObserver {
  private resizeObserver: ResizeObserver | undefined;
  private mutationObserver: MutationObserver | undefined;
  private disposed = false;

  public constructor(private readonly root: HTMLElement) {
    this.refresh();
  }

  /**
   * 重建观察目标：ResizeObserver 覆盖容器缩放/字体/DPI 变化，
   * MutationObserver 捕获 Blazor 动态增删的标签条与标签页。
   */
  public refresh(): void {
    if (this.disposed) {
      return;
    }

    if (typeof ResizeObserver !== "undefined") {
      this.resizeObserver?.disconnect();
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
            measureTabStrip(strip, this.root);
          }
        }
      });
      this.resizeObserver = observer;
      for (const strip of Array.from(
        this.root.querySelectorAll<HTMLElement>(domMetadata.tabStrip.selector))) {
        observer.observe(strip);
        for (const tab of Array.from(
          strip.querySelectorAll<HTMLElement>("[role=\"tab\"]"))) {
          observer.observe(tab);
        }

        measureTabStrip(strip, this.root);
      }
    }

    if (typeof MutationObserver !== "undefined") {
      this.mutationObserver?.disconnect();
      const observer = new MutationObserver(() => {
        if (!this.disposed) {
          this.refresh();
        }
      });
      this.mutationObserver = observer;
      observer.observe(this.root, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["data-atlas-overflow-mode"]
      });
    }
  }

  public dispose(): void {
    if (this.disposed) {
      return;
    }

    this.disposed = true;
    this.resizeObserver?.disconnect();
    this.resizeObserver = undefined;
    this.mutationObserver?.disconnect();
    this.mutationObserver = undefined;
  }
}

// 静态溢出观察器的宿主注册表。C# Host 在交互禁用时按 HostId 挂载，
// 重复挂载会先释放旧实例，避免根元素替换后遗留观察器。
const overflowObservers = new Map<string, AtlasTabOverflowObserver>();

/**
 * 内部辅助入口：在交互控制器未启用时测量标签页溢出。不属于 C#↔JS
 * 交互协议，不参与协议版本或消息类型。
 */
export function attachAtlasTabOverflowObserver(
  root: HTMLElement | null,
  hostId: string): AtlasTabOverflowObserver {
  const previous = overflowObservers.get(hostId);
  previous?.dispose();

  const host = resolveOverflowRoot(root, hostId);
  const observer = new AtlasTabOverflowObserver(host);
  overflowObservers.set(hostId, observer);
  return observer;
}

/**
 * 内部辅助入口：释放 C# Host 持有的静态溢出观察器。
 */
export function disposeAtlasTabOverflowObserver(
  observer: AtlasTabOverflowObserver): void {
  for (const [hostId, registered] of overflowObservers) {
    if (registered === observer) {
      overflowObservers.delete(hostId);
      break;
    }
  }

  observer.dispose();
}

function resolveOverflowRoot(
  root: HTMLElement | null,
  hostId: string): HTMLElement {
  if (typeof HTMLElement !== "undefined" && root instanceof HTMLElement) {
    return root;
  }

  const host = typeof document !== "undefined"
    ? document.querySelector<HTMLElement>(`[data-atlas-host-id="${hostId}"]`)
    : null;
  if (host) {
    return host;
  }

  // 内部挂载诊断，与 controller-registry 的重试标记保持一致。
  throw new Error("ATLAS-OVERFLOW-ATTACH-RETRY");
}

/**
 * 内部辅助入口：把焦点放到溢出菜单的第一个可见（未被裁剪）项。
 * 菜单全部标签项始终渲染，未裁剪项由 CSS display:none 隐藏；
 * 浏览器无法聚焦隐藏元素，因此该聚焦必须在测量完成后执行。
 */
export function focusFirstVisibleAtlasPopupItem(menu: HTMLElement): void {
  if (!menu.isConnected) {
    return;
  }

  // 首次渲染时 Blazor 可能已把焦点放到一个尚未被 CSS 隐藏的首项上；
  // 该焦点会在测量后丢失，因此这里不保留首帧焦点。先聚焦菜单容器，
  // 使 Escape/Tab 立即可用；随后等待测量标记出现，再把焦点移到
  // 第一个未被裁剪的项。测量由 MutationObserver 异步触发，
  // 因此这里保留有限次重试，避免与首帧聚焦竞争。
  menu.focus({ preventScroll: true });
  const schedule = typeof requestAnimationFrame === "function"
    ? requestAnimationFrame
    : (callback: FrameRequestCallback): number =>
      setTimeout(() => callback(0), 0) as unknown as number;
  let attempts = 20;
  const focusFirstVisible = (): void => {
    if (!menu.isConnected) {
      return;
    }

    const items = Array.from(
      menu.querySelectorAll<HTMLButtonElement>("[data-atlas-tab-menu-item]"));
    const measured = items.some(
      item => item.dataset.atlasTabClipped !== undefined);
    if (!measured) {
      if (attempts-- > 0) {
        schedule(focusFirstVisible);
      }

      return;
    }

    const firstVisible = items.find(
      item => item.dataset.atlasTabClipped !== "false" && !item.disabled);
    (firstVisible ?? menu).focus({ preventScroll: true });
  };
  schedule(focusFirstVisible);
}
