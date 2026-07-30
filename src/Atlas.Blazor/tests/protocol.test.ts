import assert from "node:assert/strict";
import test from "node:test";

import {
  ATLAS_PROTOCOL_VERSION,
  isSupportedProtocolVersion
} from "../ClientScripts/protocol";

test("protocol version is explicit and rejects unknown versions", () => {
  assert.equal(ATLAS_PROTOCOL_VERSION, 1);
  assert.equal(isSupportedProtocolVersion(ATLAS_PROTOCOL_VERSION), true);
  assert.equal(isSupportedProtocolVersion(ATLAS_PROTOCOL_VERSION + 1), false);
});
