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
  attachAtlasController,
  disposeAtlasController,
  updateAtlasController
} from "./controller";
