import type { AtlasControllerIdentity } from "./protocol";
import domMetadata from "./dom-metadata-manifest.json";

export const ATLAS_SANDBOX_PROTOCOL_VERSION = 1 as const;

export interface AtlasSandboxReadyMessage {
  readonly type: "atlas-sandbox-ready";
  readonly protocolVersion: typeof ATLAS_SANDBOX_PROTOCOL_VERSION;
  readonly workspaceId: string;
  readonly itemId: string;
  readonly generation: number;
}

export interface AtlasSandboxMessageContext {
  readonly workspaceId: string;
  readonly itemId: string;
  readonly generation: number;
  readonly maxMessageBytes: number;
}

export type AtlasSandboxRejectionReason =
  | "source"
  | "origin"
  | "size"
  | "shape"
  | "version"
  | "identity"
  | "stale"
  | "command";

export type AtlasSandboxValidationResult =
  | { readonly accepted: true; readonly message: AtlasSandboxReadyMessage }
  | { readonly accepted: false; readonly reason: AtlasSandboxRejectionReason };

type SandboxEvent = Pick<MessageEvent, "data" | "origin" | "source">;

export function validateSandboxMessage(
  event: SandboxEvent,
  frameWindow: Window | null,
  context: AtlasSandboxMessageContext): AtlasSandboxValidationResult {
  if (frameWindow === null || event.source !== frameWindow) {
    return rejected("source");
  }

  if (event.origin !== "null") {
    return rejected("origin");
  }

  const byteLength = serializedByteLength(event.data);
  if (byteLength === null || byteLength > context.maxMessageBytes) {
    return rejected("size");
  }

  if (typeof event.data !== "object" || event.data === null
    || Array.isArray(event.data)) {
    return rejected("shape");
  }

  const candidate = event.data as Record<string, unknown>;
  if (candidate.protocolVersion !== ATLAS_SANDBOX_PROTOCOL_VERSION) {
    return rejected("version");
  }

  if (candidate.workspaceId !== context.workspaceId
    || candidate.itemId !== context.itemId) {
    return rejected("identity");
  }

  if (candidate.generation !== context.generation) {
    return rejected("stale");
  }

  const keys = Object.keys(candidate).sort();
  if (candidate.type !== "atlas-sandbox-ready"
    || keys.join(",") !== "generation,itemId,protocolVersion,type,workspaceId") {
    return rejected("command");
  }

  return {
    accepted: true,
    message: candidate as unknown as AtlasSandboxReadyMessage
  };
}

export class AtlasSandboxController {
  private readonly ownerWindow: Window | null | undefined;
  private disposed = false;

  public constructor(
    private readonly root: HTMLElement,
    private identity: AtlasControllerIdentity) {
    this.ownerWindow = root.ownerDocument?.defaultView;
    this.ownerWindow?.addEventListener("message", this.onMessage);
  }

  public update(identity: AtlasControllerIdentity): void {
    this.identity = identity;
  }

  public dispose(): void {
    if (this.disposed) {
      return;
    }

    this.disposed = true;
    this.ownerWindow?.removeEventListener("message", this.onMessage);
  }

  private readonly onMessage = (event: MessageEvent): void => {
    if (this.disposed) {
      return;
    }

    const frames = this.root.querySelectorAll<HTMLIFrameElement>(
      domMetadata.sandboxFrame.selector);
    for (let index = 0; index < frames.length; index += 1) {
      const frame = frames[index];
      if (!frame) {
        continue;
      }

      if (event.source !== frame.contentWindow) {
        continue;
      }

      const maxMessageBytes = Number(frame.dataset.atlasMaxMessageBytes);
      const result = validateSandboxMessage(event, frame.contentWindow, {
        workspaceId: this.identity.workspaceId,
        itemId: frame.dataset.atlasItemId ?? "",
        generation: this.identity.generation,
        maxMessageBytes: Number.isSafeInteger(maxMessageBytes) && maxMessageBytes > 0
          ? maxMessageBytes
          : 0
      });
      if (result.accepted
        && frame.dataset.atlasWorkspaceId === this.identity.workspaceId
        && Number(frame.dataset.atlasGeneration) === this.identity.generation) {
        frame.dataset.atlasSandboxReady = "true";
      } else {
        this.reportDiagnostic(result.accepted ? "identity" : result.reason);
      }

      return;
    }
  };

  private reportDiagnostic(reason: AtlasSandboxRejectionReason): void {
    this.root.dataset.atlasSandboxDiagnostic = reason;
    if (typeof CustomEvent !== "undefined") {
      this.root.dispatchEvent(new CustomEvent("atlas-sandbox-diagnostic", {
        detail: { diagnosticId: "ATLAS-SANDBOX-MESSAGE-REJECTED", reason }
      }));
    }
  }
}

function rejected(reason: AtlasSandboxRejectionReason): AtlasSandboxValidationResult {
  return { accepted: false, reason };
}

function serializedByteLength(value: unknown): number | null {
  try {
    const serialized = JSON.stringify(value);
    return serialized === undefined
      ? null
      : new TextEncoder().encode(serialized).byteLength;
  } catch {
    return null;
  }
}
