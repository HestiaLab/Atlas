import { AtlasInteractionController } from "./controller";
import {
  ATLAS_PROTOCOL_VERSION,
  type AtlasControllerIdentity,
  type AtlasControllerOptions,
  type AtlasDotNetReceiver
} from "./protocol";

const controllers = new Map<string, AtlasInteractionController>();

export function attachAtlasController(
  root: HTMLElement,
  identity: AtlasControllerIdentity,
  receiver: AtlasDotNetReceiver,
  options?: AtlasControllerOptions): AtlasInteractionController {
  if (identity.protocolVersion !== ATLAS_PROTOCOL_VERSION) {
    throw new Error("ATLAS-INTERACTION-ATTACH-REJECTED");
  }

  const previous = controllers.get(identity.hostId);
  if (previous !== undefined) {
    // ADR-0005 将注册表记录为“拒绝重复挂载同一工作区”。实现改为替换
    // 先前的控制器，使重绑/重试流程可以确定性地重新挂载；替换会先释放
    // 旧的监听器，因此任一时刻每个 Host 最多只有一个活动控制器。
    disposeAtlasController(previous);
  }

  const controller = new AtlasInteractionController(
    resolveHostElement(root, identity.hostId),
    identity,
    receiver,
    options);
  controllers.set(identity.hostId, controller);
  return controller;
}

/**
 * 解析 Blazor 移交的 Host 根元素。
 *
 * Blazor 的 ElementReference reviver 通过查询元素的 `_bl_<id>` 捕获属性来
 * 解析 `{__internalId: ...}` 包装。在 WebAssembly 中，reviver 可能在当前
 * 渲染批次应用该属性之前运行；此时即使工作区元素即将出现在 DOM 中，也会
 * 得到 `null`。回退到 Host 的稳定身份属性，并在两种查找都找不到元素时
 * 发出重试信号。
 */
function resolveHostElement(root: HTMLElement, hostId: string): HTMLElement {
  if (root instanceof HTMLElement) {
    return root;
  }

  const host = document.querySelector(`[data-atlas-host-id="${hostId}"]`);
  if (host instanceof HTMLElement) {
    return host;
  }

  // 该消息文本是内部挂载诊断，不属于冻结的交互协议：C# Host 通过匹配它
  // 区分瞬时重试与真实挂载失败。修改时请保持两侧同步。
  throw new Error("ATLAS-INTERACTION-ATTACH-RETRY");
}

export function updateAtlasController(
  controller: AtlasInteractionController,
  identity: AtlasControllerIdentity,
  options?: AtlasControllerOptions): void {
  controller.update(identity, options);
}

export function disposeAtlasController(
  controller: AtlasInteractionController): void {
  controller.dispose();
  for (const [hostId, registered] of controllers) {
    if (registered === controller) {
      controllers.delete(hostId);
      break;
    }
  }
}
