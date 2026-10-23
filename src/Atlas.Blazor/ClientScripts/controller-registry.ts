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
 * Resolves the Host root element handed over by Blazor.
 *
 * Blazor's ElementReference reviver resolves `{__internalId: ...}` wrappers by
 * querying the element's `_bl_<id>` capture attribute. In WebAssembly the
 * reviver can run before the current render batch has applied that attribute,
 * which produces `null` even though the workspace element is about to appear
 * in the DOM. Fall back to the Host's stable identity attribute, and signal a
 * retry when neither lookup can find the element yet.
 */
function resolveHostElement(root: HTMLElement, hostId: string): HTMLElement {
  if (root instanceof HTMLElement) {
    return root;
  }

  const host = document.querySelector(`[data-atlas-host-id="${hostId}"]`);
  if (host instanceof HTMLElement) {
    return host;
  }

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
