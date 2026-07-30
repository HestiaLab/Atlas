export const ATLAS_PROTOCOL_VERSION = 1 as const;

export interface AtlasControllerIdentity {
  readonly hostId: string;
  readonly protocolVersion: typeof ATLAS_PROTOCOL_VERSION;
}

export function isSupportedProtocolVersion(value: number): value is typeof ATLAS_PROTOCOL_VERSION {
  return value === ATLAS_PROTOCOL_VERSION;
}
