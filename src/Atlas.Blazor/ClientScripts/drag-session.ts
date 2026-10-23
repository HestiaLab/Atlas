import type { DropTarget } from "./drop-target";
import type { AtlasDragSourceKind } from "./protocol";

export type DragPhase = "pressed" | "dragging";

export interface DragSessionInit {
  readonly source: HTMLElement;
  readonly pointerId: number;
  readonly startX: number;
  readonly startY: number;
  readonly sessionId: string;
  readonly generation: number;
  readonly baseRevision: number;
  readonly itemId: string;
  readonly sourceKind: AtlasDragSourceKind;
  readonly sourceGroupId?: string;
}

export interface DragSession extends DragSessionInit {
  phase: DragPhase;
  documentGeometry: ReadonlyMap<HTMLElement, DOMRect>;
  documentTabGeometry: ReadonlyMap<HTMLElement, DOMRect>;
  preview: HTMLElement | undefined;
  target?: DropTarget;
}

export function createDragSession(init: DragSessionInit): DragSession {
  return {
    ...init,
    phase: "pressed",
    documentGeometry: new Map(),
    documentTabGeometry: new Map(),
    preview: undefined
  };
}

export function captureDocumentGeometry(
  root: HTMLElement): ReadonlyMap<HTMLElement, DOMRect> {
  return new Map(
    Array.from(
      root.querySelectorAll<HTMLElement>(
        "[data-atlas-group-kind=document][data-atlas-group-id]"))
      .map(group => [group, group.getBoundingClientRect()] as const));
}

export function captureDocumentTabGeometry(
  root: HTMLElement): ReadonlyMap<HTMLElement, DOMRect> {
  return new Map(
    Array.from(
      root.querySelectorAll<HTMLElement>(
        "[data-atlas-drag-kind=document-tab][data-atlas-insertion-index]"))
      .map(tab => [tab, tab.getBoundingClientRect()] as const));
}

export class DragPreview {
  private frame: number | undefined;
  private pendingPosition:
    { readonly preview: HTMLElement; readonly x: number; readonly y: number } | undefined;

  public constructor(
    private readonly ownerDocument: Document | undefined,
    private readonly ownerWindow: Window | null | undefined) {
  }

  public create(
    drag: DragSession,
    x: number,
    y: number,
    overlayRoot: HTMLElement | undefined): HTMLElement | undefined {
    const document = this.ownerDocument;
    if (!document || !overlayRoot) {
      return undefined;
    }

    overlayRoot.dataset.atlasDragSessionId = drag.sessionId;

    const preview = document.createElement("div");
    preview.className = "atlas-v2-drag-preview";
    preview.dataset.atlasDragPreview = drag.sourceKind;
    preview.setAttribute("aria-hidden", "true");
    preview.setAttribute("role", "presentation");
    preview.style.setProperty("pointer-events", "none");
    this.writePosition(preview, x, y);

    const label = drag.source.getAttribute("aria-label")?.trim()
      || drag.source.textContent?.trim()
      || drag.itemId;

    const iconKey = drag.source
      .querySelector<HTMLElement>(".atlas-v2-item-icon[data-atlas-icon]")
      ?.dataset.atlasIcon;
    if (iconKey) {
      const icon = document.createElement("span");
      icon.className = "atlas-v2-item-icon";
      icon.dataset.atlasIcon = iconKey;
      preview.appendChild(icon);
    }

    const title = document.createElement("span");
    title.className = "atlas-v2-drag-preview-title";
    title.textContent = label;
    preview.appendChild(title);

    overlayRoot.appendChild(preview);
    return preview;
  }

  public position(
    preview: HTMLElement | undefined,
    x: number,
    y: number): void {
    if (!preview) {
      return;
    }

    this.pendingPosition = { preview, x, y };
    if (this.frame !== undefined) {
      return;
    }

    const flush = (): void => {
      this.frame = undefined;
      const pending = this.pendingPosition;
      this.pendingPosition = undefined;
      if (pending) {
        this.writePosition(pending.preview, pending.x, pending.y);
      }
    };

    if (this.ownerWindow) {
      this.frame = this.ownerWindow.requestAnimationFrame(flush);
    } else {
      flush();
    }
  }

  public clear(
    preview: HTMLElement | undefined,
    overlayRoot: HTMLElement | undefined): void {
    if (this.frame !== undefined) {
      this.ownerWindow?.cancelAnimationFrame(this.frame);
      this.frame = undefined;
    }

    this.pendingPosition = undefined;
    preview?.remove();
    overlayRoot?.removeAttribute("data-atlas-drag-session-id");
  }

  private writePosition(preview: HTMLElement, x: number, y: number): void {
    preview.style.setProperty(
      "transform",
      `translate3d(${x + 12}px, ${y + 12}px, 0)`);
  }
}

export function previewDropTarget(
  drag: DragSession,
  target: DropTarget | undefined): void {
  if (drag.target?.element === target?.element && drag.target?.kind === target?.kind) {
    return;
  }

  clearDropPreview(drag.target);
  if (target) {
    drag.target = target;
    target.element.dataset.atlasDropPreview = target.kind;
  } else {
    delete drag.target;
  }
}

export function clearDropPreview(target: DropTarget | undefined): void {
  target?.element.removeAttribute("data-atlas-drop-preview");
}
