export {
  ATLAS_PROTOCOL_VERSION,
  isValidControllerIdentity,
  isSupportedProtocolVersion
} from "./protocol";

export type {
  AtlasControllerIdentity,
  AtlasControllerOptions,
  AtlasDotNetReceiver,
  AtlasDragSourceKind,
  AtlasDropTargetKind,
  AtlasInteractionMessage,
  AtlasKeyboardBinding,
  AtlasKeyboardCommand,
  KeyboardCommandIntent,
  DockItemCommit,
  ResizeSplitCommit
} from "./protocol";

export {
  AtlasInteractionController,
  clearAtlasSplitPreview
} from "./controller";

export {
  attachAtlasController,
  disposeAtlasController,
  updateAtlasController
} from "./controller-registry";

export {
  ATLAS_SANDBOX_PROTOCOL_VERSION,
  AtlasSandboxController,
  validateSandboxMessage
} from "./sandbox";

export type {
  AtlasSandboxMessageContext,
  AtlasSandboxReadyMessage,
  AtlasSandboxRejectionReason,
  AtlasSandboxValidationResult
} from "./sandbox";
