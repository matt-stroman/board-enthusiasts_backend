import { beforeEach, describe, expect, it, vi } from "vitest";
import { WorkerAppService } from "./service-boundary";

type MarketingContactRow = {
  id: string;
  email: string;
  normalized_email: string;
  first_name: string | null;
  status: "subscribed";
  lifecycle_status: "waitlisted" | "invited" | "converted";
  consented_at: string;
  consent_text_version: string;
  source: string;
  utm_source: string | null;
  utm_medium: string | null;
  utm_campaign: string | null;
  utm_term: string | null;
  utm_content: string | null;
  brevo_contact_id: string | null;
  brevo_sync_state: "pending" | "synced" | "skipped" | "failed";
  brevo_synced_at: string | null;
  brevo_last_error: string | null;
  converted_app_user_id: string | null;
  created_at: string;
  updated_at: string;
};

type RoleInterestRow = {
  marketing_contact_id: string;
  role: "player" | "developer";
  created_at: string;
};

const tables: {
  marketing_contacts: MarketingContactRow[];
  marketing_contact_role_interests: RoleInterestRow[];
} = {
  marketing_contacts: [],
  marketing_contact_role_interests: [],
};

function resetTables() {
  tables.marketing_contacts = [];
  tables.marketing_contact_role_interests = [];
}

function createQueryBuilder(tableName: keyof typeof tables) {
  let filters: Array<{ column: string; value: unknown }> = [];
  let pendingUpdate: Record<string, unknown> | null = null;

  const applyFilters = <TRow extends Record<string, unknown>>(rows: TRow[]) =>
    rows.filter((row) => filters.every((filter) => row[filter.column] === filter.value));

  const builder = {
    select(_columns?: string) {
      return builder;
    },
    then(onFulfilled: (value: { data: Array<Record<string, unknown>>; error: null }) => unknown, onRejected?: (reason: unknown) => unknown) {
      return Promise.resolve({
        data: applyFilters(tables[tableName] as Array<Record<string, unknown>>),
        error: null,
      }).then(onFulfilled, onRejected);
    },
    limit(count: number) {
      return Promise.resolve({
        data: applyFilters(tables[tableName] as Array<Record<string, unknown>>).slice(0, count),
        error: null,
      });
    },
    upsert(payload: Record<string, unknown>, options?: { onConflict?: string }) {
      const rows = tables[tableName] as Array<Record<string, unknown>>;
      const conflictColumn = options?.onConflict ?? "id";
      const match = rows.find((row) => row[conflictColumn] === payload[conflictColumn]);
      if (match) {
        Object.assign(match, payload);
      } else {
        rows.push({ ...payload } as never);
      }
      return builder;
    },
    single() {
      const rows = applyFilters(tables[tableName] as Array<Record<string, unknown>>);
      return Promise.resolve({
        data: (rows[0] ?? (tables[tableName] as Array<Record<string, unknown>>).slice(-1)[0] ?? null),
        error: null,
      });
    },
    eq(column: string, value: unknown) {
      filters = [...filters, { column, value }];
      if (pendingUpdate) {
        for (const row of applyFilters(tables[tableName] as Array<Record<string, unknown>>)) {
          Object.assign(row, pendingUpdate);
        }

        return Promise.resolve({ error: null });
      }

      return builder;
    },
    insert(payload: Array<Record<string, unknown>> | Record<string, unknown>) {
      const rows = Array.isArray(payload) ? payload : [payload];
      (tables[tableName] as Array<Record<string, unknown>>).push(...rows.map((row) => ({ ...row })));
      return Promise.resolve({ error: null });
    },
    delete() {
      return {
        eq(column: string, value: unknown) {
          const rows = tables[tableName] as Array<Record<string, unknown>>;
          const kept = rows.filter((row) => row[column] !== value);
          rows.splice(0, rows.length, ...kept);
          return Promise.resolve({ error: null });
        },
      };
    },
    update(payload: Record<string, unknown>) {
      pendingUpdate = payload;
      return builder;
    },
  };

  return builder;
}

vi.mock("@supabase/supabase-js", () => ({
  createClient: vi.fn(() => ({
    from(tableName: keyof typeof tables) {
      return createQueryBuilder(tableName);
    },
  })),
}));

describe("WorkerAppService.createMarketingSignup", () => {
  beforeEach(() => {
    resetTables();
    vi.restoreAllMocks();
  });

  it("stores waitlisted lifecycle state, role interests, and Brevo attributes for a new signup", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 42 }),
      });
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      TURNSTILE_SECRET_KEY: "turnstile-secret",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
    });

    const response = await service.createMarketingSignup({
      email: "Taylor@example.com",
      firstName: "Taylor",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "turnstile-token",
      roleInterests: ["player", "developer", "player"],
    });

    expect(response.duplicate).toBe(false);
    expect(response.signup.lifecycleStatus).toBe("waitlisted");
    expect(response.signup.roleInterests).toEqual(["developer", "player"]);
    expect(tables.marketing_contacts[0]).toMatchObject({
      normalized_email: "taylor@example.com",
      lifecycle_status: "waitlisted",
      source: "landing_page",
    });
    expect(tables.marketing_contact_role_interests).toEqual([
      expect.objectContaining({ role: "developer" }),
      expect.objectContaining({ role: "player" }),
    ]);
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      "https://api.brevo.com/v3/contacts",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          email: "Taylor@example.com",
          attributes: {
            FIRSTNAME: "Taylor",
            SOURCE: "landing_page",
            BE_LIFECYCLE_STATUS: "waitlisted",
            BE_ROLE_INTEREST: "developer,player",
          },
          listIds: [12],
          updateEnabled: true,
          emailBlacklisted: false,
        }),
      }),
    );
  });

  it("treats unchecked role interests as none for Brevo and an empty application record", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 99 }),
      });
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      TURNSTILE_SECRET_KEY: "turnstile-secret",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
    });

    const response = await service.createMarketingSignup({
      email: "hello@example.com",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "turnstile-token",
      roleInterests: [],
    });

    expect(response.signup.roleInterests).toEqual([]);
    expect(tables.marketing_contact_role_interests).toEqual([]);
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      "https://api.brevo.com/v3/contacts",
      expect.objectContaining({
        body: expect.stringContaining("\"BE_ROLE_INTEREST\":\"none\""),
      }),
    );
  });
});
