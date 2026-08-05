export const ATLAS_PROTOCOL_VERSION = 1 as const;

export interface AtlasControllerIdentity {
  readonly hostId: string;
  readonly workspaceId: string;
  readonly generation: number;
  readonly protocolVersion: typeof ATLAS_PROTOCOL_VERSION;
}

export interface ResizeSplitCommit {
  readonly type: "resize-split";
  readonly splitId: string;
  readonly firstRatio: number;
  readonly mode: "resize" | "keyboard" | "equalize";
  readonly generation: number;
  readonly baseRevision: number | null;
  /**
   * Present only when the split's basis is FixedPixels; the anchored child's
   * extent in CSS pixels. Older hosts ignore the field and degrade to the
   * proportional interpretation of firstRatio.
   */
  readonly basisKind?: "proportional" | "fixed-pixels";
  readonly pixelExtent?: number;
}

export type AtlasKeyboardCommand =
  | "next-tab"
  | "previous-tab"
  | "close-active-document"
  | "focus-inline-start"
  | "focus-inline-end"
  | "focus-block-start"
  | "focus-block-end";

export interface KeyboardCommandIntent {
  readonly type: "keyboard-command";
  readonly command: AtlasKeyboardCommand;
  readonly generation: number;
  readonly sourceGroupId?: string;
  readonly targetGroupId?: string;
}

export type AtlasDragSourceKind =
  | "document-tab"
  | "panel-header"
  | "toolbar-entry";

export type AtlasDropTargetKind =
  | "document-center"
  | "document-inline-start"
  | "document-inline-end"
  | "document-block-start"
  | "document-block-end"
  | "panel-header-insertion"
  | "panel-region-center"
  | "toolbar-insertion"
  | "toolbar-region"
  | "drop-scaffold";

export interface DockItemCommit {
  readonly type: "dock-item";
  readonly protocolVersion: typeof ATLAS_PROTOCOL_VERSION;
  readonly sessionId: string;
  readonly hostId: string;
  readonly workspaceId: string;
  readonly generation: number;
  readonly itemId: string;
  readonly sourceKind: AtlasDragSourceKind;
  readonly baseRevision: number;
  readonly targetKind: AtlasDropTargetKind;
  readonly targetGroupId: string;
  readonly targetItemId?: string;
  readonly logicalRegion?: string;
  readonly insertionIndex?: number;
  readonly newGroupId?: string;
  readonly newSplitId?: string;
}

export type AtlasInteractionMessage =
  | ResizeSplitCommit
  | KeyboardCommandIntent
  | DockItemCommit;

export interface AtlasDotNetReceiver {
  invokeMethodAsync(method: "OnInteraction", message: AtlasInteractionMessage): Promise<unknown>;
}

export interface AtlasControllerOptions {
  readonly keyboardStepPixels?: number;
  readonly keyboardCommitDelayMilliseconds?: number;
  readonly keyboardBindings?: readonly AtlasKeyboardBinding[];
}

export interface AtlasKeyboardBinding {
  readonly code: string;
  readonly control: boolean;
  readonly shift: boolean;
  readonly alt: boolean;
  readonly meta: boolean;
  readonly command: AtlasKeyboardCommand;
  readonly isGlobal: boolean;
}

export function isSupportedProtocolVersion(value: number): value is typeof ATLAS_PROTOCOL_VERSION {
  return value === ATLAS_PROTOCOL_VERSION;
}

export function isValidControllerIdentity(
  value: unknown): value is AtlasControllerIdentity {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;
  return typeof candidate.protocolVersion === "number"
    && isSupportedProtocolVersion(candidate.protocolVersion)
    && typeof candidate.hostId === "string"
    && candidate.hostId.length > 0
    && typeof candidate.workspaceId === "string"
    && candidate.workspaceId.length > 0
    && typeof candidate.generation === "number"
    && Number.isSafeInteger(candidate.generation)
    && candidate.generation >= 0;
}
