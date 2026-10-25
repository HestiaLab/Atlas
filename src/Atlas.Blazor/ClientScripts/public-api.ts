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

// 内部辅助入口：交互禁用时由 C# Host 单独挂载标签页溢出观察器，
// 并在测量后聚焦首个可见菜单项。这些导出不是交互协议消息，
// 也不改变既有控制器语义。
export {
  attachAtlasTabOverflowObserver,
  disposeAtlasTabOverflowObserver,
  focusFirstVisibleAtlasPopupItem
} from "./tab-overflow";

// 内部辅助入口：键盘打开的菜单按锚点测量定位；不新增协议消息。
export { positionAtlasPopup } from "./popup-position";

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
