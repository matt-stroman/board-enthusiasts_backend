import { readFile } from "node:fs/promises";
import path from "node:path";

import { createClient, type SupabaseClient } from "@supabase/supabase-js";
import {
  migrationMediaBuckets,
  migrationMediaUploadPolicies,
  migrationSeedStudios,
  migrationSeedTitles,
  migrationSeedUsers
} from "../../packages/migration-contract/src/index";

interface SeedOptions {
  supabaseUrl: string;
  secretKey: string;
  password: string;
  assetRoot: string;
  avatarsBucket: string;
  cardImagesBucket: string;
  heroImagesBucket: string;
  logoImagesBucket: string;
}

interface ParsedArgs {
  [key: string]: string | undefined;
}

interface AuthUserRecord {
  id: string;
  email: string;
}

function formatErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  if (typeof error === "string") {
    return error;
  }

  try {
    return JSON.stringify(error, null, 2);
  } catch {
    return String(error);
  }
}

function parseArgs(argv: string[]): ParsedArgs {
  const parsed: ParsedArgs = {};

  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith("--")) {
      continue;
    }

    const key = token.slice(2);
    const nextValue = argv[index + 1];
    if (!nextValue || nextValue.startsWith("--")) {
      parsed[key] = "true";
      continue;
    }

    parsed[key] = nextValue;
    index += 1;
  }

  return parsed;
}

function requireArg(args: ParsedArgs, name: string): string {
  const value = (args[name] ?? "").trim();
  if (!value) {
    throw new Error(`Missing required argument --${name}`);
  }

  return value;
}

function buildOptions(argv: string[]): SeedOptions {
  const args = parseArgs(argv);

  return {
    supabaseUrl: requireArg(args, "supabase-url"),
    secretKey: requireArg(args, "secret-key"),
    password: requireArg(args, "password"),
    assetRoot: requireArg(args, "asset-root"),
    avatarsBucket: (args["avatars-bucket"] ?? migrationMediaBuckets.avatars).trim() || migrationMediaBuckets.avatars,
    cardImagesBucket: (args["card-images-bucket"] ?? migrationMediaBuckets.cardImages).trim() || migrationMediaBuckets.cardImages,
    heroImagesBucket: (args["hero-images-bucket"] ?? migrationMediaBuckets.heroImages).trim() || migrationMediaBuckets.heroImages,
    logoImagesBucket: (args["logo-images-bucket"] ?? migrationMediaBuckets.logoImages).trim() || migrationMediaBuckets.logoImages
  };
}

function isTransientSupabaseError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  const normalized = message.toLowerCase();
  return (
    normalized.includes("invalid response") ||
    normalized.includes("fetch failed") ||
    normalized.includes("gateway") ||
    normalized.includes("econnrefused") ||
    normalized.includes("timed out") ||
    normalized.includes("502") ||
    normalized.includes("503")
  );
}

async function delay(milliseconds: number): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function deriveInitials(displayName: string): string {
  return displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((segment) => segment[0]!.toUpperCase())
    .join("");
}

function buildPlayerCountDisplay(minPlayers: number, maxPlayers: number): string {
  return minPlayers === maxPlayers ? `${minPlayers} player${minPlayers === 1 ? "" : "s"}` : `${minPlayers}-${maxPlayers} players`;
}

function buildAgeDisplay(authority: string, value: string): string {
  return `${authority} ${value}`;
}

function parseMimeType(filePath: string, fallback: string): string {
  const extension = path.extname(filePath).toLowerCase();
  if (extension === ".svg") {
    return "image/svg+xml";
  }
  if (extension === ".png") {
    return "image/png";
  }
  if (extension === ".jpg" || extension === ".jpeg") {
    return "image/jpeg";
  }
  if (extension === ".webp") {
    return "image/webp";
  }

  return fallback;
}

function formatBucketFileSizeLimit(maxUploadBytes: number): string {
  if (maxUploadBytes % (1024 * 1024) === 0) {
    return `${maxUploadBytes / (1024 * 1024)}MB`;
  }
  return `${Math.round(maxUploadBytes / 1024)}KB`;
}

async function ensureBucket(
  client: SupabaseClient,
  bucket: string,
  options: { maxUploadBytes: number; acceptedMimeTypes: readonly string[] }
): Promise<void> {
  const { data, error } = await client.storage.listBuckets();
  if (error) {
    throw error;
  }

  if (data.some((candidate) => candidate.name === bucket)) {
    await client.storage.updateBucket(bucket, {
      public: true,
      fileSizeLimit: formatBucketFileSizeLimit(options.maxUploadBytes),
      allowedMimeTypes: [...options.acceptedMimeTypes]
    });
    return;
  }

  const { error: createError } = await client.storage.createBucket(bucket, {
    public: true,
    fileSizeLimit: formatBucketFileSizeLimit(options.maxUploadBytes),
    allowedMimeTypes: [...options.acceptedMimeTypes]
  });
  if (createError) {
    throw createError;
  }
}

async function waitForSupabaseReady(client: SupabaseClient): Promise<void> {
  const timeoutAt = Date.now() + 180_000;
  let lastError = "Supabase HTTP services are still starting.";
  const supabaseUrl = client.supabaseUrl.replace(/\/$/, "");
  const serviceRoleKey = client.supabaseKey;

  async function probe(url: string, headers: Record<string, string> = {}): Promise<string | null> {
    try {
      const response = await fetch(url, {
        method: "GET",
        headers,
        signal: AbortSignal.timeout(10_000)
      });
      if (response.ok) {
        return null;
      }

      const detail = (await response.text()).trim();
      return `HTTP ${response.status}${detail ? `: ${detail}` : ""}`;
    } catch (error: unknown) {
      return error instanceof Error ? error.message : String(error);
    }
  }

  while (Date.now() < timeoutAt) {
    const sharedHeaders = {
      apikey: serviceRoleKey,
      authorization: `Bearer ${serviceRoleKey}`,
      accept: "application/json"
    };
    const [authError, restError, storageError] = await Promise.all([
      probe(`${supabaseUrl}/auth/v1/health`),
      probe(`${supabaseUrl}/rest/v1/migration_wave_state?select=key&limit=1`, sharedHeaders),
      probe(`${supabaseUrl}/storage/v1/bucket`, sharedHeaders)
    ]);

    if (!authError && !restError && !storageError) {
      return;
    }

    const details = [
      authError ? `auth: ${authError}` : null,
      restError ? `rest: ${restError}` : null,
      storageError ? `storage: ${storageError}` : null
    ].filter(Boolean);
    if (details.length > 0) {
      lastError = details.join(" | ");
    }

    await delay(2000);
  }

  throw new Error(`Timed out waiting for Supabase APIs to become ready. Last error: ${lastError}`);
}

async function uploadAsset(
  client: SupabaseClient,
  bucket: string,
  assetRoot: string,
  relativePath: string,
  storagePath: string,
  fallbackMimeType: string
): Promise<{ storagePath: string; publicUrl: string; mimeType: string }> {
  const fullPath = path.join(assetRoot, relativePath);
  const content = await readFile(fullPath);
  const mimeType = parseMimeType(relativePath, fallbackMimeType);
  const { error } = await client.storage.from(bucket).upload(storagePath, content, {
    contentType: mimeType,
    upsert: true
  });
  if (error) {
    throw error;
  }

  const { data } = client.storage.from(bucket).getPublicUrl(storagePath);
  return {
    storagePath,
    publicUrl: data.publicUrl,
    mimeType
  };
}

function getTitleMediaBucket(options: SeedOptions, mediaRole: "card" | "hero" | "logo"): string {
  switch (mediaRole) {
    case "card":
      return options.cardImagesBucket;
    case "hero":
      return options.heroImagesBucket;
    case "logo":
      return options.logoImagesBucket;
  }
}

function bucketSupportsMimeType(bucketPolicy: { acceptedMimeTypes: readonly string[] }, mimeType: string): boolean {
  return bucketPolicy.acceptedMimeTypes.includes(mimeType);
}

async function ensureAuthUsers(client: SupabaseClient, password: string): Promise<Map<string, AuthUserRecord>> {
  const { data, error } = await client.auth.admin.listUsers();
  if (error) {
    throw error;
  }

  const usersByEmail = new Map(
    data.users
      .filter((candidate) => candidate.email)
      .map((candidate) => [candidate.email!.toLowerCase(), { id: candidate.id, email: candidate.email! } satisfies AuthUserRecord])
  );

  const records = new Map<string, AuthUserRecord>();
  for (const fixture of migrationSeedUsers) {
    const existing = usersByEmail.get(fixture.email.toLowerCase());
    if (existing) {
      const { error: updateError } = await client.auth.admin.updateUserById(existing.id, {
        password,
        email_confirm: true,
        user_metadata: {
          userName: fixture.userName,
          displayName: fixture.displayName
        }
      });
      if (updateError) {
        throw updateError;
      }

      records.set(fixture.userName, existing);
      continue;
    }

    const { data: created, error: createError } = await client.auth.admin.createUser({
      email: fixture.email,
      password,
      email_confirm: true,
      user_metadata: {
        userName: fixture.userName,
        displayName: fixture.displayName
      }
    });
    if (createError || !created.user?.id || !created.user.email) {
      throw createError ?? new Error(`Failed to create auth user for ${fixture.userName}`);
    }

    records.set(fixture.userName, { id: created.user.id, email: created.user.email });
  }

  return records;
}

async function resetDemoData(client: SupabaseClient): Promise<void> {
  const { error } = await client.rpc("reset_migration_demo_data");
  if (error) {
    throw error;
  }
}

async function seedOnce(options: SeedOptions): Promise<void> {
  const client = createClient(options.supabaseUrl, options.secretKey, {
    auth: {
      autoRefreshToken: false,
      persistSession: false
    }
  });

  console.log("==> Waiting for Supabase APIs to become ready");
  await waitForSupabaseReady(client);

  console.log("==> Ensuring Supabase media buckets exist");
  await ensureBucket(client, options.avatarsBucket, migrationMediaUploadPolicies.avatars);
  await ensureBucket(client, options.cardImagesBucket, migrationMediaUploadPolicies.cardImages);
  await ensureBucket(client, options.heroImagesBucket, migrationMediaUploadPolicies.heroImages);
  await ensureBucket(client, options.logoImagesBucket, migrationMediaUploadPolicies.logoImages);

  console.log("==> Creating or updating deterministic Supabase auth users");
  const authUsers = await ensureAuthUsers(client, options.password);

  console.log("==> Resetting local demo tables");
  await resetDemoData(client);

  console.log("==> Seeding application users and role projections");
  const appUserRows = migrationSeedUsers.map((fixture) => {
    const authRecord = authUsers.get(fixture.userName);
    if (!authRecord) {
      throw new Error(`Missing auth user for fixture ${fixture.userName}`);
    }

    return {
      auth_user_id: authRecord.id,
      user_name: fixture.userName,
      display_name: fixture.displayName,
      first_name: fixture.firstName,
      last_name: fixture.lastName,
      email: fixture.email,
      email_verified: true,
      identity_provider: "email",
      avatar_url: `https://api.dicebear.com/9.x/initials/svg?seed=${encodeURIComponent(fixture.displayName)}`,
      avatar_storage_path: null
    };
  });

  const { data: insertedUsers, error: userInsertError } = await client
    .from("app_users")
    .insert(appUserRows)
    .select("id, auth_user_id, user_name");
  if (userInsertError) {
    throw userInsertError;
  }

  const appUsersByUserName = new Map(insertedUsers.map((row) => [row.user_name as string, row.id as string]));
  const appUsersByAuthId = new Map(insertedUsers.map((row) => [row.auth_user_id as string, row.id as string]));

  const roleRows = migrationSeedUsers.flatMap((fixture) => {
    const appUserId = appUsersByUserName.get(fixture.userName);
    if (!appUserId) {
      throw new Error(`Missing app user id for ${fixture.userName}`);
    }

    return fixture.roles.map((role) => ({
      user_id: appUserId,
      role
    }));
  });
  const { error: roleInsertError } = await client.from("app_user_roles").insert(roleRows);
  if (roleInsertError) {
    throw roleInsertError;
  }

  const boardProfileRows = migrationSeedUsers
    .filter((fixture) => fixture.boardUserId)
    .map((fixture) => ({
      user_id: appUsersByUserName.get(fixture.userName),
      board_user_id: fixture.boardUserId,
      display_name: fixture.displayName,
      avatar_url: fixture.boardAvatarUrl ?? null
    }));
  if (boardProfileRows.length > 0) {
    const { error: boardProfileError } = await client.from("user_board_profiles").insert(boardProfileRows);
    if (boardProfileError) {
      throw boardProfileError;
    }
  }

  console.log("==> Uploading studio media and seeding studios");
  const studioRows: Array<Record<string, unknown>> = [];
  for (const studio of migrationSeedStudios) {
    const ownerUserId = appUsersByUserName.get(studio.ownerUserName);
    if (!ownerUserId) {
      throw new Error(`Missing owner user for studio ${studio.slug}`);
    }

    const uploadedLogo = await uploadAsset(
      client,
      options.logoImagesBucket,
      options.assetRoot,
      studio.logoAssetPath,
      `studios/${studio.slug}/logo${path.extname(studio.logoAssetPath).toLowerCase()}`,
      "image/svg+xml"
    );
    const uploadedBanner = await uploadAsset(
      client,
      options.heroImagesBucket,
      options.assetRoot,
      studio.bannerAssetPath,
      `studios/${studio.slug}/banner${path.extname(studio.bannerAssetPath).toLowerCase()}`,
      "image/svg+xml"
    );
    const studioAvatarMimeType = parseMimeType(studio.avatarAssetPath, "image/png");
    const studioAvatarStoragePath = `studios/${studio.slug}/avatar${path.extname(studio.avatarAssetPath).toLowerCase()}`;
    const uploadedAvatar = bucketSupportsMimeType(migrationMediaUploadPolicies.avatars, studioAvatarMimeType)
      ? await uploadAsset(
          client,
          options.avatarsBucket,
          options.assetRoot,
          studio.avatarAssetPath,
          studioAvatarStoragePath,
          studioAvatarMimeType
        )
      : null;

    studioRows.push({
      slug: studio.slug,
      display_name: studio.displayName,
      description: studio.description,
      // Seed catalog studio avatars fall back to the uploaded logo when the source art is
      // vector-only and the avatar bucket policy intentionally rejects SVG uploads.
      avatar_url: uploadedAvatar?.publicUrl ?? uploadedLogo.publicUrl,
      avatar_storage_path: uploadedAvatar?.storagePath ?? null,
      logo_url: uploadedLogo.publicUrl,
      logo_storage_path: uploadedLogo.storagePath,
      banner_url: uploadedBanner.publicUrl,
      banner_storage_path: uploadedBanner.storagePath,
      created_by_user_id: ownerUserId
    });
  }

  const { data: insertedStudios, error: studioInsertError } = await client
    .from("studios")
    .insert(studioRows)
    .select("id, slug");
  if (studioInsertError) {
    throw studioInsertError;
  }

  const studiosBySlug = new Map(insertedStudios.map((row) => [row.slug as string, row.id as string]));

  const membershipRows = migrationSeedStudios.map((studio) => ({
    studio_id: studiosBySlug.get(studio.slug),
    user_id: appUsersByUserName.get(studio.ownerUserName),
    role: "owner"
  }));
  const { error: membershipError } = await client.from("studio_memberships").insert(membershipRows);
  if (membershipError) {
    throw membershipError;
  }

  const linkRows = migrationSeedStudios.flatMap((studio) =>
    studio.links.map((link) => ({
      studio_id: studiosBySlug.get(studio.slug),
      label: link.label,
      url: link.url
    }))
  );
  if (linkRows.length > 0) {
    const { error: linkInsertError } = await client.from("studio_links").insert(linkRows);
    if (linkInsertError) {
      throw linkInsertError;
    }
  }

  console.log("==> Uploading title media and seeding public catalog rows");
  const titleRows: Array<Record<string, unknown>> = [];
  const mediaRows: Array<Record<string, unknown>> = [];
  const metadataVersionRows: Array<Record<string, unknown>> = [];
  const metadataVersionGenreRows: Array<Record<string, unknown>> = [];
  const releaseRows: Array<Record<string, unknown>> = [];
  const titleIdsBySlug = new Map<string, string>();
  for (const title of migrationSeedTitles) {
    const studioId = studiosBySlug.get(title.studioSlug);
    if (!studioId) {
      throw new Error(`Missing studio ${title.studioSlug} for title ${title.slug}`);
    }

    const titleId = crypto.randomUUID();
    titleIdsBySlug.set(title.slug, titleId);
    const currentReleaseId = title.currentReleaseVersion ? crypto.randomUUID() : null;
    for (const media of title.media) {
      const uploaded = await uploadAsset(
        client,
        getTitleMediaBucket(options, media.role),
        options.assetRoot,
        media.assetPath,
        `titles/${title.studioSlug}/${title.slug}/${media.role}${path.extname(media.assetPath).toLowerCase()}`,
        media.mimeType
      );
      mediaRows.push({
        title_id: titleId,
        media_role: media.role,
        source_url: uploaded.publicUrl,
        storage_path: uploaded.storagePath,
        alt_text: media.altText,
        mime_type: uploaded.mimeType,
        width: media.width,
        height: media.height
      });
    }

    for (let revisionNumber = 1; revisionNumber <= title.currentMetadataRevision; revisionNumber += 1) {
      const isCurrent = revisionNumber === title.currentMetadataRevision;
      metadataVersionRows.push({
        title_id: titleId,
        revision_number: revisionNumber,
        is_current: isCurrent,
        is_frozen: Boolean(title.currentReleaseVersion) && isCurrent,
        display_name: revisionNumber === 1 && title.currentMetadataRevision > 1 ? `${title.displayName} Prototype` : title.displayName,
        short_description:
          revisionNumber === 1 && title.currentMetadataRevision > 1
            ? `${title.shortDescription} Prototype notes.`
            : title.shortDescription,
        description:
          revisionNumber === 1 && title.currentMetadataRevision > 1
            ? `${title.description}\n\nPrototype revision preserved for parity testing.`
            : title.description,
        genre_display: title.genreDisplay,
        min_players: title.minPlayers,
        max_players: title.maxPlayers,
        age_rating_authority: title.ageRatingAuthority,
        age_rating_value: title.ageRatingValue,
        min_age_years: title.minAgeYears
      });
      metadataVersionGenreRows.push(
        ...title.genreSlugs.map((genreSlug, index) => ({
          title_id: titleId,
          revision_number: revisionNumber,
          genre_slug: genreSlug,
          display_order: index
        }))
      );
    }

    if (title.currentReleaseVersion && currentReleaseId) {
      releaseRows.push({
        id: currentReleaseId,
        title_id: titleId,
        version: title.currentReleaseVersion,
        status: "published",
        metadata_revision_number: title.currentMetadataRevision,
        acquisition_url: title.acquisition?.url ?? null,
        is_current: true,
        published_at: title.currentReleasePublishedAt ?? null
      });
    }

    if (title.slug === "compass-echo") {
      const draftReleaseId = crypto.randomUUID();
      releaseRows.push({
        id: draftReleaseId,
        title_id: titleId,
        version: "1.0.0-rc1",
        status: "draft",
        metadata_revision_number: title.currentMetadataRevision,
        acquisition_url: "https://blue-harbor-games.example/titles/compass-echo/rc1",
        is_current: false,
        published_at: null
      });
    }

    titleRows.push({
      id: titleId,
      studio_id: studioId,
      slug: title.slug,
      content_kind: title.contentKind,
      lifecycle_status: title.lifecycleStatus,
      visibility: title.visibility,
      is_reported: title.isReported,
      current_metadata_revision: title.currentMetadataRevision,
      display_name: title.displayName,
      short_description: title.shortDescription,
      description: title.description,
      genre_display: title.genreDisplay,
      min_players: title.minPlayers,
      max_players: title.maxPlayers,
      age_rating_authority: title.ageRatingAuthority,
      age_rating_value: title.ageRatingValue,
      min_age_years: title.minAgeYears,
      current_release_id: currentReleaseId,
      current_release_version: title.currentReleaseVersion ?? null,
      current_release_published_at: title.currentReleasePublishedAt ?? null,
      acquisition_url: title.acquisition?.url ?? null
    });
  }

  const { error: titleInsertError } = await client.from("titles").insert(titleRows);
  if (titleInsertError) {
    throw titleInsertError;
  }
  const { error: metadataVersionInsertError } = await client.from("title_metadata_versions").insert(metadataVersionRows);
  if (metadataVersionInsertError) {
    throw metadataVersionInsertError;
  }
  const { error: metadataVersionGenreInsertError } = await client.from("title_metadata_version_genres").insert(metadataVersionGenreRows);
  if (metadataVersionGenreInsertError) {
    throw metadataVersionGenreInsertError;
  }
  const { error: releaseInsertError } = await client.from("title_releases").insert(releaseRows);
  if (releaseInsertError) {
    throw releaseInsertError;
  }
  const { error: mediaInsertError } = await client.from("title_media_assets").insert(mediaRows);
  if (mediaInsertError) {
    throw mediaInsertError;
  }
  const avaUserId = appUsersByUserName.get("ava.garcia");
  const alexUserId = appUsersByUserName.get("alex.rivera");
  const emmaUserId = appUsersByUserName.get("emma.torres");
  const lanternDriftTitleId = titleIdsBySlug.get("lantern-drift");
  const compassEchoTitleId = titleIdsBySlug.get("compass-echo");
  const orbitOrchardTitleId = titleIdsBySlug.get("orbit-orchard");

  console.log("==> Seeding player collections and title reports");
  if (avaUserId && lanternDriftTitleId) {
    const { error: libraryError } = await client.from("player_library_titles").insert({
      user_id: avaUserId,
      title_id: lanternDriftTitleId
    });
    if (libraryError) {
      throw libraryError;
    }
  }

  if (avaUserId && compassEchoTitleId) {
    const { error: wishlistError } = await client.from("player_wishlist_titles").insert({
      user_id: avaUserId,
      title_id: compassEchoTitleId
    });
    if (wishlistError) {
      throw wishlistError;
    }
  }

  if (avaUserId && orbitOrchardTitleId && alexUserId && emmaUserId) {
    const reportId = crypto.randomUUID();
    const createdAt = "2026-03-08T10:15:00Z";
    const updatedAt = "2026-03-08T11:00:00Z";

    const { error: reportError } = await client.from("title_reports").insert({
      id: reportId,
      title_id: orbitOrchardTitleId,
      reporter_user_id: avaUserId,
      status: "needs_player_response",
      reason: "The current title listing still shows placeholder acquisition details and missing release notes.",
      resolution_note: null,
      resolved_by_user_id: null,
      resolved_at: null,
      created_at: createdAt,
      updated_at: updatedAt
    });
    if (reportError) {
      throw reportError;
    }

    const { error: reportMessageError } = await client.from("title_report_messages").insert([
      {
        report_id: reportId,
        author_user_id: avaUserId,
        author_role: "player",
        audience: "all",
        message: "The title page says the release is ready, but the acquisition page still points at placeholder information.",
        created_at: createdAt
      },
      {
        report_id: reportId,
        author_user_id: alexUserId,
        author_role: "moderator",
        audience: "player",
        message: "Thanks. Can you confirm whether this happens on the current published listing and not just the testing build?",
        created_at: updatedAt
      }
    ]);
    if (reportMessageError) {
      throw reportMessageError;
    }

    const { error: notificationError } = await client.from("user_notifications").insert([
      {
        user_id: alexUserId,
        category: "title_report",
        title: "New title report submitted",
        body: "Ava Garcia reported Orbit Orchard. The current title listing still shows placeholder acquisition details and missing release notes.",
        action_url: `/moderate?workflow=reports-review&reportId=${reportId}`,
        is_read: false,
        read_at: null,
        created_at: createdAt,
        updated_at: createdAt
      },
      {
        user_id: emmaUserId,
        category: "title_report",
        title: "Moderator follow-up for your title",
        body: "Alex Rivera sent an update about Orbit Orchard. Review the report thread and respond from Develop.",
        action_url: `/develop?domain=titles&workflow=titles-reports&titleId=${orbitOrchardTitleId}&reportId=${reportId}`,
        is_read: false,
        read_at: null,
        created_at: updatedAt,
        updated_at: updatedAt
      },
      {
        user_id: avaUserId,
        category: "title_report",
        title: "Moderator follow-up on your report",
        body: "Alex Rivera asked for more detail about Orbit Orchard. Open the report thread in Play.",
        action_url: `/player?workflow=reported-titles&reportId=${reportId}`,
        is_read: false,
        read_at: null,
        created_at: updatedAt,
        updated_at: updatedAt
      }
    ]);
    if (notificationError) {
      throw notificationError;
    }
  }

  if (avaUserId && compassEchoTitleId && alexUserId && emmaUserId) {
    const reportId = crypto.randomUUID();
    const createdAt = "2026-03-08T09:10:00Z";
    const updatedAt = "2026-03-08T10:05:00Z";

    const { error: reportError } = await client.from("title_reports").insert({
      id: reportId,
      title_id: compassEchoTitleId,
      reporter_user_id: avaUserId,
      status: "needs_developer_response",
      reason: "The install guidance references a companion feature that is not visible in the current testing build.",
      resolution_note: null,
      resolved_by_user_id: null,
      resolved_at: null,
      created_at: createdAt,
      updated_at: updatedAt
    });
    if (reportError) {
      throw reportError;
    }

    const { error: reportMessageError } = await client.from("title_report_messages").insert([
      {
        report_id: reportId,
        author_user_id: avaUserId,
        author_role: "player",
        audience: "all",
        message: "I expected to see the synced clue board mentioned in the listing, but I cannot find it.",
        created_at: createdAt
      },
      {
        report_id: reportId,
        author_user_id: alexUserId,
        author_role: "moderator",
        audience: "developer",
        message: "Please confirm whether this feature is intentionally hidden in testing or if the listing needs an update.",
        created_at: updatedAt
      }
    ]);
    if (reportMessageError) {
      throw reportMessageError;
    }

    const { error: notificationError } = await client.from("user_notifications").insert([
      {
        user_id: alexUserId,
        category: "title_report",
        title: "New title report submitted",
        body: "Ava Garcia reported Compass Echo. The install guidance references a companion feature that is not visible in the current testing build.",
        action_url: `/moderate?workflow=reports-review&reportId=${reportId}`,
        is_read: false,
        read_at: null,
        created_at: createdAt,
        updated_at: createdAt
      },
      {
        user_id: emmaUserId,
        category: "title_report",
        title: "Moderator follow-up for your title",
        body: "Alex Rivera sent an update about Compass Echo. Open the developer report thread for the latest moderator note.",
        action_url: `/develop?domain=titles&workflow=titles-reports&titleId=${compassEchoTitleId}&reportId=${reportId}`,
        is_read: false,
        read_at: null,
        created_at: updatedAt,
        updated_at: updatedAt
      }
    ]);
    if (notificationError) {
      throw notificationError;
    }
  }

  console.log("==> Migration seed complete");
  console.log(`Seeded users use password: ${options.password}`);
  console.log(`Moderator account: alex.rivera (${migrationSeedUsers[0]?.email ?? ""})`);
  console.log(`Developer account: emma.torres (${migrationSeedUsers[1]?.email ?? ""})`);
  console.log(`Avatars bucket: ${options.avatarsBucket}`);
  console.log(`Card images bucket: ${options.cardImagesBucket}`);
  console.log(`Hero images bucket: ${options.heroImagesBucket}`);
  console.log(`Logo images bucket: ${options.logoImagesBucket}`);
  console.log(`Seeded application users: ${appUsersByAuthId.size}`);
}

async function seed(): Promise<void> {
  const options = buildOptions(process.argv.slice(2));
  const maxAttempts = 10;

  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    try {
      await seedOnce(options);
      return;
    } catch (error: unknown) {
      if (attempt >= maxAttempts || !isTransientSupabaseError(error)) {
        throw error;
      }

      const message = error instanceof Error ? error.message : String(error);
      console.warn(`Transient Supabase startup error during local seed (attempt ${attempt}/${maxAttempts}): ${message}`);
      await delay(2000);
    }
  }
}

seed().catch((error: unknown) => {
  const message = formatErrorMessage(error);
  console.error(message);
  process.exitCode = 1;
});
