import assert from "node:assert/strict";
import test from "node:test";

import {
  AtlasTabOverflowObserver,
  focusFirstVisibleAtlasPopupItem,
  measureTabStrip
} from "../ClientScripts/tab-overflow";
import domMetadata from "../ClientScripts/dom-metadata-manifest.json";

interface FakeRect {
  left: number;
  right: number;
  width: number;
}

class FakeTab {
  public readonly dataset: Record<string, string> = {};
  public rect: FakeRect = { left: 0, right: 100, width: 100 };
  public readonly captured: string[] = [];
  public disabled = false;
  public focused = false;

  public setAttribute(name: string, value: string): void {
    this.captured.push(`${name}=${value}`);
  }

  public getBoundingClientRect(): DOMRect {
    return this.rect as DOMRect;
  }

  public focus(): void {
    this.focused = true;
  }
}

class FakeStrip {
  public readonly dataset: Record<string, string> = {};
  public rect: FakeRect = { left: 0, right: 300, width: 300 };
  public tabs: FakeTab[] = [];
  public group: FakeGroup | null = null;
  public ownerDocument: { activeElement: FakeTab | null } = { activeElement: null };

  public querySelectorAll(selector: string): FakeTab[] {
    return selector === "[role=\"tab\"]" ? this.tabs : [];
  }

  public closest(selector: string): FakeGroup | null {
    return selector === domMetadata.groupRegion.selector ? this.group : null;
  }

  public getBoundingClientRect(): DOMRect {
    return this.rect as DOMRect;
  }
}

class FakeMenu {
  public isConnected = false;
  public focused = false;

  public constructor(public readonly items: FakeTab[]) {}

  public querySelectorAll(selector: string): FakeTab[] {
    return selector === "[data-atlas-tab-menu-item]" ? this.items : [];
  }

  public contains(candidate: Node | null): boolean {
    return this.items.includes(candidate as unknown as FakeTab);
  }

  public focus(): void {
    this.focused = true;
  }
}

class FakeGroup {
  public menu: FakeMenu | null = null;

  public constructor(public readonly menuItems: FakeTab[]) {}

  public querySelectorAll(selector: string): FakeTab[] {
    return selector === "[data-atlas-tab-menu-item]" ? this.menuItems : [];
  }

  public querySelector(selector: string): FakeMenu | null {
    return selector === "[data-atlas-tab-menu]" ? this.menu : null;
  }
}

class FakeRoot {
  public constructor(public readonly strips: FakeStrip[]) {}

  public querySelectorAll(selector: string): FakeStrip[] {
    return selector === domMetadata.tabStrip.selector ? this.strips : [];
  }

  public contains(candidate: Node | null): boolean {
    return this.strips.includes(candidate as unknown as FakeStrip);
  }
}

test("measureTabStrip marks clipped tabs and menu items in menu mode", () => {
  const strip = new FakeStrip();
  strip.dataset.atlasOverflowMode = "menu";
  const first = new FakeTab();
  first.dataset.atlasItemId = "first";
  first.rect = { left: 0, right: 120, width: 120 };
  const second = new FakeTab();
  second.dataset.atlasItemId = "second";
  second.rect = { left: 280, right: 400, width: 120 };
  strip.tabs = [first, second];
  const menuSecond = new FakeTab();
  menuSecond.dataset.atlasItemId = "second";
  strip.group = new FakeGroup([menuSecond]);
  const root = new FakeRoot([strip]);

  measureTabStrip(
    strip as unknown as HTMLElement,
    root as unknown as HTMLElement);

  assert.equal(first.dataset.atlasTabClipped, "false");
  assert.equal(second.dataset.atlasTabClipped, "true");
  assert.equal(strip.dataset.atlasOverflowing, "true");
  assert.equal(menuSecond.dataset.atlasTabClipped, "true");

  strip.dataset.atlasOverflowMode = "scroll";
  measureTabStrip(
    strip as unknown as HTMLElement,
    root as unknown as HTMLElement);

  assert.equal(strip.dataset.atlasOverflowing, undefined);
  assert.equal(first.dataset.atlasTabClipped, undefined);
  assert.equal(second.dataset.atlasTabClipped, undefined);
  assert.equal(menuSecond.dataset.atlasTabClipped, undefined);
});

test("measureTabStrip keeps the overflow button hidden when every tab is visible", () => {
  const strip = new FakeStrip();
  strip.dataset.atlasOverflowMode = "menu";
  const tab = new FakeTab();
  tab.dataset.atlasItemId = "only";
  tab.rect = { left: 0, right: 100, width: 100 };
  strip.tabs = [tab];
  strip.group = new FakeGroup([tab]);
  const root = new FakeRoot([strip]);

  measureTabStrip(
    strip as unknown as HTMLElement,
    root as unknown as HTMLElement);

  assert.equal(tab.dataset.atlasTabClipped, "false");
  assert.equal(strip.dataset.atlasOverflowing, "false");
});

test("focus helper targets the first visible overflow menu item", () => {
  const first = new FakeTab();
  first.dataset.atlasItemId = "first";
  first.dataset.atlasTabClipped = "false";
  const second = new FakeTab();
  second.dataset.atlasItemId = "second";
  second.dataset.atlasTabClipped = "true";
  const menu = new FakeMenu([first, second]);
  menu.isConnected = true;

  const globalScope = globalThis as Record<string, unknown>;
  const previousDocument = globalScope.document;
  const previousAnimationFrame = globalScope.requestAnimationFrame;
  let scheduled: FrameRequestCallback | undefined;
  globalScope.document = { activeElement: null };
  globalScope.requestAnimationFrame = (callback: FrameRequestCallback): number => {
    scheduled = callback;
    return 1;
  };
  try {
    focusFirstVisibleAtlasPopupItem(menu as unknown as HTMLElement);
    // 菜单容器立即可聚焦，保证 Escape 在测量前也可用。
    assert.equal(menu.focused, true);
    assert.equal(second.focused, false);
    scheduled?.(0);
    assert.equal(first.focused, false);
    assert.equal(second.focused, true);

    // 标记尚未测量时先停留于菜单容器，下一帧再选择首个可见项。
    first.focused = false;
    second.focused = false;
    menu.focused = false;
    delete first.dataset.atlasTabClipped;
    delete second.dataset.atlasTabClipped;
    globalScope.document = { activeElement: null };
    focusFirstVisibleAtlasPopupItem(menu as unknown as HTMLElement);
    scheduled?.(0);
    assert.equal(menu.focused, true);
    assert.equal(second.focused, false);
  } finally {
    globalScope.document = previousDocument;
    globalScope.requestAnimationFrame = previousAnimationFrame;
  }
});

test("tab overflow observer observes strips and tabs and disconnects on dispose", () => {
  const observed: unknown[] = [];
  let resizeDisconnects = 0;
  let mutationDisconnects = 0;
  let mutationReobservations = 0;
  class FakeResizeObserver {
    public constructor(callback: unknown) {
      assert.equal(typeof callback, "function");
    }

    public observe(target: unknown): void {
      observed.push(target);
    }

    public disconnect(): void {
      resizeDisconnects += 1;
    }
  }

  class FakeMutationObserver {
    public constructor(callback: unknown) {
      assert.equal(typeof callback, "function");
    }

    public observe(): void {
      mutationReobservations += 1;
    }

    public disconnect(): void {
      mutationDisconnects += 1;
    }
  }

  const globalScope = globalThis as Record<string, unknown>;
  const previousResize = globalScope.ResizeObserver;
  const previousMutation = globalScope.MutationObserver;
  globalScope.ResizeObserver = FakeResizeObserver;
  globalScope.MutationObserver = FakeMutationObserver;
  try {
    const strip = new FakeStrip();
    const tab = new FakeTab();
    strip.tabs = [tab];
    const root = new FakeRoot([strip]);
    const observer = new AtlasTabOverflowObserver(
      root as unknown as HTMLElement);

    assert.ok(observed.includes(strip));
    assert.ok(observed.includes(tab));
    assert.equal(mutationReobservations, 1);

    observer.refresh();
    assert.equal(mutationReobservations, 2);

    observer.dispose();
    // refresh 重建观察器时已断开第一组；dispose 断开当前组。
    assert.equal(resizeDisconnects, 2);
    assert.equal(mutationDisconnects, 2);

    observer.refresh();
    assert.equal(mutationReobservations, 2);
  } finally {
    globalScope.ResizeObserver = previousResize;
    globalScope.MutationObserver = previousMutation;
  }
});
