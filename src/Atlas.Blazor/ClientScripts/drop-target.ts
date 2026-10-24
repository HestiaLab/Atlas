import domMetadata from "./dom-metadata-manifest.json";
import type {
  AtlasDragSourceKind,
  AtlasDropTargetKind
} from "./protocol";

export interface DropTarget {
  readonly element: HTMLElement;
  readonly kind: AtlasDropTargetKind;
  readonly groupId: string;
  readonly targetItemId?: string;
  readonly logicalRegion?: string;
  readonly insertionIndex?: number;
}

export interface DropTargetDragContext {
  readonly sourceKind: AtlasDragSourceKind;
  readonly sourceGroupId?: string;
  readonly documentGeometry: ReadonlyMap<HTMLElement, DOMRect>;
  readonly documentTabGeometry: ReadonlyMap<HTMLElement, DOMRect>;
}

export function resolveDropTarget(
  root: HTMLElement,
  ownerDocument: Document | undefined,
  x: number,
  y: number,
  eventTarget: EventTarget | null,
  drag: DropTargetDragContext): DropTarget | undefined {
  const hit = ownerDocument?.elementFromPoint(x, y)
    ?? (eventTarget instanceof Element ? eventTarget : null);
  if (!(hit instanceof Element)
    || hit.closest<HTMLElement>(domMetadata.workspaceRoot.selector) !== root) {
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

    const headerIndex = readNonNegativeInteger(
      panelHeader.dataset.atlasInsertionIndex) ?? 0;
    const rect = panelHeader.getBoundingClientRect();
    // 按标题垂直中点拆分前/后插入，与工具栏条目解析器一致，
    // 使释放位置与高亮插入槽一致。
    const insertionIndex = rect.height > 0
      && y >= rect.top + (rect.height / 2)
      ? headerIndex + 1
      : headerIndex;
    return {
      element: panelHeader,
      kind: "panel-header-insertion",
      groupId: panelHeader.dataset.atlasGroupId,
      insertionIndex
    };
  }

  // 可见的工具 Group 主体是有效的 Panel 放置目标（region-center）：
  // 在此释放会把被拖拽 Panel 移入目标 Group 并激活它，镜像
  // document-center 语义而不创建新分割。ToolBar 条目拖拽仍使用
  // ToolBar-first 启用集。
  const toolGroup = hit.closest<HTMLElement>(
    "[data-atlas-group-kind=tool][data-atlas-group-id]");
  if (drag.sourceKind === "panel-header"
    && toolGroup?.dataset.atlasGroupId
    && toolGroup.dataset.atlasGroupId !== drag.sourceGroupId) {
    return {
      element: toolGroup,
      kind: "panel-region-center",
      groupId: toolGroup.dataset.atlasGroupId
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

export function documentTargetKind(
  rect: DOMRect,
  x: number,
  y: number): AtlasDropTargetKind {
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

function readNonNegativeInteger(
  value: string | undefined): number | undefined {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : undefined;
}
