export {
  ATLAS_PROTOCOL_VERSION,
  isValidControllerIdentity,
  isSupportedProtocolVersion
} from "./protocol";

export type {
  AtlasControllerIdentity,
  AtlasControllerOptions,
  AtlasDotNetReceiver,
  AtlasInteractionMessage,
  AtlasKeyboardBinding,
  AtlasKeyboardCommand,
  KeyboardCommandIntent,
  ResizeSplitCommit
} from "./protocol";

export {
  AtlasInteractionController,
  attachAtlasController,
  disposeAtlasController,
  updateAtlasController
} from "./controller";
