import { beforeEach, describe, expect, it, vi } from "vitest";
import { WorkerAppService } from "./service-boundary";

type AppUserRow = {
  id: string;
  auth_user_id: string;
  user_name: string;
  display_name: string | null;
  first_name: string | null;
  last_name: string | null;
  email: string | null;
  email_verified: boolean;
  identity_provider: string;
  avatar_url: string | null;
  avatar_storage_path: string | null;
  updated_at: string;
};

type AppUserRoleRow = {
  user_id: string;
  role: string;
};

type StudioRow = {
  id: string;
  slug: string;
  display_name: string;
  description: string | null;
  avatar_url: string | null;
  avatar_storage_path: string | null;
  logo_url: string | null;
  logo_storage_path: string | null;
  banner_url: string | null;
  banner_storage_path: string | null;
  created_by_user_id: string;
  created_at: string;
  updated_at: string;
};

type StudioMembershipRow = {
  studio_id: string;
  user_id: string;
  role: "owner" | "admin" | "editor";
  joined_at?: string;
  updated_at?: string;
};

type StudioLinkRow = {
  id: string;
  studio_id: string;
  label: string;
  url: string;
  created_at: string;
  updated_at: string;
};

const tables: {
  app_users: AppUserRow[];
  app_user_roles: AppUserRoleRow[];
  studios: StudioRow[];
  studio_memberships: StudioMembershipRow[];
  studio_links: StudioLinkRow[];
} = {
  app_users: [],
  app_user_roles: [],
  studios: [],
  studio_memberships: [],
  studio_links: [],
};

const storageUploads: Array<{ bucket: string; path: string; contentType: string }> = [];

function resetTables() {
  tables.app_users = [
    {
      id: "user-1",
      auth_user_id: "auth-user-1",
      user_name: "taylor",
      display_name: "Taylor",
      first_name: "Taylor",
      last_name: null,
      email: "taylor@example.com",
      email_verified: true,
      identity_provider: "email",
      avatar_url: null,
      avatar_storage_path: null,
      updated_at: "2026-03-13T00:00:00Z",
    },
  ];
  tables.app_user_roles = [
    { user_id: "user-1", role: "player" },
    { user_id: "user-1", role: "developer" },
  ];
  tables.studios = [
    {
      id: "studio-1",
      slug: "blue-harbor-games",
      display_name: "Blue Harbor Games",
      description: "Blue Harbor Games profile.",
      avatar_url: null,
      avatar_storage_path: null,
      logo_url: "https://cdn.example/logo.png",
      logo_storage_path: "studios/blue-harbor-games/logo.png",
      banner_url: null,
      banner_storage_path: null,
      created_by_user_id: "user-1",
      created_at: "2026-03-13T00:00:00Z",
      updated_at: "2026-03-13T00:00:00Z",
    },
  ];
  tables.studio_memberships = [{ studio_id: "studio-1", user_id: "user-1", role: "owner" }];
  tables.studio_links = [];
  storageUploads.splice(0, storageUploads.length);
}

function createQueryBuilder(tableName: keyof typeof tables) {
  let filters: Array<{ column: string; value: unknown }> = [];
  let inFilter: { column: string; values: unknown[] } | null = null;
  let pendingUpdate: Record<string, unknown> | null = null;
  let pendingSelect: string | null = null;

  const applyFilters = <TRow extends Record<string, unknown>>(rows: TRow[]) =>
    rows.filter((row) => {
      const matchesEq = filters.every((filter) => row[filter.column] === filter.value);
      const matchesIn = inFilter ? inFilter.values.includes(row[inFilter.column]) : true;
      return matchesEq && matchesIn;
    });

  const builder = {
    select(columns?: string) {
      pendingSelect = columns ?? null;
      return builder;
    },
    then(onFulfilled: (value: { data: Array<Record<string, unknown>>; error: null }) => unknown, onRejected?: (reason: unknown) => unknown) {
      return Promise.resolve({
        data: applyFilters(tables[tableName] as Array<Record<string, unknown>>),
        error: null,
      }).then(onFulfilled, onRejected);
    },
    in(column: string, values: unknown[]) {
      inFilter = { column, values };
      return Promise.resolve({
        data: applyFilters(tables[tableName] as Array<Record<string, unknown>>),
        error: null,
      });
    },
    limit(count: number) {
      return Promise.resolve({
        data: applyFilters(tables[tableName] as Array<Record<string, unknown>>).slice(0, count),
        error: null,
      });
    },
    single() {
      const rows = applyFilters(tables[tableName] as Array<Record<string, unknown>>);
      return Promise.resolve({
        data: rows[0] ?? null,
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
      const destination = tables[tableName] as Array<Record<string, unknown>>;
      const inserted = rows.map((row, index) => {
        const copy = { ...row };
        if (!copy.id) {
          copy.id = `${tableName.slice(0, -1)}-${destination.length + index + 1}`;
        }
        return copy;
      });
      destination.push(...inserted);

      return {
        select() {
          return {
            single: async () => ({ data: inserted[0] ?? null, error: null }),
          };
        },
        then(onFulfilled: (value: { error: null }) => unknown, onRejected?: (reason: unknown) => unknown) {
          return Promise.resolve({ error: null }).then(onFulfilled, onRejected);
        },
      };
    },
    update(payload: Record<string, unknown>) {
      pendingUpdate = payload;
      return builder;
    },
    delete() {
      return {
        eq(column: string, value: unknown) {
          const kept = (tables[tableName] as Array<Record<string, unknown>>).filter((row) => row[column] !== value);
          tables[tableName].splice(0, tables[tableName].length, ...(kept as never[]));
          return Promise.resolve({ error: null });
        },
      };
    },
  };

  return builder;
}

const authGetUser = vi.fn(async () => ({
  data: {
    user: {
      id: "auth-user-1",
      email: "taylor@example.com",
      email_confirmed_at: "2026-03-13T00:00:00Z",
      user_metadata: { displayName: "Taylor" },
      app_metadata: { provider: "email" },
      identities: [{ provider: "email" }],
    },
  },
  error: null,
}));

vi.mock("@supabase/supabase-js", () => ({
  createClient: vi.fn(() => ({
    auth: {
      getUser: authGetUser,
    },
    from(tableName: keyof typeof tables) {
      return createQueryBuilder(tableName);
    },
    storage: {
      from(bucket: string) {
        return {
          async upload(path: string, file: File, options: { contentType: string }) {
            storageUploads.push({ bucket, path, contentType: options.contentType });
            return { error: null };
          },
          getPublicUrl(path: string) {
            return {
              data: {
                publicUrl: `https://storage.example/${bucket}/${path}`,
              },
            };
          },
        };
      },
    },
  })),
}));

describe("WorkerAppService studio avatar support", () => {
  beforeEach(() => {
    resetTables();
    vi.clearAllMocks();
  });

  it("persists avatar URLs when creating and updating studios", async () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      SUPABASE_MEDIA_BUCKET: "catalog-media",
    });

    const created = await service.createStudio("developer-token", {
      slug: "signal-harbor-studio",
      displayName: "Signal Harbor Studio",
      description: "A coastal co-op studio profile.",
      avatarUrl: "https://example.com/avatar.png",
      logoUrl: null,
      bannerUrl: null,
    });

    expect(created.studio.avatarUrl).toBe("https://example.com/avatar.png");

    const updated = await service.updateStudio("developer-token", "studio-1", {
      slug: "blue-harbor-games",
      displayName: "Blue Harbor Games",
      description: "Blue Harbor Games profile.",
      avatarUrl: "https://example.com/updated-avatar.png",
      logoUrl: "https://example.com/logo.png",
      bannerUrl: null,
    });

    expect(updated.studio.avatarUrl).toBe("https://example.com/updated-avatar.png");
    expect(tables.studios.find((studio) => studio.id === "studio-1")?.avatar_url).toBe("https://example.com/updated-avatar.png");
  });

  it("uploads studio avatar files into storage and updates the studio projection", async () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      SUPABASE_MEDIA_BUCKET: "catalog-media",
    });

    const response = await service.uploadStudioMedia(
      "developer-token",
      "studio-1",
      "avatar",
      new File(["avatar-bytes"], "studio-avatar.png", { type: "image/png" }),
    );

    expect(storageUploads).toEqual([
      expect.objectContaining({
        bucket: "catalog-media",
        path: "studios/blue-harbor-games/avatar.png",
        contentType: "image/png",
      }),
    ]);
    expect(response.studio.avatarUrl).toBe("https://storage.example/catalog-media/studios/blue-harbor-games/avatar.png");
    expect(tables.studios.find((studio) => studio.id === "studio-1")?.avatar_storage_path).toBe("studios/blue-harbor-games/avatar.png");
  });
});
