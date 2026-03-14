import test from "node:test";
import assert from "node:assert/strict";

import { buildSupabaseReadyProbeHeaders } from "./migration-seed";

test("buildSupabaseReadyProbeHeaders includes the service-role api key for all probes", () => {
  const headers = buildSupabaseReadyProbeHeaders("service-role-secret");

  assert.deepEqual(headers, {
    apikey: "service-role-secret",
    authorization: "Bearer service-role-secret",
    accept: "application/json"
  });
});
