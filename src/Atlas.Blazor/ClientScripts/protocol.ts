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

export type AtlasInteractionMessage = ResizeSplitCommit | KeyboardCommandIntent;

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
