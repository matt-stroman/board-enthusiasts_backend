import { createClient, type SupabaseClient, type User as SupabaseAuthUser } from "@supabase/supabase-js";
import {
  type AddModerationTitleReportMessageRequest,
  type AddTitleReportMessageRequest,
  type MarketingContactLifecycleStatus,
  type MarketingContactRoleInterest,
  type MarketingContactStatus,
  type MarketingSignupRequest,
  type MarketingSignupResponse,
  type BoardProfileResponse,
  type CreateDeveloperTitleRequest,
  migrationMediaBucket,
  type AgeRatingAuthorityDefinition,
  type AgeRatingAuthorityListResponse,
  type CatalogPaging,
  type CatalogTitle,
  type CatalogTitleListQuery,
  type CatalogTitleListResponse,
  type CatalogTitleResponse,
  type CatalogTitleSummary,
  type CreatePlayerTitleReportRequest,
  type CurrentUserResponse,
  type DeveloperTitle,
  type DeveloperEnrollmentResponse,
  type DeveloperTitleListResponse,
  type DeveloperTitleResponse,
  type DeveloperStudioListResponse,
  type GenreDefinition,
  type GenreListResponse,
  type ModerateTitleReportDecisionRequest,
  type ModerationDeveloperListResponse,
  normalizeGenreSlug,
  type PlayerCollectionMutationResponse,
  type PlayerTitleListResponse,
  type PlayerTitleReportListResponse,
  type PlayerTitleReportResponse,
  type PlatformRole,
  type PublicTitleAcquisition,
  type Studio,
  type StudioLink,
  type StudioLinkListResponse,
  type StudioLinkResponse,
  type StudioListResponse,
  type StudioMembershipRole,
  type StudioResponse,
  type UpdateUserProfileRequest,
  type UserNameAvailabilityResponse,
  type UserNotification,
  type UserNotificationListResponse,
  type UserNotificationResponse,
  type TitleReportActor,
  type TitleReportDetail,
  type TitleReportDetailResponse,
  type TitleReportListResponse,
  type TitleReportMessage,
  type TitleReportSummary,
  type TitleLifecycleStatus,
  type TitleMediaAsset,
  type TitleMediaAssetListResponse,
  type TitleMediaAssetResponse,
  type TitleMetadataVersion,
  type TitleMetadataVersionListResponse,
  type TitleRelease,
  type TitleReleaseListResponse,
  type TitleReleaseResponse,
  type TitleVisibility,
  type UpsertBoardProfileRequest,
  type UpsertTitleMediaAssetRequest,
  type UpsertTitleMetadataRequest,
  type UpsertTitleReleaseRequest,
  type UpdateDeveloperTitleRequest,
  type UserProfileResponse,
  type VerifiedDeveloperRoleStateResponse
} from "@board-enthusiasts/migration-contract";
import { problem, validationProblem } from "./http";

type AppUserRow = {
  id: string;
  auth_user_id: string;
  user_name: string;
  display_name: string | null;
  first_name: string | null;
  last_name: string | null;
  email: string | null;
  email_verified: boolean;
  identity_provider: string | null;
  avatar_url: string | null;
  avatar_storage_path: string | null;
  updated_at: string;
};

type AppUserRoleRow = {
  user_id: string;
  role: PlatformRole;
};

type MarketingContactRow = {
  id: string;
  email: string;
  normalized_email: string;
  first_name: string | null;
  status: MarketingContactStatus;
  lifecycle_status: MarketingContactLifecycleStatus;
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

type MarketingContactRoleInterestRow = {
  marketing_contact_id: string;
  role: MarketingContactRoleInterest;
  created_at: string;
};

type MarketingContactRecord = {
  contact: MarketingContactRow;
  roleInterests: MarketingContactRoleInterest[];
};

type StudioRow = {
  id: string;
  slug: string;
  display_name: string;
  description: string | null;
  logo_url: string | null;
  logo_storage_path: string | null;
  banner_url: string | null;
  banner_storage_path: string | null;
  created_at: string;
  updated_at: string;
  created_by_user_id: string;
};

type StudioMembershipRow = {
  studio_id: string;
  user_id: string;
  role: StudioMembershipRole;
};

type StudioLinkRow = {
  id: string;
  studio_id: string;
  label: string;
  url: string;
  created_at: string;
  updated_at: string;
};

type GenreRow = {
  slug: string;
  display_name: string;
};

type TitleRow = {
  id: string;
  studio_id: string;
  slug: string;
  content_kind: "game" | "app";
  lifecycle_status: TitleLifecycleStatus;
  visibility: TitleVisibility;
  is_reported: boolean;
  current_metadata_revision: number;
  display_name: string;
  short_description: string;
  description: string;
  genre_display: string;
  min_players: number;
  max_players: number;
  age_rating_authority: string;
  age_rating_value: string;
  min_age_years: number;
  current_release_id: string | null;
  current_release_version: string | null;
  current_release_published_at: string | null;
  acquisition_url: string | null;
  created_at: string;
  updated_at: string;
};

type TitleMetadataVersionGenreRow = {
  title_id: string;
  revision_number: number;
  genre_slug: string;
  display_order: number;
};

type AgeRatingAuthorityRow = {
  code: string;
  display_name: string;
};

type TitleMediaAssetRow = {
  id: string;
  title_id: string;
  media_role: "card" | "hero" | "logo";
  source_url: string;
  storage_path: string | null;
  alt_text: string | null;
  mime_type: string | null;
  width: number | null;
  height: number | null;
  created_at: string;
  updated_at: string;
};

type BoardProfileRow = {
  user_id: string;
  board_user_id: string;
  display_name: string | null;
  avatar_url: string | null;
  linked_at: string;
  last_synced_at: string;
};

type UserNotificationRow = {
  id: string;
  user_id: string;
  category: string;
  title: string;
  body: string;
  action_url: string | null;
  is_read: boolean;
  read_at: string | null;
  created_at: string;
  updated_at: string;
};

type PlayerLibraryRow = {
  user_id: string;
  title_id: string;
  created_at: string;
};

type PlayerWishlistRow = {
  user_id: string;
  title_id: string;
  created_at: string;
};

type TitleReportStatus =
  | "open"
  | "needs_developer_response"
  | "needs_player_response"
  | "developer_responded"
  | "player_responded"
  | "validated"
  | "invalidated";

type TitleReportRow = {
  id: string;
  title_id: string;
  reporter_user_id: string;
  status: TitleReportStatus;
  reason: string;
  resolution_note: string | null;
  resolved_by_user_id: string | null;
  resolved_at: string | null;
  created_at: string;
  updated_at: string;
};

type TitleMetadataVersionRow = {
  title_id: string;
  revision_number: number;
  is_current: boolean;
  is_frozen: boolean;
  display_name: string;
  short_description: string;
  description: string;
  genre_display: string;
  min_players: number;
  max_players: number;
  age_rating_authority: string;
  age_rating_value: string;
  min_age_years: number;
  created_at: string;
  updated_at: string;
};

type TitleReleaseStatus = "draft" | "published" | "withdrawn";

type TitleReleaseRow = {
  id: string;
  title_id: string;
  version: string;
  status: TitleReleaseStatus;
  metadata_revision_number: number;
  acquisition_url: string | null;
  is_current: boolean;
  published_at: string | null;
  created_at: string;
  updated_at: string;
};

type TitleReportMessageRow = {
  id: string;
  report_id: string;
  author_user_id: string;
  author_role: "player" | "developer" | "moderator";
  audience: "all" | "player" | "developer";
  message: string;
  created_at: string;
};

type WaveStateRow = {
  key: string;
  value: string;
};

const roleOrder: PlatformRole[] = ["player", "developer", "verified_developer", "moderator", "admin", "super_admin"];
const marketingRoleInterestOrder: MarketingContactRoleInterest[] = ["developer", "player"];
const acceptedImageMimeTypes = new Set(["image/png", "image/jpeg", "image/webp", "image/gif", "image/svg+xml"]);
const maxUploadBytes = 25 * 1024 * 1024;

function sortRoles(roles: PlatformRole[]): PlatformRole[] {
  return [...roles].sort((left, right) => roleOrder.indexOf(left) - roleOrder.indexOf(right));
}

function buildInitials(displayName: string | null, firstName: string | null, lastName: string | null, userName: string | null): string {
  const source = [displayName, [firstName, lastName].filter(Boolean).join(" ").trim(), userName]
    .find((candidate) => candidate && candidate.trim().length > 0)
    ?.trim();

  if (!source) {
    return "BE";
  }

  return source
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

function isAbsoluteUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

function normalizeEmailAddress(value: string): string {
  return value.trim().toLowerCase();
}

function isValidEmailAddress(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

function trimNullableString(value: string | null | undefined, maxLength: number): string | null {
  const trimmed = (value ?? "").trim();
  if (!trimmed) {
    return null;
  }

  return trimmed.slice(0, maxLength);
}

function isValidMarketingSource(value: string): boolean {
  return /^[a-z0-9][a-z0-9_-]{0,63}$/.test(value);
}

function sanitizeMarketingRoleInterests(value: MarketingSignupRequest["roleInterests"]): MarketingContactRoleInterest[] {
  if (!Array.isArray(value)) {
    return [];
  }

  const unique = new Set<MarketingContactRoleInterest>();
  for (const candidate of value) {
    if (candidate === "player" || candidate === "developer") {
      unique.add(candidate);
    }
  }

  return marketingRoleInterestOrder.filter((role) => unique.has(role));
}

function validateStudioSlug(slug: string): boolean {
  return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug);
}

function sanitizeUserName(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^[._-]+|[._-]+$/g, "");
}

function isNormalizedUserName(value: string): boolean {
  return /^[a-z0-9](?:[a-z0-9._-]*[a-z0-9])?$/.test(value);
}

function validateTitleSlug(slug: string): boolean {
  return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug);
}

function isPublicCatalogTitle(title: TitleRow): boolean {
  return title.visibility === "listed" && (title.lifecycle_status === "testing" || title.lifecycle_status === "published");
}

function isPublicCatalogDetail(title: TitleRow): boolean {
  return title.visibility !== "private" && (title.lifecycle_status === "testing" || title.lifecycle_status === "published");
}

function mapStudioLink(row: StudioLinkRow): StudioLink {
  return {
    id: row.id,
    label: row.label,
    url: row.url,
    createdAt: row.created_at,
    updatedAt: row.updated_at
  };
}

function mapTitleMediaAsset(row: TitleMediaAssetRow): TitleMediaAsset {
  return {
    id: row.id,
    mediaRole: row.media_role,
    sourceUrl: row.source_url,
    altText: row.alt_text,
    mimeType: row.mime_type,
    width: row.width,
    height: row.height,
    createdAt: row.created_at,
    updatedAt: row.updated_at
  };
}

function mapGenreDefinition(row: GenreRow): GenreDefinition {
  return {
    slug: row.slug,
    displayName: row.display_name
  };
}

function mapAgeRatingAuthorityDefinition(row: AgeRatingAuthorityRow): AgeRatingAuthorityDefinition {
  return {
    code: row.code,
    displayName: row.display_name
  };
}

function mapTitleMetadataVersion(row: TitleMetadataVersionRow, genreSlugs: string[]): TitleMetadataVersion {
  return {
    revisionNumber: row.revision_number,
    isCurrent: row.is_current,
    isFrozen: row.is_frozen,
    displayName: row.display_name,
    shortDescription: row.short_description,
    description: row.description,
    genreSlugs,
    genreDisplay: row.genre_display,
    minPlayers: row.min_players,
    maxPlayers: row.max_players,
    playerCountDisplay: buildPlayerCountDisplay(row.min_players, row.max_players),
    ageRatingAuthority: row.age_rating_authority,
    ageRatingValue: row.age_rating_value,
    minAgeYears: row.min_age_years,
    ageDisplay: buildAgeDisplay(row.age_rating_authority, row.age_rating_value),
    createdAt: row.created_at,
    updatedAt: row.updated_at
  };
}

function mapTitleRelease(row: TitleReleaseRow): TitleRelease {
  return {
    id: row.id,
    version: row.version,
    status: row.status,
    metadataRevisionNumber: row.metadata_revision_number,
    acquisitionUrl: row.acquisition_url,
    isCurrent: row.is_current,
    publishedAt: row.published_at,
    createdAt: row.created_at,
    updatedAt: row.updated_at
  };
}

function buildAcquisition(row: TitleRow): PublicTitleAcquisition | undefined {
  if (!row.acquisition_url) {
    return undefined;
  }

  return {
    url: row.acquisition_url
  };
}

function buildCatalogSummary(title: TitleRow, studio: StudioRow, mediaRows: TitleMediaAssetRow[]): CatalogTitleSummary {
  const cardImageUrl = mediaRows.find((row) => row.media_role === "card")?.source_url ?? null;
  const logoImageUrl = mediaRows.find((row) => row.media_role === "logo")?.source_url ?? null;
  return {
    id: title.id,
    studioId: title.studio_id,
    studioSlug: studio.slug,
    studioDisplayName: studio.display_name,
    slug: title.slug,
    contentKind: title.content_kind,
    lifecycleStatus: title.lifecycle_status,
    visibility: title.visibility,
    isReported: title.is_reported,
    currentMetadataRevision: title.current_metadata_revision,
    displayName: title.display_name,
    shortDescription: title.short_description,
    genreDisplay: title.genre_display,
    minPlayers: title.min_players,
    maxPlayers: title.max_players,
    playerCountDisplay: buildPlayerCountDisplay(title.min_players, title.max_players),
    ageRatingAuthority: title.age_rating_authority,
    ageRatingValue: title.age_rating_value,
    minAgeYears: title.min_age_years,
    ageDisplay: buildAgeDisplay(title.age_rating_authority, title.age_rating_value),
    cardImageUrl,
    logoImageUrl,
    acquisitionUrl: title.acquisition_url
  };
}

function buildCatalogDetail(title: TitleRow, studio: StudioRow, mediaRows: TitleMediaAssetRow[]): CatalogTitle {
  return {
    ...buildCatalogSummary(title, studio, mediaRows),
    description: title.description,
    mediaAssets: mediaRows.map(mapTitleMediaAsset),
    currentRelease:
      title.current_release_id && title.current_release_version && title.current_release_published_at
        ? {
            id: title.current_release_id,
            version: title.current_release_version,
            metadataRevisionNumber: title.current_metadata_revision,
            publishedAt: title.current_release_published_at
          }
        : undefined,
    acquisition: buildAcquisition(title),
    createdAt: title.created_at,
    updatedAt: title.updated_at
  };
}

function buildDeveloperTitle(title: TitleRow, studio: StudioRow, mediaRows: TitleMediaAssetRow[], genreSlugs: string[]): DeveloperTitle {
  return {
    id: title.id,
    studioId: title.studio_id,
    studioSlug: studio.slug,
    slug: title.slug,
    contentKind: title.content_kind,
    lifecycleStatus: title.lifecycle_status,
    visibility: title.visibility,
    currentMetadataRevision: title.current_metadata_revision,
    displayName: title.display_name,
    shortDescription: title.short_description,
    description: title.description,
    genreSlugs,
    genreDisplay: title.genre_display,
    minPlayers: title.min_players,
    maxPlayers: title.max_players,
    playerCountDisplay: buildPlayerCountDisplay(title.min_players, title.max_players),
    ageRatingAuthority: title.age_rating_authority,
    ageRatingValue: title.age_rating_value,
    minAgeYears: title.min_age_years,
    ageDisplay: buildAgeDisplay(title.age_rating_authority, title.age_rating_value),
    cardImageUrl: mediaRows.find((row) => row.media_role === "card")?.source_url ?? null,
    acquisitionUrl: title.acquisition_url,
    mediaAssets: mediaRows.map(mapTitleMediaAsset),
    currentRelease:
      title.current_release_id && title.current_release_version && title.current_release_published_at
        ? {
            id: title.current_release_id,
            version: title.current_release_version,
            metadataRevisionNumber: title.current_metadata_revision,
            publishedAt: title.current_release_published_at
          }
        : undefined,
    acquisition: buildAcquisition(title),
    currentReleaseId: title.current_release_id,
    createdAt: title.created_at,
    updatedAt: title.updated_at
  };
}

function parseConfigurationValue(value: Record<string, unknown> | null): Record<string, unknown> | null {
  return value ?? null;
}

function mapBoardProfile(row: BoardProfileRow) {
  return {
    boardUserId: row.board_user_id,
    displayName: row.display_name ?? row.board_user_id,
    avatarUrl: row.avatar_url,
    linkedAt: row.linked_at,
    lastSyncedAt: row.last_synced_at
  };
}

function mapUserNotification(row: UserNotificationRow): UserNotification {
  return {
    id: row.id,
    category: row.category,
    title: row.title,
    body: row.body,
    actionUrl: row.action_url,
    isRead: row.is_read,
    readAt: row.read_at,
    createdAt: row.created_at,
    updatedAt: row.updated_at
  };
}

function mapMarketingSignup(record: MarketingContactRecord): MarketingSignupResponse["signup"] {
  return {
    email: record.contact.email,
    firstName: record.contact.first_name,
    status: record.contact.status,
    lifecycleStatus: record.contact.lifecycle_status,
    roleInterests: record.roleInterests,
    source: record.contact.source,
    consentedAt: record.contact.consented_at,
    updatedAt: record.contact.updated_at
  };
}

function mapPlayerCollectionMutation(titleId: string, included: boolean, alreadyInRequestedState: boolean): PlayerCollectionMutationResponse {
  return {
    titleId,
    included,
    alreadyInRequestedState
  };
}

export interface Env {
  APP_ENV?: string;
  SUPABASE_URL?: string;
  SUPABASE_PUBLISHABLE_KEY?: string;
  SUPABASE_SECRET_KEY?: string;
  SUPABASE_MEDIA_BUCKET?: string;
  ALLOWED_WEB_ORIGINS?: string;
  TURNSTILE_SECRET_KEY?: string;
  BREVO_API_KEY?: string;
  BREVO_SIGNUPS_LIST_ID?: string;
  SUPPORT_REPORT_RECIPIENT?: string;
  SUPPORT_REPORT_SENDER_EMAIL?: string;
  SUPPORT_REPORT_SENDER_NAME?: string;
  MAILPIT_BASE_URL?: string;
}

export interface WorkerAppContext {
  envName: string;
  supabaseUrl: string;
  supabasePublishableKey: string;
  supabaseSecretKey: string;
  supabaseMediaBucket: string;
  allowedWebOrigins: string[];
  turnstileSecretKey: string | null;
  brevoApiKey: string | null;
  brevoSignupsListId: number | null;
  supportReportRecipient: string;
  supportReportSenderEmail: string;
  supportReportSenderName: string;
  mailpitBaseUrl: string | null;
}

interface SupportIssueReportRequest {
  category: "email_signup";
  firstName?: string | null;
  email?: string | null;
  pageUrl: string;
  apiBaseUrl: string;
  occurredAt: string;
  errorMessage: string;
  technicalDetails?: string | null;
  userAgent?: string | null;
  language?: string | null;
  timeZone?: string | null;
  viewportWidth?: number | null;
  viewportHeight?: number | null;
  screenWidth?: number | null;
  screenHeight?: number | null;
}

interface AuthenticatedUser {
  appUser: AppUserRow;
  roles: PlatformRole[];
}

interface StudioMutationRequest {
  slug: string;
  displayName: string;
  description?: string | null;
  logoUrl?: string | null;
  bannerUrl?: string | null;
}

interface StudioLinkMutationRequest {
  label: string;
  url: string;
}

function normalizeCatalogFilterValues(value: string | string[] | undefined): string[] {
  if (Array.isArray(value)) {
    return value.map((candidate) => candidate.trim()).filter(Boolean);
  }

  if (typeof value === "string" && value.trim().length > 0) {
    return [value.trim()];
  }

  return [];
}

function parseCatalogGenreTags(value: string | null | undefined): string[] {
  if (!value) {
    return [];
  }

  return value
    .split(",")
    .map((candidate) => candidate.trim())
    .filter(Boolean);
}

function buildGenreDisplayFromRows(genreSlugs: readonly string[], genreRows: GenreRow[]): string {
  const genreBySlug = new Map(genreRows.map((genre) => [genre.slug, genre]));
  return genreSlugs
    .map((genreSlug) => genreBySlug.get(normalizeGenreSlug(genreSlug))?.display_name ?? normalizeGenreSlug(genreSlug))
    .join(", ");
}

function buildGenreDisplayNameFromInput(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return "";
  }

  if (/[A-Z]/.test(trimmed) || /\s/.test(trimmed)) {
    return trimmed;
  }

  return trimmed
    .split("-")
    .filter(Boolean)
    .map((segment) => segment.charAt(0).toUpperCase() + segment.slice(1))
    .join(" ");
}

function createServiceClient(context: WorkerAppContext): SupabaseClient {
  return createClient(context.supabaseUrl, context.supabaseSecretKey, {
    auth: {
      autoRefreshToken: false,
      persistSession: false
    }
  });
}

function parseContext(env: Env): WorkerAppContext {
  const supabaseUrl = (env.SUPABASE_URL ?? "").trim();
  const supabasePublishableKey = (env.SUPABASE_PUBLISHABLE_KEY ?? "").trim();
  const supabaseSecretKey = (env.SUPABASE_SECRET_KEY ?? "").trim();
  const parsedBrevoListId = Number((env.BREVO_SIGNUPS_LIST_ID ?? "").trim());
  const mailpitBaseUrl = (env.MAILPIT_BASE_URL ?? "").trim();
  const allowedWebOrigins = (env.ALLOWED_WEB_ORIGINS ?? "")
    .split(",")
    .map((candidate) => candidate.trim())
    .filter(Boolean);

  if (!supabaseUrl || !supabasePublishableKey || !supabaseSecretKey) {
    throw problem(
      503,
      "backend_environment_incomplete",
      "Backend environment is incomplete.",
      "Supabase URL, publishable key, and secret key must be configured for the Workers API."
    );
  }

  return {
    envName: env.APP_ENV?.trim() || "local",
    supabaseUrl,
    supabasePublishableKey,
    supabaseSecretKey,
    supabaseMediaBucket: env.SUPABASE_MEDIA_BUCKET?.trim() || migrationMediaBucket,
    allowedWebOrigins,
    turnstileSecretKey: (env.TURNSTILE_SECRET_KEY ?? "").trim() || null,
    brevoApiKey: (env.BREVO_API_KEY ?? "").trim() || null,
    brevoSignupsListId: Number.isInteger(parsedBrevoListId) && parsedBrevoListId > 0 ? parsedBrevoListId : null,
    supportReportRecipient: (env.SUPPORT_REPORT_RECIPIENT ?? "").trim() || "support@boardenthusiasts.com",
    supportReportSenderEmail: (env.SUPPORT_REPORT_SENDER_EMAIL ?? "").trim() || "noreply@boardenthusiasts.com",
    supportReportSenderName: (env.SUPPORT_REPORT_SENDER_NAME ?? "").trim() || "Board Enthusiasts",
    mailpitBaseUrl: mailpitBaseUrl || ((env.APP_ENV?.trim() || "local") === "local" ? "http://127.0.0.1:55424" : null)
  };
}

export class WorkerAppService {
  private readonly context: WorkerAppContext;
  private readonly client: SupabaseClient;

  constructor(env: Env) {
    this.context = parseContext(env);
    this.client = createServiceClient(this.context);
  }

  getContext(): WorkerAppContext {
    return this.context;
  }

  async getReadyState(): Promise<Record<string, unknown>> {
    const { data, error } = await this.client
      .from("migration_wave_state")
      .select("key, value")
      .eq("key", "status");

    if (error) {
      throw problem(503, "supabase_unavailable", "Supabase is not ready.", error.message);
    }

    const readinessState = new Map((data as WaveStateRow[]).map((row) => [row.key, row.value]));
    return {
      status: "ready",
      environment: this.context.envName,
      stackStatus: readinessState.get("status") ?? "unknown",
      supabaseUrlConfigured: true,
      mediaBucket: this.context.supabaseMediaBucket
    };
  }

  async createMarketingSignup(input: MarketingSignupRequest): Promise<MarketingSignupResponse> {
    const email = input.email.trim();
    const normalizedEmail = normalizeEmailAddress(email);
    const firstName = trimNullableString(input.firstName, 100);
    const roleInterests = sanitizeMarketingRoleInterests(input.roleInterests);
    const source = input.source.trim().toLowerCase();
    const consentTextVersion = input.consentTextVersion.trim();

    if (!email) {
      throw validationProblem({
        email: ["Email is required."]
      });
    }

    if (!isValidEmailAddress(normalizedEmail)) {
      throw validationProblem({
        email: ["Enter a valid email address."]
      });
    }

    if (!isValidMarketingSource(source)) {
      throw validationProblem({
        source: ["Source must use lowercase letters, numbers, underscores, or hyphens."]
      });
    }

    if (!consentTextVersion) {
      throw validationProblem({
        consentTextVersion: ["Consent text version is required."]
      });
    }

    await this.verifyTurnstile(input.turnstileToken ?? null);

    const existing = await this.getMarketingContactByNormalizedEmail(normalizedEmail);
    const timestamp = new Date().toISOString();
    const rowToUpsert = {
      email,
      normalized_email: normalizedEmail,
      first_name: firstName,
      status: "subscribed",
      lifecycle_status: "waitlisted",
      consented_at: timestamp,
      consent_text_version: consentTextVersion.slice(0, 64),
      source,
      utm_source: trimNullableString(input.utmSource, 120),
      utm_medium: trimNullableString(input.utmMedium, 120),
      utm_campaign: trimNullableString(input.utmCampaign, 160),
      utm_term: trimNullableString(input.utmTerm, 160),
      utm_content: trimNullableString(input.utmContent, 160),
      updated_at: timestamp
    } satisfies Partial<MarketingContactRow>;

    const { data, error } = await this.client
      .from("marketing_contacts")
      .upsert(rowToUpsert, { onConflict: "normalized_email" })
      .select("*")
      .single();

    if (error || !data) {
      throw problem(500, "marketing_signup_failed", "Marketing signup could not be saved.", error?.message ?? "Unknown database failure.");
    }

    const saved = data as MarketingContactRow;
    await this.replaceMarketingContactRoleInterests(saved.id, roleInterests);
    await this.syncMarketingContactToBrevo(saved, roleInterests);
    const refreshed = (await this.getMarketingContactRecordByNormalizedEmail(normalizedEmail)) ?? {
      contact: saved,
      roleInterests
    };

    return {
      accepted: true,
      duplicate: existing !== null,
      signup: mapMarketingSignup(refreshed)
    };
  }

  async reportSupportIssue(input: SupportIssueReportRequest): Promise<{ accepted: true }> {
    if (input.category !== "email_signup") {
      throw validationProblem({
        category: ["Category must be 'email_signup'."]
      });
    }

    const firstName = trimNullableString(input.firstName, 100);
    const email = trimNullableString(input.email, 320);
    const normalizedEmail = email ? normalizeEmailAddress(email) : null;

    if (normalizedEmail && !isValidEmailAddress(normalizedEmail)) {
      throw validationProblem({
        email: ["Enter a valid email address when supplied."]
      });
    }

    const pageUrl = trimNullableString(input.pageUrl, 1000);
    const apiBaseUrl = trimNullableString(input.apiBaseUrl, 500);
    const occurredAt = trimNullableString(input.occurredAt, 100);
    const errorMessage = trimNullableString(input.errorMessage, 2000);
    const technicalDetails = trimNullableString(input.technicalDetails, 4000);
    if (!pageUrl || !apiBaseUrl || !occurredAt || !errorMessage) {
      throw validationProblem({
        body: ["Support issue reports require pageUrl, apiBaseUrl, occurredAt, and errorMessage."]
      });
    }

    const lines = [
      "Board Enthusiasts landing-page issue report",
      "",
      `Category: ${input.category}`,
      `Occurred at: ${occurredAt}`,
      `Page URL: ${pageUrl}`,
      `API base URL: ${apiBaseUrl}`,
      `First name: ${firstName ?? "(not provided)"}`,
      `Email: ${normalizedEmail ?? "(not provided)"}`,
      `Error: ${errorMessage}`,
      `User agent: ${trimNullableString(input.userAgent, 1000) ?? "(not provided)"}`,
      `Language: ${trimNullableString(input.language, 40) ?? "(not provided)"}`,
      `Time zone: ${trimNullableString(input.timeZone, 100) ?? "(not provided)"}`,
      `Viewport: ${input.viewportWidth ?? "?"}x${input.viewportHeight ?? "?"}`,
      `Screen: ${input.screenWidth ?? "?"}x${input.screenHeight ?? "?"}`,
      `Environment: ${this.context.envName}`,
      `Technical details: ${technicalDetails ?? "(not provided)"}`,
    ];

    await this.sendSupportIssueEmail({
      subject: "[Bug Report] Email signup issue",
      text: lines.join("\n"),
      replyToEmail: normalizedEmail,
      replyToName: firstName,
    });

    return { accepted: true };
  }

  async getCurrentUserResponse(token: string): Promise<CurrentUserResponse> {
    const user = await this.requireUser(token);

    return {
      subject: user.appUser.auth_user_id,
      displayName: user.appUser.display_name ?? user.appUser.user_name,
      email: user.appUser.email,
      emailVerified: user.appUser.email_verified,
      identityProvider: user.appUser.identity_provider,
      roles: user.roles
    };
  }

  async getCurrentUserProfile(token: string): Promise<UserProfileResponse> {
    const user = await this.requireUser(token);

    return {
      profile: {
        subject: user.appUser.auth_user_id,
        displayName: user.appUser.display_name,
        userName: user.appUser.user_name,
        firstName: user.appUser.first_name,
        lastName: user.appUser.last_name,
        email: user.appUser.email,
        emailVerified: user.appUser.email_verified,
        avatarUrl: user.appUser.avatar_url,
        avatarDataUrl: null,
        initials: buildInitials(
          user.appUser.display_name,
          user.appUser.first_name,
          user.appUser.last_name,
          user.appUser.user_name
        ),
        updatedAt: user.appUser.updated_at
      }
    };
  }

  async updateCurrentUserProfile(token: string, input: UpdateUserProfileRequest): Promise<UserProfileResponse> {
    const user = await this.requireUser(token);

    if (input.displayName !== undefined && input.displayName !== null && input.displayName.trim().length === 0) {
      throw validationProblem({
        displayName: ["Display name cannot be empty when supplied."]
      });
    }

    const profileUpdate: Record<string, unknown> = {
      updated_at: new Date().toISOString()
    };

    if (input.displayName !== undefined) {
      profileUpdate.display_name = input.displayName?.trim() ?? null;
    }
    if (input.firstName !== undefined) {
      profileUpdate.first_name = input.firstName?.trim() || null;
    }
    if (input.lastName !== undefined) {
      profileUpdate.last_name = input.lastName?.trim() || null;
    }
    if (input.avatarUrl !== undefined || input.avatarDataUrl !== undefined) {
      profileUpdate.avatar_url =
        (typeof input.avatarUrl === "string" && input.avatarUrl.trim().length > 0
          ? input.avatarUrl.trim()
          : typeof input.avatarDataUrl === "string" && input.avatarDataUrl.trim().length > 0
            ? input.avatarDataUrl.trim()
            : null);
      profileUpdate.avatar_storage_path = null;
    }

    const { error } = await this.client
      .from("app_users")
      .update(profileUpdate)
      .eq("id", user.appUser.id);

    if (error) {
      throw problem(500, "profile_update_failed", "Profile update failed.", error.message);
    }

    return this.getCurrentUserProfile(token);
  }

  async getUserNameAvailability(requestedUserName: string): Promise<UserNameAvailabilityResponse> {
    const normalizedUserName = sanitizeUserName(requestedUserName);
    if (!normalizedUserName || !isNormalizedUserName(normalizedUserName) || normalizedUserName !== requestedUserName.trim()) {
      return {
        userNameAvailability: {
          requestedUserName,
          normalizedUserName,
          available: false,
        },
      };
    }

    const { data, error } = await this.client.from("app_users").select("id").eq("user_name", normalizedUserName).limit(1);
    if (error) {
      throw problem(500, "identity_lookup_failed", "Identity lookup failed.", error.message);
    }

    if ((data as Array<{ id: string }>).length > 0) {
      return {
        userNameAvailability: {
          requestedUserName,
          normalizedUserName,
          available: false,
        },
      };
    }

    const perPage = 200;
    for (let page = 1; page <= 20; page += 1) {
      const { data: authData, error: authError } = await this.client.auth.admin.listUsers({ page, perPage });
      if (authError) {
        throw problem(500, "identity_lookup_failed", "Identity lookup failed.", authError.message);
      }

      const users = authData.users ?? [];
      const reserved = users.some((user) => {
        const metadata = (user.user_metadata ?? {}) as Record<string, unknown>;
        const candidate = typeof metadata.userName === "string" ? sanitizeUserName(metadata.userName) : "";
        return candidate === normalizedUserName;
      });
      if (reserved) {
        return {
          userNameAvailability: {
            requestedUserName,
            normalizedUserName,
            available: false,
          },
        };
      }

      if (users.length < perPage) {
        break;
      }
    }

    return {
      userNameAvailability: {
        requestedUserName,
        normalizedUserName,
        available: true,
      },
    };
  }

  async getBoardProfile(token: string): Promise<BoardProfileResponse> {
    const user = await this.requireUser(token);
    const { data, error } = await this.client
      .from("user_board_profiles")
      .select("*")
      .eq("user_id", user.appUser.id)
      .limit(1);
    if (error) {
      throw problem(500, "board_profile_lookup_failed", "Board profile lookup failed.", error.message);
    }

    const profile = (data as BoardProfileRow[])[0];
    if (!profile) {
      throw problem(404, "board_profile_not_found", "Board profile not found.", "No linked Board profile exists for the current user.");
    }

    return {
      boardProfile: mapBoardProfile(profile)
    };
  }

  async upsertBoardProfile(token: string, input: UpsertBoardProfileRequest): Promise<BoardProfileResponse> {
    const user = await this.requireUser(token);
    if (!input.boardUserId.trim()) {
      throw validationProblem({
        boardUserId: ["Board user ID is required."]
      });
    }

    const existing = await this.findBoardProfileByUserId(user.appUser.id);
    const now = new Date().toISOString();
    const { error } = await this.client.from("user_board_profiles").upsert(
      {
        user_id: user.appUser.id,
        board_user_id: input.boardUserId.trim(),
        display_name: input.displayName?.trim() || null,
        avatar_url: input.avatarUrl?.trim() || null,
        linked_at: existing?.linked_at ?? now,
        last_synced_at: input.lastSyncedAt?.trim() || now,
        updated_at: now
      },
      { onConflict: "user_id" }
    );
    if (error) {
      throw problem(500, "board_profile_upsert_failed", "Board profile update failed.", error.message);
    }

    return this.getBoardProfile(token);
  }

  async deleteBoardProfile(token: string): Promise<void> {
    const user = await this.requireUser(token);
    const { error } = await this.client.from("user_board_profiles").delete().eq("user_id", user.appUser.id);
    if (error) {
      throw problem(500, "board_profile_delete_failed", "Board profile delete failed.", error.message);
    }
  }

  async getCurrentUserNotifications(token: string): Promise<UserNotificationListResponse> {
    const user = await this.requireUser(token);
    const { data, error } = await this.client
      .from("user_notifications")
      .select("*")
      .eq("user_id", user.appUser.id)
      .order("created_at", { ascending: false })
      .limit(40);
    if (error) {
      throw problem(500, "notification_lookup_failed", "Notification lookup failed.", error.message);
    }

    return {
      notifications: (data as UserNotificationRow[]).map(mapUserNotification)
    };
  }

  async markCurrentUserNotificationRead(token: string, notificationId: string): Promise<UserNotificationResponse> {
    const user = await this.requireUser(token);
    const { data, error } = await this.client
      .from("user_notifications")
      .select("*")
      .eq("id", notificationId)
      .eq("user_id", user.appUser.id)
      .limit(1);
    if (error) {
      throw problem(500, "notification_lookup_failed", "Notification lookup failed.", error.message);
    }

    const notification = (data as UserNotificationRow[])[0];
    if (!notification) {
      throw problem(404, "notification_not_found", "Notification not found.", "The requested notification was not found.");
    }

    if (!notification.is_read) {
      const now = new Date().toISOString();
      const { data: updated, error: updateError } = await this.client
        .from("user_notifications")
        .update({
          is_read: true,
          read_at: now,
          updated_at: now
        })
        .eq("id", notification.id)
        .eq("user_id", user.appUser.id)
        .select("*")
        .single();
      if (updateError) {
        throw problem(500, "notification_update_failed", "Notification update failed.", updateError.message);
      }

      return {
        notification: mapUserNotification(updated as unknown as UserNotificationRow)
      };
    }

    return {
      notification: mapUserNotification(notification)
    };
  }

  async getDeveloperEnrollment(token: string): Promise<DeveloperEnrollmentResponse> {
    const user = await this.requireUser(token);
    const developerAccessEnabled = user.roles.includes("developer");
    const verifiedDeveloper = user.roles.includes("verified_developer");

    return {
      developerEnrollment: {
        status: developerAccessEnabled ? "enrolled" : "not_enrolled",
        actionRequiredBy: "none",
        developerAccessEnabled,
        verifiedDeveloper,
        canSubmitRequest: !developerAccessEnabled
      }
    };
  }

  async enrollCurrentUserAsDeveloper(token: string): Promise<DeveloperEnrollmentResponse> {
    const user = await this.requireUser(token);
    const alreadyEnrolled = user.roles.includes("developer");
    if (!alreadyEnrolled) {
      const { error } = await this.client.from("app_user_roles").insert({
        user_id: user.appUser.id,
        role: "developer"
      });
      if (error) {
        throw problem(500, "developer_enrollment_failed", "Developer enrollment failed.", error.message);
      }
    }

    return this.getDeveloperEnrollment(token);
  }

  async getPlayerLibrary(token: string): Promise<PlayerTitleListResponse> {
    const user = await this.requireUser(token);
    return {
      titles: await this.getPlayerCollectionTitles(user.appUser.id, "library")
    };
  }

  async setPlayerLibraryState(token: string, titleId: string, included: boolean): Promise<PlayerCollectionMutationResponse> {
    const user = await this.requireUser(token);
    return this.setPlayerCollectionState(user.appUser.id, titleId, included, "library");
  }

  async getPlayerWishlist(token: string): Promise<PlayerTitleListResponse> {
    const user = await this.requireUser(token);
    return {
      titles: await this.getPlayerCollectionTitles(user.appUser.id, "wishlist")
    };
  }

  async setPlayerWishlistState(token: string, titleId: string, included: boolean): Promise<PlayerCollectionMutationResponse> {
    const user = await this.requireUser(token);
    return this.setPlayerCollectionState(user.appUser.id, titleId, included, "wishlist");
  }

  async listPlayerTitleReports(token: string): Promise<PlayerTitleReportListResponse> {
    const user = await this.requireUser(token);
    const reports = await this.getTitleReportsByReporter(user.appUser.id);
    const reportSummaries = await this.buildTitleReportSummaries(reports);

    return {
      reports: reportSummaries.map((report) => ({
        id: report.id,
        titleId: report.titleId,
        studioSlug: report.studioSlug,
        titleSlug: report.titleSlug,
        titleDisplayName: report.titleDisplayName,
        status: report.status,
        reason: report.reason,
        createdAt: report.createdAt,
        updatedAt: report.updatedAt
      }))
    };
  }

  async createPlayerTitleReport(token: string, input: CreatePlayerTitleReportRequest): Promise<PlayerTitleReportResponse> {
    const user = await this.requireUser(token);
    const errors: Record<string, string[]> = {};
    if (!input.titleId || input.titleId.trim().length === 0) {
      errors.titleId = ["Title identifier is required."];
    }
    if (!input.reason || input.reason.trim().length === 0) {
      errors.reason = ["Report reason is required."];
    }
    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }

    const title = await this.getTitleById(input.titleId);
    const existing = await this.findOpenTitleReportForPlayer(user.appUser.id, title.id);
    if (existing) {
      const summaries = await this.buildTitleReportSummaries([existing]);
      return {
        report: {
          id: summaries[0]!.id,
          titleId: summaries[0]!.titleId,
          studioSlug: summaries[0]!.studioSlug,
          titleSlug: summaries[0]!.titleSlug,
          titleDisplayName: summaries[0]!.titleDisplayName,
          status: summaries[0]!.status,
          reason: summaries[0]!.reason,
          createdAt: summaries[0]!.createdAt,
          updatedAt: summaries[0]!.updatedAt
        }
      };
    }

    const now = new Date().toISOString();
    const { data, error } = await this.client
      .from("title_reports")
      .insert({
        title_id: title.id,
        reporter_user_id: user.appUser.id,
        status: "open",
        reason: input.reason.trim(),
        created_at: now,
        updated_at: now
      })
      .select("*")
      .single();
    if (error) {
      throw problem(500, "title_report_create_failed", "Title report creation failed.", error.message);
    }

    await this.client.from("titles").update({ is_reported: true }).eq("id", title.id);
    const report = data as unknown as TitleReportRow;
    await this.createTitleReportNotifications(report, title, user.appUser);
    const summaries = await this.buildTitleReportSummaries([report]);
    return {
      report: {
        id: summaries[0]!.id,
        titleId: summaries[0]!.titleId,
        studioSlug: summaries[0]!.studioSlug,
        titleSlug: summaries[0]!.titleSlug,
        titleDisplayName: summaries[0]!.titleDisplayName,
        status: summaries[0]!.status,
        reason: summaries[0]!.reason,
        createdAt: summaries[0]!.createdAt,
        updatedAt: summaries[0]!.updatedAt
      }
    };
  }

  async getPlayerTitleReport(token: string, reportId: string): Promise<TitleReportDetailResponse> {
    const user = await this.requireUser(token);
    const report = await this.getTitleReportById(reportId);
    if (report.reporter_user_id !== user.appUser.id) {
      throw problem(403, "player_report_access_required", "Player report access is required.", "Caller does not have access to this title report.");
    }

    return {
      report: await this.buildTitleReportDetail(report)
    };
  }

  async addPlayerTitleReportMessage(token: string, reportId: string, input: AddTitleReportMessageRequest): Promise<TitleReportDetailResponse> {
    const user = await this.requireUser(token);
    const report = await this.getTitleReportById(reportId);
    if (report.reporter_user_id !== user.appUser.id) {
      throw problem(403, "player_report_access_required", "Player report access is required.", "Caller does not have access to this title report.");
    }

    this.assertTitleReportOpenForMessaging(report);
    this.validateTitleReportMessage(input.message);

    const now = new Date().toISOString();
    const { error: messageError } = await this.client.from("title_report_messages").insert({
      report_id: report.id,
      author_user_id: user.appUser.id,
      author_role: "player",
      audience: "all",
      message: input.message.trim(),
      created_at: now
    });
    if (messageError) {
      throw problem(500, "title_report_message_failed", "Title report message failed.", messageError.message);
    }

    const { error: reportError } = await this.client
      .from("title_reports")
      .update({
        status: "player_responded",
        updated_at: now
      })
      .eq("id", report.id);
    if (reportError) {
      throw problem(500, "title_report_message_failed", "Title report message failed.", reportError.message);
    }

    await this.createPlayerReportReplyNotifications(report, input.message.trim(), user.appUser);
    return {
      report: await this.buildTitleReportDetail(await this.getTitleReportById(report.id))
    };
  }

  async listModerationDevelopers(token: string, search?: string | null): Promise<ModerationDeveloperListResponse> {
    const user = await this.requireUser(token);
    this.assertModerator(user.roles);

    const developerUserIds = await this.listUserIdsByRole("developer");
    const developers = await this.getUsersByIds(developerUserIds);
    const normalizedSearch = (search ?? "").trim().toLowerCase();

    return {
      developers: developers
        .filter((developer) => {
          if (!normalizedSearch) {
            return true;
          }

          return [developer.user_name, developer.display_name, developer.email]
            .filter((candidate): candidate is string => Boolean(candidate))
            .some((candidate) => candidate.toLowerCase().includes(normalizedSearch));
        })
        .sort((left, right) => (left.display_name ?? left.user_name).localeCompare(right.display_name ?? right.user_name))
        .map((developer) => ({
          developerSubject: developer.auth_user_id,
          userName: developer.user_name,
          displayName: developer.display_name,
          email: developer.email
        }))
    };
  }

  async getVerifiedDeveloperState(token: string, developerIdentifier: string): Promise<VerifiedDeveloperRoleStateResponse> {
    const user = await this.requireUser(token);
    this.assertModerator(user.roles);

    const developer = await this.findDeveloperByIdentifier(developerIdentifier);
    const roles = await this.getRolesForUser(developer.id);
    return {
      verifiedDeveloperRoleState: {
        developerSubject: developer.auth_user_id,
        verifiedDeveloper: roles.includes("verified_developer"),
        alreadyInRequestedState: false
      }
    };
  }

  async setVerifiedDeveloperState(token: string, developerIdentifier: string, verified: boolean): Promise<VerifiedDeveloperRoleStateResponse> {
    const user = await this.requireUser(token);
    this.assertModerator(user.roles);

    const developer = await this.findDeveloperByIdentifier(developerIdentifier);
    const roles = await this.getRolesForUser(developer.id);
    const alreadyInRequestedState = roles.includes("verified_developer") === verified;

    if (!alreadyInRequestedState) {
      if (verified) {
        const { error } = await this.client.from("app_user_roles").insert({
          user_id: developer.id,
          role: "verified_developer"
        });
        if (error) {
          throw problem(500, "verified_developer_role_update_failed", "Verified developer role update failed.", error.message);
        }
      } else {
        const { error } = await this.client
          .from("app_user_roles")
          .delete()
          .eq("user_id", developer.id)
          .eq("role", "verified_developer");
        if (error) {
          throw problem(500, "verified_developer_role_update_failed", "Verified developer role update failed.", error.message);
        }
      }
    }

    return {
      verifiedDeveloperRoleState: {
        developerSubject: developer.auth_user_id,
        verifiedDeveloper: verified,
        alreadyInRequestedState
      }
    };
  }

  async listModerationTitleReports(token: string): Promise<TitleReportListResponse> {
    const user = await this.requireUser(token);
    this.assertModerator(user.roles);

    return {
      reports: await this.buildTitleReportSummaries(await this.getTitleReports())
    };
  }

  async getModerationTitleReport(token: string, reportId: string): Promise<TitleReportDetailResponse> {
    const user = await this.requireUser(token);
    this.assertModerator(user.roles);

    return {
      report: await this.buildTitleReportDetail(await this.getTitleReportById(reportId))
    };
  }

  async addModerationTitleReportMessage(
    token: string,
    reportId: string,
    input: AddModerationTitleReportMessageRequest
  ): Promise<TitleReportDetailResponse> {
    const user = await this.requireUser(token);
    this.assertModerator(user.roles);

    const report = await this.getTitleReportById(reportId);
    this.assertTitleReportOpenForMessaging(report);
    this.validateTitleReportMessage(input.message);

    if (input.recipientRole !== "player" && input.recipientRole !== "developer") {
      throw validationProblem({
        recipientRole: ["Recipient role must be either player or developer."]
      });
    }

    const now = new Date().toISOString();
    const { error: messageError } = await this.client.from("title_report_messages").insert({
      report_id: report.id,
      author_user_id: user.appUser.id,
      author_role: "moderator",
      audience: input.recipientRole,
      message: input.message.trim(),
      created_at: now
    });
    if (messageError) {
      throw problem(500, "title_report_message_failed", "Title report message failed.", messageError.message);
    }

    const nextStatus: TitleReportStatus = input.recipientRole === "player" ? "needs_player_response" : "needs_developer_response";
    const { error: reportError } = await this.client
      .from("title_reports")
      .update({
        status: nextStatus,
        updated_at: now
      })
      .eq("id", report.id);
    if (reportError) {
      throw problem(500, "title_report_message_failed", "Title report message failed.", reportError.message);
    }

    await this.createModerationReportMessageNotifications(report, input.message.trim(), input.recipientRole, user.appUser);
    return {
      report: await this.buildTitleReportDetail(await this.getTitleReportById(report.id))
    };
  }

  async resolveModerationTitleReport(
    token: string,
    reportId: string,
    validate: boolean,
    input: ModerateTitleReportDecisionRequest
  ): Promise<TitleReportDetailResponse> {
    const user = await this.requireUser(token);
    this.assertModerator(user.roles);

    const report = await this.getTitleReportById(reportId);
    const now = new Date().toISOString();
    const nextStatus: TitleReportStatus = validate ? "validated" : "invalidated";
    const { error } = await this.client
      .from("title_reports")
      .update({
        status: nextStatus,
        resolution_note: input.note?.trim() ? input.note.trim() : null,
        resolved_by_user_id: user.appUser.id,
        resolved_at: now,
        updated_at: now
      })
      .eq("id", report.id);
    if (error) {
      throw problem(500, "title_report_resolution_failed", "Title report resolution failed.", error.message);
    }

    await this.createModerationDecisionNotifications(report, validate, input.note?.trim() ?? null, user.appUser);
    return {
      report: await this.buildTitleReportDetail(await this.getTitleReportById(report.id))
    };
  }

  async listPublicStudios(): Promise<StudioListResponse> {
    const studios = await this.getStudios();
    const linksByStudioId = await this.getStudioLinksByStudioIds(studios.map((studio) => studio.id));

    return {
      studios: studios
        .sort((left, right) => left.display_name.localeCompare(right.display_name))
        .map((studio) => this.buildStudioSummary(studio, linksByStudioId.get(studio.id) ?? []))
    };
  }

  async listGenres(): Promise<GenreListResponse> {
    const genres = await this.getGenres();
    return {
      genres: genres.map(mapGenreDefinition)
    };
  }

  async listAgeRatingAuthorities(): Promise<AgeRatingAuthorityListResponse> {
    const ageRatingAuthorities = await this.getAgeRatingAuthorities();
    return {
      ageRatingAuthorities: ageRatingAuthorities.map(mapAgeRatingAuthorityDefinition)
    };
  }

  async getPublicStudio(slug: string): Promise<StudioResponse> {
    const studio = await this.getStudioBySlug(slug);
    const linksByStudioId = await this.getStudioLinksByStudioIds([studio.id]);

    return {
      studio: this.buildStudio(studio, linksByStudioId.get(studio.id) ?? [])
    };
  }

  async listManagedStudios(token: string): Promise<DeveloperStudioListResponse> {
    const user = await this.requireUser(token);
    const memberships = await this.getStudioMembershipsForUser(user.appUser.id);
    const studios = memberships.length > 0 ? await this.getStudiosByIds(memberships.map((membership) => membership.studio_id)) : [];
    const linksByStudioId = await this.getStudioLinksByStudioIds(studios.map((studio) => studio.id));
    const roleByStudioId = new Map(memberships.map((membership) => [membership.studio_id, membership.role]));

    return {
      studios: studios
        .sort((left, right) => left.display_name.localeCompare(right.display_name))
        .map((studio) => ({
          ...this.buildStudioSummary(studio, linksByStudioId.get(studio.id) ?? []),
          role: roleByStudioId.get(studio.id) ?? "editor"
        }))
    };
  }

  async createStudio(token: string, input: StudioMutationRequest): Promise<StudioResponse> {
    const user = await this.requireUser(token);
    if (!this.canCreateStudio(user.roles)) {
      throw problem(403, "developer_access_required", "Developer access is required.", "Only developers and above can create studios.");
    }

    this.validateStudioMutation(input);
    const existing = await this.findStudioBySlug(input.slug);
    if (existing) {
      throw problem(409, "studio_slug_conflict", "Studio already exists", "The supplied studio slug is already in use.");
    }

    const now = new Date().toISOString();
    const { data, error } = await this.client
      .from("studios")
      .insert({
        slug: input.slug,
        display_name: input.displayName,
        description: input.description ?? null,
        logo_url: input.logoUrl ?? null,
        banner_url: input.bannerUrl ?? null,
        created_by_user_id: user.appUser.id,
        created_at: now,
        updated_at: now
      })
      .select("*")
      .single();
    if (error) {
      throw problem(500, "studio_create_failed", "Studio creation failed.", error.message);
    }

    const studio = data as unknown as StudioRow;
    const { error: membershipError } = await this.client.from("studio_memberships").insert({
      studio_id: studio.id,
      user_id: user.appUser.id,
      role: "owner",
      joined_at: now,
      updated_at: now
    });
    if (membershipError) {
      throw problem(500, "studio_create_failed", "Studio creation failed.", membershipError.message);
    }

    return {
      studio: this.buildStudio(studio, [])
    };
  }

  async updateStudio(token: string, studioId: string, input: StudioMutationRequest): Promise<StudioResponse> {
    const user = await this.requireUser(token);
    const studio = await this.getStudioById(studioId);
    await this.requireStudioAccess(user.appUser.id, studio.id);
    this.validateStudioMutation(input);

    const existing = await this.findStudioBySlug(input.slug);
    if (existing && existing.id !== studio.id) {
      throw problem(409, "studio_slug_conflict", "Studio already exists", "The supplied studio slug is already in use.");
    }

    const { error } = await this.client
      .from("studios")
      .update({
        slug: input.slug,
        display_name: input.displayName,
        description: input.description ?? null,
        logo_url: input.logoUrl ?? null,
        banner_url: input.bannerUrl ?? null,
        updated_at: new Date().toISOString()
      })
      .eq("id", studio.id);
    if (error) {
      throw problem(500, "studio_update_failed", "Studio update failed.", error.message);
    }

    const refreshedStudio = await this.getStudioById(studio.id);
    const links = (await this.getStudioLinksByStudioIds([studio.id])).get(studio.id) ?? [];
    return {
      studio: this.buildStudio(refreshedStudio, links)
    };
  }

  async deleteStudio(token: string, studioId: string): Promise<void> {
    const user = await this.requireUser(token);
    await this.requireStudioOwner(user.appUser.id, studioId);

    const { error } = await this.client.from("studios").delete().eq("id", studioId);
    if (error) {
      throw problem(500, "studio_delete_failed", "Studio delete failed.", error.message);
    }
  }

  async listStudioLinks(token: string, studioId: string): Promise<StudioLinkListResponse> {
    const user = await this.requireUser(token);
    await this.requireStudioAccess(user.appUser.id, studioId);

    return {
      links: (await this.getStudioLinksByStudioIds([studioId])).get(studioId)?.map(mapStudioLink) ?? []
    };
  }

  async createStudioLink(token: string, studioId: string, input: StudioLinkMutationRequest): Promise<StudioLinkResponse> {
    const user = await this.requireUser(token);
    await this.requireStudioAccess(user.appUser.id, studioId);
    this.validateStudioLinkMutation(input);

    const { data, error } = await this.client
      .from("studio_links")
      .insert({
        studio_id: studioId,
        label: input.label,
        url: input.url,
        created_at: new Date().toISOString(),
        updated_at: new Date().toISOString()
      })
      .select("*")
      .single();
    if (error) {
      throw problem(500, "studio_link_create_failed", "Studio link create failed.", error.message);
    }

    return {
      link: mapStudioLink(data as unknown as StudioLinkRow)
    };
  }

  async updateStudioLink(token: string, studioId: string, linkId: string, input: StudioLinkMutationRequest): Promise<StudioLinkResponse> {
    const user = await this.requireUser(token);
    await this.requireStudioAccess(user.appUser.id, studioId);
    this.validateStudioLinkMutation(input);
    await this.requireStudioLink(studioId, linkId);

    const { data, error } = await this.client
      .from("studio_links")
      .update({
        label: input.label,
        url: input.url,
        updated_at: new Date().toISOString()
      })
      .eq("id", linkId)
      .eq("studio_id", studioId)
      .select("*")
      .single();
    if (error) {
      throw problem(500, "studio_link_update_failed", "Studio link update failed.", error.message);
    }

    return {
      link: mapStudioLink(data as unknown as StudioLinkRow)
    };
  }

  async deleteStudioLink(token: string, studioId: string, linkId: string): Promise<void> {
    const user = await this.requireUser(token);
    await this.requireStudioAccess(user.appUser.id, studioId);
    await this.requireStudioLink(studioId, linkId);

    const { error } = await this.client.from("studio_links").delete().eq("id", linkId).eq("studio_id", studioId);
    if (error) {
      throw problem(500, "studio_link_delete_failed", "Studio link delete failed.", error.message);
    }
  }

  async uploadStudioMedia(token: string, studioId: string, kind: "logo" | "banner", file: File | null): Promise<StudioResponse> {
    const user = await this.requireUser(token);
    const studio = await this.getStudioById(studioId);
    await this.requireStudioAccess(user.appUser.id, studio.id);
    const validatedFile = this.requireUploadFile(file);

    const extension = this.extensionForMimeType(validatedFile.type);
    const storagePath = `studios/${studio.slug}/${kind}${extension}`;
    const { error: uploadError } = await this.client.storage
      .from(this.context.supabaseMediaBucket)
      .upload(storagePath, validatedFile, { contentType: validatedFile.type, upsert: true });
    if (uploadError) {
      throw problem(500, "studio_media_upload_failed", "Studio media upload failed.", uploadError.message);
    }

    const publicUrl = this.client.storage.from(this.context.supabaseMediaBucket).getPublicUrl(storagePath).data.publicUrl;
    const updatePayload =
      kind === "logo"
        ? {
            logo_url: publicUrl,
            logo_storage_path: storagePath,
            updated_at: new Date().toISOString()
          }
        : {
            banner_url: publicUrl,
            banner_storage_path: storagePath,
            updated_at: new Date().toISOString()
          };
    const { error: updateError } = await this.client.from("studios").update(updatePayload).eq("id", studio.id);
    if (updateError) {
      throw problem(500, "studio_media_upload_failed", "Studio media upload failed.", updateError.message);
    }

    const links = (await this.getStudioLinksByStudioIds([studio.id])).get(studio.id) ?? [];
    const refreshedStudio = await this.getStudioById(studio.id);
    return {
      studio: this.buildStudio(refreshedStudio, links)
    };
  }

  async getStudioTitles(token: string, studioId: string): Promise<DeveloperTitleListResponse> {
    const user = await this.requireUser(token);
    await this.requireStudioAccess(user.appUser.id, studioId);

    const studio = await this.getStudioById(studioId);
    const titles = await this.getTitlesByStudioId(studioId);
    const mediaByTitleId = await this.getTitleMediaByTitleIds(titles.map((title) => title.id));

    return {
      titles: titles
        .sort((left, right) => left.display_name.localeCompare(right.display_name))
        .map((title) => buildCatalogSummary(title, studio, mediaByTitleId.get(title.id) ?? []))
    };
  }

  async createTitle(token: string, studioId: string, input: CreateDeveloperTitleRequest): Promise<DeveloperTitleResponse> {
    const user = await this.requireUser(token);
    const studio = await this.getStudioById(studioId);
    await this.requireStudioAccess(user.appUser.id, studio.id);
    this.validateCreateDeveloperTitle(input);

    const existing = await this.findTitleByStudioAndSlug(studio.id, input.slug);
    if (existing) {
      throw problem(409, "title_slug_conflict", "Title already exists.", "The supplied title slug is already in use for this studio.");
    }

    const now = new Date().toISOString();
    const metadata = input.metadata;
    const genreRows = await this.requireGenres(metadata.genreSlugs);
    const genreDisplay = buildGenreDisplayFromRows(metadata.genreSlugs, genreRows);
    const ageRatingAuthority = await this.getAgeRatingAuthorityByCode(metadata.ageRatingAuthority);
    if (!ageRatingAuthority) {
      throw validationProblem({
        ageRatingAuthority: [`Unknown age rating authority: ${metadata.ageRatingAuthority}.`]
      });
    }
    const { data, error } = await this.client
      .from("titles")
      .insert({
        studio_id: studio.id,
        slug: input.slug,
        content_kind: input.contentKind,
        lifecycle_status: input.lifecycleStatus,
        visibility: input.lifecycleStatus === "draft" ? "private" : input.visibility,
        is_reported: false,
        current_metadata_revision: 1,
        display_name: metadata.displayName,
        short_description: metadata.shortDescription,
        description: metadata.description,
        genre_display: genreDisplay,
        min_players: metadata.minPlayers,
        max_players: metadata.maxPlayers,
        age_rating_authority: ageRatingAuthority.code,
        age_rating_value: metadata.ageRatingValue,
        min_age_years: metadata.minAgeYears,
        current_release_id: null,
        current_release_version: null,
        current_release_published_at: null,
        acquisition_url: null,
        created_at: now,
        updated_at: now
      })
      .select("*")
      .single();
    if (error) {
      throw problem(500, "title_create_failed", "Title creation failed.", error.message);
    }

    const title = data as unknown as TitleRow;
    const { error: metadataError } = await this.client.from("title_metadata_versions").insert({
      title_id: title.id,
      revision_number: 1,
      is_current: true,
      is_frozen: false,
      display_name: metadata.displayName,
      short_description: metadata.shortDescription,
      description: metadata.description,
      genre_display: genreDisplay,
      min_players: metadata.minPlayers,
      max_players: metadata.maxPlayers,
      age_rating_authority: ageRatingAuthority.code,
      age_rating_value: metadata.ageRatingValue,
      min_age_years: metadata.minAgeYears,
      created_at: now,
      updated_at: now
    });
    if (metadataError) {
      throw problem(500, "title_create_failed", "Title creation failed.", metadataError.message);
    }

    await this.replaceTitleMetadataVersionGenres(title.id, 1, metadata.genreSlugs);

    return {
      title: await this.getDeveloperTitleDetails(user.appUser.id, title.id)
    };
  }

  async getDeveloperTitle(token: string, titleId: string): Promise<DeveloperTitleResponse> {
    const user = await this.requireUser(token);
    return {
      title: await this.getDeveloperTitleDetails(user.appUser.id, titleId)
    };
  }

  async updateTitle(token: string, titleId: string, input: UpdateDeveloperTitleRequest): Promise<DeveloperTitleResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    this.validateUpdateDeveloperTitle(input);

    const existing = await this.findTitleByStudioAndSlug(title.studio_id, input.slug);
    if (existing && existing.id !== title.id) {
      throw problem(409, "title_slug_conflict", "Title already exists.", "The supplied title slug is already in use for this studio.");
    }

    const nextVisibility =
      input.lifecycleStatus === "draft" || input.lifecycleStatus === "archived" ? "private" : input.visibility;
    const { error } = await this.client
      .from("titles")
      .update({
        slug: input.slug,
        content_kind: input.contentKind,
        lifecycle_status: input.lifecycleStatus,
        visibility: nextVisibility,
        updated_at: new Date().toISOString()
      })
      .eq("id", title.id);
    if (error) {
      throw problem(500, "title_update_failed", "Title update failed.", error.message);
    }

    return {
      title: await this.getDeveloperTitleDetails(user.appUser.id, title.id)
    };
  }

  async upsertTitleMetadata(token: string, titleId: string, input: UpsertTitleMetadataRequest): Promise<DeveloperTitleResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    this.validateTitleMetadataInput(input);

    const currentVersion = await this.getCurrentTitleMetadataVersionRow(title.id);
    const now = new Date().toISOString();
    let nextRevisionNumber = currentVersion.revision_number;
    const genreRows = await this.requireGenres(input.genreSlugs);
    const genreDisplay = buildGenreDisplayFromRows(input.genreSlugs, genreRows);
    const ageRatingAuthority = await this.getAgeRatingAuthorityByCode(input.ageRatingAuthority);
    if (!ageRatingAuthority) {
      throw validationProblem({
        ageRatingAuthority: [`Unknown age rating authority: ${input.ageRatingAuthority}.`]
      });
    }

    if (currentVersion.is_frozen) {
      nextRevisionNumber = (await this.getHighestMetadataRevisionNumber(title.id)) + 1;

      const { error: demoteError } = await this.client
        .from("title_metadata_versions")
        .update({ is_current: false })
        .eq("title_id", title.id)
        .eq("is_current", true);
      if (demoteError) {
        throw problem(500, "title_metadata_update_failed", "Title metadata update failed.", demoteError.message);
      }

      const { error: insertError } = await this.client.from("title_metadata_versions").insert({
        title_id: title.id,
        revision_number: nextRevisionNumber,
        is_current: true,
        is_frozen: false,
        display_name: input.displayName,
        short_description: input.shortDescription,
        description: input.description,
        genre_display: genreDisplay,
        min_players: input.minPlayers,
        max_players: input.maxPlayers,
        age_rating_authority: ageRatingAuthority.code,
        age_rating_value: input.ageRatingValue,
        min_age_years: input.minAgeYears,
        created_at: now,
        updated_at: now
      });
      if (insertError) {
        throw problem(500, "title_metadata_update_failed", "Title metadata update failed.", insertError.message);
      }
      await this.replaceTitleMetadataVersionGenres(title.id, nextRevisionNumber, input.genreSlugs);
    } else {
      const { error: updateError } = await this.client
        .from("title_metadata_versions")
        .update({
          display_name: input.displayName,
          short_description: input.shortDescription,
          description: input.description,
          genre_display: genreDisplay,
          min_players: input.minPlayers,
          max_players: input.maxPlayers,
          age_rating_authority: ageRatingAuthority.code,
          age_rating_value: input.ageRatingValue,
          min_age_years: input.minAgeYears,
          updated_at: now
        })
        .eq("title_id", title.id)
        .eq("revision_number", currentVersion.revision_number);
      if (updateError) {
        throw problem(500, "title_metadata_update_failed", "Title metadata update failed.", updateError.message);
      }
      await this.replaceTitleMetadataVersionGenres(title.id, currentVersion.revision_number, input.genreSlugs);
    }

    const nextCurrentVersion = await this.getTitleMetadataVersionRow(title.id, nextRevisionNumber);
    await this.syncTitleFromMetadataVersion(title.id, nextCurrentVersion);
    return {
      title: await this.getDeveloperTitleDetails(user.appUser.id, title.id)
    };
  }

  async getTitleMetadataVersions(token: string, titleId: string): Promise<TitleMetadataVersionListResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    const versions = await this.getTitleMetadataVersionRows(title.id);
    const genreSlugsByRevision = await this.getGenreSlugsByMetadataVersion(title.id);

    return {
      metadataVersions: versions
        .sort((left, right) => right.revision_number - left.revision_number)
        .map((version) => mapTitleMetadataVersion(version, genreSlugsByRevision.get(version.revision_number) ?? []))
    };
  }

  async activateTitleMetadataVersion(token: string, titleId: string, revisionNumber: number): Promise<DeveloperTitleResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    const version = await this.getTitleMetadataVersionRow(title.id, revisionNumber);

    const { error: clearError } = await this.client.from("title_metadata_versions").update({ is_current: false }).eq("title_id", title.id);
    if (clearError) {
      throw problem(500, "title_metadata_activate_failed", "Title metadata activation failed.", clearError.message);
    }

    const { error: activateError } = await this.client
      .from("title_metadata_versions")
      .update({ is_current: true, updated_at: new Date().toISOString() })
      .eq("title_id", title.id)
      .eq("revision_number", revisionNumber);
    if (activateError) {
      throw problem(500, "title_metadata_activate_failed", "Title metadata activation failed.", activateError.message);
    }

    await this.syncTitleFromMetadataVersion(title.id, version);
    return {
      title: await this.getDeveloperTitleDetails(user.appUser.id, title.id)
    };
  }

  async getTitleMediaAssets(token: string, titleId: string): Promise<TitleMediaAssetListResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    return {
      mediaAssets: (await this.getTitleMediaAssetsForTitle(title.id)).map(mapTitleMediaAsset)
    };
  }

  async upsertTitleMediaAsset(
    token: string,
    titleId: string,
    mediaRole: string,
    input: UpsertTitleMediaAssetRequest
  ): Promise<TitleMediaAssetResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    this.validateTitleMediaRole(mediaRole);
    this.validateTitleMediaAssetInput(input);

    const { data, error } = await this.client
      .from("title_media_assets")
      .upsert(
        {
          title_id: title.id,
          media_role: mediaRole,
          source_url: input.sourceUrl,
          storage_path: null,
          alt_text: input.altText,
          mime_type: input.mimeType,
          width: input.width,
          height: input.height,
          updated_at: new Date().toISOString()
        },
        { onConflict: "title_id,media_role" }
      )
      .select("*")
      .single();
    if (error) {
      throw problem(500, "title_media_upsert_failed", "Title media update failed.", error.message);
    }

    return {
      mediaAsset: mapTitleMediaAsset(data as unknown as TitleMediaAssetRow)
    };
  }

  async uploadTitleMediaAsset(
    token: string,
    titleId: string,
    mediaRole: string,
    file: File | null,
    altText: string | null
  ): Promise<TitleMediaAssetResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    this.validateTitleMediaRole(mediaRole);
    const validatedFile = this.requireUploadFile(file);
    const studio = await this.getStudioById(title.studio_id);
    const extension = this.extensionForMimeType(validatedFile.type);
    const storagePath = `titles/${studio.slug}/${title.slug}/${mediaRole}${extension}`;
    const { error: uploadError } = await this.client.storage
      .from(this.context.supabaseMediaBucket)
      .upload(storagePath, validatedFile, { contentType: validatedFile.type, upsert: true });
    if (uploadError) {
      throw problem(500, "title_media_upload_failed", "Title media upload failed.", uploadError.message);
    }

    const publicUrl = this.client.storage.from(this.context.supabaseMediaBucket).getPublicUrl(storagePath).data.publicUrl;
    return this.upsertTitleMediaAsset(token, titleId, mediaRole, {
      sourceUrl: publicUrl,
      altText: altText?.trim() ? altText.trim() : null,
      mimeType: validatedFile.type,
      width: null,
      height: null
    });
  }

  async deleteTitleMediaAsset(token: string, titleId: string, mediaRole: string): Promise<void> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    this.validateTitleMediaRole(mediaRole);

    const { error } = await this.client
      .from("title_media_assets")
      .delete()
      .eq("title_id", title.id)
      .eq("media_role", mediaRole);
    if (error) {
      throw problem(500, "title_media_delete_failed", "Title media delete failed.", error.message);
    }
  }

  async getDeveloperTitleReports(token: string, titleId: string): Promise<TitleReportListResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    return {
      reports: await this.buildTitleReportSummaries(await this.getTitleReportsByTitleId(title.id))
    };
  }

  async getDeveloperTitleReport(token: string, titleId: string, reportId: string): Promise<TitleReportDetailResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    const report = await this.getTitleReportById(reportId);
    if (report.title_id !== title.id) {
      throw problem(404, "title_report_not_found", "Title report not found.", "The requested title report was not found.");
    }

    return {
      report: await this.buildTitleReportDetail(report)
    };
  }

  async addDeveloperTitleReportMessage(
    token: string,
    titleId: string,
    reportId: string,
    input: AddTitleReportMessageRequest
  ): Promise<TitleReportDetailResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    const report = await this.getTitleReportById(reportId);
    if (report.title_id !== title.id) {
      throw problem(404, "title_report_not_found", "Title report not found.", "The requested title report was not found.");
    }

    this.assertTitleReportOpenForMessaging(report);
    this.validateTitleReportMessage(input.message);

    const now = new Date().toISOString();
    const { error: messageError } = await this.client.from("title_report_messages").insert({
      report_id: report.id,
      author_user_id: user.appUser.id,
      author_role: "developer",
      audience: "all",
      message: input.message.trim(),
      created_at: now
    });
    if (messageError) {
      throw problem(500, "title_report_message_failed", "Title report message failed.", messageError.message);
    }

    const { error: reportError } = await this.client
      .from("title_reports")
      .update({
        status: "developer_responded",
        updated_at: now
      })
      .eq("id", report.id);
    if (reportError) {
      throw problem(500, "title_report_message_failed", "Title report message failed.", reportError.message);
    }

    await this.createDeveloperReportReplyNotifications(report, input.message.trim(), user.appUser);
    return {
      report: await this.buildTitleReportDetail(await this.getTitleReportById(report.id))
    };
  }

  async getTitleReleases(token: string, titleId: string): Promise<TitleReleaseListResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    return {
      releases: (await this.getTitleReleaseRows(title.id))
        .sort((left, right) => (right.published_at ?? right.created_at).localeCompare(left.published_at ?? left.created_at))
        .map(mapTitleRelease)
    };
  }

  async createTitleRelease(token: string, titleId: string, input: UpsertTitleReleaseRequest): Promise<TitleReleaseResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    this.validateTitleReleaseInput(input);
    await this.getTitleMetadataVersionRow(title.id, input.metadataRevisionNumber);
    const now = new Date().toISOString();

    const { data, error } = await this.client
      .from("title_releases")
      .insert({
        title_id: title.id,
        version: input.version,
        status: "draft",
        metadata_revision_number: input.metadataRevisionNumber,
        acquisition_url: input.acquisitionUrl?.trim() ? input.acquisitionUrl.trim() : null,
        is_current: false,
        published_at: null,
        created_at: now,
        updated_at: now
      })
      .select("*")
      .single();
    if (error) {
      throw problem(500, "title_release_create_failed", "Title release creation failed.", error.message);
    }

    return {
      release: mapTitleRelease(data as unknown as TitleReleaseRow)
    };
  }

  async updateTitleRelease(
    token: string,
    titleId: string,
    releaseId: string,
    input: UpsertTitleReleaseRequest
  ): Promise<TitleReleaseResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    this.validateTitleReleaseInput(input);
    await this.getTitleMetadataVersionRow(title.id, input.metadataRevisionNumber);
    const release = await this.requireTitleRelease(title.id, releaseId);
    if (release.status === "withdrawn") {
      throw problem(409, "title_release_withdrawn", "Release cannot be edited.", "Withdrawn releases cannot be updated.");
    }

    const now = new Date().toISOString();
    const { data, error } = await this.client
      .from("title_releases")
      .update({
        version: input.version,
        metadata_revision_number: input.metadataRevisionNumber,
        acquisition_url: input.acquisitionUrl?.trim() ? input.acquisitionUrl.trim() : null,
        updated_at: now
      })
      .eq("id", release.id)
      .select("*")
      .single();
    if (error) {
      throw problem(500, "title_release_update_failed", "Title release update failed.", error.message);
    }

    const updatedRelease = data as unknown as TitleReleaseRow;
    if (release.is_current) {
      await this.syncTitleProjectionFromRelease(title.id, updatedRelease);
    }

    return {
      release: mapTitleRelease(updatedRelease)
    };
  }

  async publishTitleRelease(token: string, titleId: string, releaseId: string): Promise<TitleReleaseResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    const release = await this.requireTitleRelease(title.id, releaseId);
    if (release.status !== "draft") {
      throw problem(409, "title_release_not_draft", "Release cannot be published.", "Only draft releases can be published.");
    }
    if (!release.acquisition_url?.trim()) {
      throw problem(409, "title_release_acquisition_required", "Release cannot be published.", "A release acquisition URL is required before publishing.");
    }

    const now = new Date().toISOString();
    const { data, error } = await this.client
      .from("title_releases")
      .update({
        status: "published",
        published_at: now,
        updated_at: now
      })
      .eq("id", release.id)
      .select("*")
      .single();
    if (error) {
      throw problem(500, "title_release_publish_failed", "Title release publish failed.", error.message);
    }

    const { error: metadataError } = await this.client
      .from("title_metadata_versions")
      .update({ is_frozen: true, updated_at: now })
      .eq("title_id", title.id)
      .eq("revision_number", release.metadata_revision_number);
    if (metadataError) {
      throw problem(500, "title_release_publish_failed", "Title release publish failed.", metadataError.message);
    }

    return {
      release: mapTitleRelease(data as unknown as TitleReleaseRow)
    };
  }

  async activateTitleRelease(token: string, titleId: string, releaseId: string): Promise<DeveloperTitleResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    const release = await this.requireTitleRelease(title.id, releaseId);
    if (release.status !== "published") {
      throw problem(409, "title_release_not_published", "Release cannot be activated.", "Only published releases can be activated.");
    }

    const now = new Date().toISOString();
    const { error: clearError } = await this.client.from("title_releases").update({ is_current: false }).eq("title_id", title.id);
    if (clearError) {
      throw problem(500, "title_release_activate_failed", "Title release activate failed.", clearError.message);
    }

    const { error: activateError } = await this.client
      .from("title_releases")
      .update({ is_current: true, updated_at: now })
      .eq("id", release.id);
    if (activateError) {
      throw problem(500, "title_release_activate_failed", "Title release activate failed.", activateError.message);
    }

    const { error: titleUpdateError } = await this.client
      .from("titles")
      .update({
        current_release_id: release.id,
        current_release_version: release.version,
        current_release_published_at: release.published_at,
        acquisition_url: release.acquisition_url,
        updated_at: now
      })
      .eq("id", title.id);
    if (titleUpdateError) {
      throw problem(500, "title_release_activate_failed", "Title release activate failed.", titleUpdateError.message);
    }

    return {
      title: await this.getDeveloperTitleDetails(user.appUser.id, title.id)
    };
  }

  async withdrawTitleRelease(token: string, titleId: string, releaseId: string): Promise<TitleReleaseResponse> {
    const user = await this.requireUser(token);
    const title = await this.requireDeveloperTitleAccess(user.appUser.id, titleId);
    const release = await this.requireTitleRelease(title.id, releaseId);
    if (release.status === "withdrawn") {
      return {
        release: mapTitleRelease(release)
      };
    }

    const now = new Date().toISOString();
    const { data, error } = await this.client
      .from("title_releases")
      .update({
        status: "withdrawn",
        is_current: false,
        updated_at: now
      })
      .eq("id", release.id)
      .select("*")
      .single();
    if (error) {
      throw problem(500, "title_release_withdraw_failed", "Title release withdraw failed.", error.message);
    }

    if (title.current_release_id === release.id) {
      const { error: titleUpdateError } = await this.client
        .from("titles")
        .update({
          current_release_id: null,
          current_release_version: null,
          current_release_published_at: null,
          acquisition_url: null,
          updated_at: now
        })
        .eq("id", title.id);
      if (titleUpdateError) {
        throw problem(500, "title_release_withdraw_failed", "Title release withdraw failed.", titleUpdateError.message);
      }
    }

    return {
      release: mapTitleRelease(data as unknown as TitleReleaseRow)
    };
  }

  async listCatalogTitles(query: CatalogTitleListQuery): Promise<CatalogTitleListResponse> {
    const allTitles = await this.getTitles();
    const allStudios = await this.getStudiosByIds(allTitles.map((title) => title.studio_id));
    const studiosById = new Map(allStudios.map((studio) => [studio.id, studio]));
    const mediaByTitleId = await this.getTitleMediaByTitleIds(allTitles.map((title) => title.id));

    const pageNumber = query.pageNumber ?? 1;
    const pageSize = query.pageSize ?? 12;
    const validatedSort = query.sort ?? "title-asc";
    const allowedSorts = new Set([
      "title-asc",
      "title-desc",
      "studio-asc",
      "studio-desc",
      "genre-asc",
      "players-asc",
      "players-desc",
      "age-asc",
      "age-desc"
    ] satisfies Array<NonNullable<CatalogTitleListQuery["sort"]>>);
    const studioSlugs = new Set(normalizeCatalogFilterValues(query.studioSlug));
    const selectedGenres = new Set(normalizeCatalogFilterValues(query.genre).map((candidate) => normalizeGenreSlug(candidate)).filter(Boolean));
    const normalizedSearch = query.search?.trim().toLowerCase() ?? "";
    const validationErrors: Record<string, string[]> = {};
    if (!allowedSorts.has(validatedSort)) {
      validationErrors.sort = ["Sort must be one of: title-asc, title-desc, studio-asc, studio-desc, genre-asc, players-asc, players-desc, age-asc, age-desc."];
    }
    if (pageNumber < 1) {
      validationErrors.pageNumber = ["Page number must be at least 1."];
    }
    if (pageSize < 1 || pageSize > 48) {
      validationErrors.pageSize = ["Page size must be between 1 and 48."];
    }
    if (query.minPlayers !== undefined && (!Number.isInteger(query.minPlayers) || query.minPlayers < 1)) {
      validationErrors.minPlayers = ["Minimum players must be an integer greater than or equal to 1."];
    }
    if (query.maxPlayers !== undefined && (!Number.isInteger(query.maxPlayers) || query.maxPlayers < 1)) {
      validationErrors.maxPlayers = ["Maximum players must be an integer greater than or equal to 1."];
    }
    if (
      query.minPlayers !== undefined &&
      query.maxPlayers !== undefined &&
      Number.isInteger(query.minPlayers) &&
      Number.isInteger(query.maxPlayers) &&
      query.minPlayers > query.maxPlayers
    ) {
      validationErrors.playerRange = ["Minimum players cannot be greater than maximum players."];
    }
    if (Object.keys(validationErrors).length > 0) {
      throw validationProblem(validationErrors);
    }

    const matchingTitles = allTitles
      .filter(isPublicCatalogTitle)
      .filter((title) => !query.contentKind || title.content_kind === query.contentKind)
      .filter((title) => {
        const studio = studiosById.get(title.studio_id);
        if (!studio) {
          return false;
        }
        if (studioSlugs.size === 0) {
          return true;
        }

        return studioSlugs.has(studio.slug);
      })
      .filter((title) => {
        if (selectedGenres.size === 0) {
          return true;
        }

        return parseCatalogGenreTags(title.genre_display).some((genreTag) => selectedGenres.has(normalizeGenreSlug(genreTag)));
      })
      .filter((title) => {
        if (query.minPlayers !== undefined && title.max_players < query.minPlayers) {
          return false;
        }
        if (query.maxPlayers !== undefined && title.min_players > query.maxPlayers) {
          return false;
        }

        return true;
      })
      .filter((title) => {
        if (!normalizedSearch) {
          return true;
        }

        const studio = studiosById.get(title.studio_id);
        return [title.display_name, title.short_description, title.genre_display, studio?.slug, studio?.display_name]
          .filter(Boolean)
          .join(" ")
          .toLowerCase()
          .includes(normalizedSearch);
      })
      .sort((left, right) => {
        const leftStudio = studiosById.get(left.studio_id);
        const rightStudio = studiosById.get(right.studio_id);
        const byTitle = left.display_name.localeCompare(right.display_name);
        switch (validatedSort) {
          case "title-desc":
            return right.display_name.localeCompare(left.display_name);
          case "studio-asc":
            return (
              (leftStudio?.display_name ?? "").localeCompare(rightStudio?.display_name ?? "") ||
              byTitle
            );
          case "studio-desc":
            return (
              (rightStudio?.display_name ?? "").localeCompare(leftStudio?.display_name ?? "") ||
              byTitle
            );
          case "genre-asc":
            return left.genre_display.localeCompare(right.genre_display) || byTitle;
          case "players-asc":
            return left.max_players - right.max_players || left.min_players - right.min_players || byTitle;
          case "players-desc":
            return right.max_players - left.max_players || right.min_players - left.min_players || byTitle;
          case "age-asc":
            return left.min_age_years - right.min_age_years || byTitle;
          case "age-desc":
            return right.min_age_years - left.min_age_years || byTitle;
          default:
            return byTitle;
        }
      });

    const totalCount = matchingTitles.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    const sliceStart = (pageNumber - 1) * pageSize;
    const pagedTitles = matchingTitles.slice(sliceStart, sliceStart + pageSize);

    return {
      titles: pagedTitles.map((title) => {
        const studio = studiosById.get(title.studio_id);
        if (!studio) {
          throw problem(500, "catalog_join_failed", "Catalog projection failed.", `Studio ${title.studio_id} was not found.`);
        }

        return buildCatalogSummary(title, studio, mediaByTitleId.get(title.id) ?? []);
      }),
      paging: {
        pageNumber,
        pageSize,
        totalCount,
        totalPages,
        hasPreviousPage: pageNumber > 1,
        hasNextPage: pageNumber < totalPages
      } satisfies CatalogPaging
    };
  }

  async getCatalogTitle(studioSlug: string, titleSlug: string): Promise<CatalogTitleResponse> {
    const studio = await this.getStudioBySlug(studioSlug);
    const title = await this.getTitleByStudioAndSlug(studio.id, titleSlug);
    if (!isPublicCatalogDetail(title)) {
      throw problem(404, "catalog_title_not_found", "Catalog title not found", "The requested title was not found.");
    }

    const mediaByTitleId = await this.getTitleMediaByTitleIds([title.id]);
    return {
      title: buildCatalogDetail(title, studio, mediaByTitleId.get(title.id) ?? [])
    };
  }

  private async requireUser(token: string): Promise<AuthenticatedUser> {
    const bearerToken = token.trim();
    if (!bearerToken) {
      throw problem(401, "unauthorized", "Unauthorized", "A bearer token is required.");
    }

    const { data, error } = await this.client.auth.getUser(bearerToken);
    if (error || !data.user?.id) {
      throw problem(401, "unauthorized", "Unauthorized", "The supplied bearer token is invalid or expired.");
    }

    const appUser = await this.findOrCreateProjectedUser(data.user);
    const roles = await this.ensureDefaultPlatformRoles(appUser.id);
    return { appUser, roles };
  }

  private async findOrCreateProjectedUser(authUser: SupabaseAuthUser): Promise<AppUserRow> {
    const { data: appUserRows, error: appUserError } = await this.client
      .from("app_users")
      .select("*")
      .eq("auth_user_id", authUser.id)
      .limit(1);
    if (appUserError) {
      throw problem(500, "identity_projection_failed", "Identity lookup failed.", appUserError.message);
    }

    const existing = (appUserRows as AppUserRow[])[0];
    if (existing) {
      return existing;
    }

    const metadata = (authUser.user_metadata ?? {}) as Record<string, unknown>;
    const email = typeof authUser.email === "string" && authUser.email.trim().length > 0 ? authUser.email.trim().toLowerCase() : null;
    const firstName = typeof metadata.firstName === "string" && metadata.firstName.trim().length > 0 ? metadata.firstName.trim() : null;
    const lastName = typeof metadata.lastName === "string" && metadata.lastName.trim().length > 0 ? metadata.lastName.trim() : null;
    const metadataDisplayName =
      (typeof metadata.displayName === "string" && metadata.displayName.trim().length > 0 ? metadata.displayName.trim() : null) ??
      (typeof metadata.full_name === "string" && metadata.full_name.trim().length > 0 ? metadata.full_name.trim() : null);
    const displayName = metadataDisplayName ?? ([firstName, lastName].filter(Boolean).join(" ").trim() || null);
    const avatarUrl =
      (typeof metadata.avatarUrl === "string" && metadata.avatarUrl.trim().length > 0 ? metadata.avatarUrl.trim() : null) ??
      (typeof metadata.avatarDataUrl === "string" && metadata.avatarDataUrl.trim().length > 0 ? metadata.avatarDataUrl.trim() : null);
    const userName = await this.reserveProjectedUserName(
      typeof metadata.userName === "string" && metadata.userName.trim().length > 0
        ? metadata.userName
        : email?.split("@")[0] ?? displayName ?? "player"
    );
    const provider =
      typeof authUser.app_metadata?.provider === "string"
        ? authUser.app_metadata.provider
        : Array.isArray(authUser.identities) && typeof authUser.identities[0]?.provider === "string"
          ? authUser.identities[0].provider
          : "email";
    const now = new Date().toISOString();
    const { data: inserted, error: insertError } = await this.client
      .from("app_users")
      .insert({
        auth_user_id: authUser.id,
        user_name: userName,
        display_name: displayName,
        first_name: firstName,
        last_name: lastName,
        email,
        email_verified: Boolean(authUser.email_confirmed_at),
        identity_provider: provider,
        avatar_url: avatarUrl,
        avatar_storage_path: null,
        updated_at: now,
      })
      .select("*")
      .single();
    if (insertError) {
      throw problem(500, "identity_projection_create_failed", "Identity projection failed.", insertError.message);
    }

    return inserted as unknown as AppUserRow;
  }

  private async reserveProjectedUserName(requestedValue: string): Promise<string> {
    const normalizedBase = sanitizeUserName(requestedValue) || "player";
    for (let suffix = 0; suffix < 1000; suffix += 1) {
      const candidate = suffix === 0 ? normalizedBase : `${normalizedBase}-${suffix + 1}`;
      const { data, error } = await this.client.from("app_users").select("id").eq("user_name", candidate).limit(1);
      if (error) {
        throw problem(500, "identity_projection_failed", "Identity lookup failed.", error.message);
      }
      if ((data as Array<{ id: string }>).length === 0) {
        return candidate;
      }
    }

    throw problem(500, "identity_projection_failed", "Identity projection failed.", "Unable to reserve a unique user name.");
  }

  private async ensureDefaultPlatformRoles(userId: string): Promise<PlatformRole[]> {
    const roles = await this.getRolesForUser(userId);
    if (roles.includes("player")) {
      return roles;
    }

    const { error } = await this.client.from("app_user_roles").insert({
      user_id: userId,
      role: "player",
    });
    if (error) {
      throw problem(500, "role_lookup_failed", "Role lookup failed.", error.message);
    }

    return this.getRolesForUser(userId);
  }

  private async listUserIdsByRole(role: PlatformRole): Promise<string[]> {
    const { data, error } = await this.client.from("app_user_roles").select("user_id").eq("role", role);
    if (error) {
      throw problem(500, "role_lookup_failed", "Role lookup failed.", error.message);
    }

    return (data as AppUserRoleRow[]).map((row) => row.user_id);
  }

  private async getRolesForUser(userId: string): Promise<PlatformRole[]> {
    const { data, error } = await this.client.from("app_user_roles").select("user_id, role").eq("user_id", userId);
    if (error) {
      throw problem(500, "role_lookup_failed", "Role lookup failed.", error.message);
    }

    return sortRoles((data as AppUserRoleRow[]).map((row) => row.role));
  }

  private async getUsersByIds(userIds: string[]): Promise<AppUserRow[]> {
    if (userIds.length === 0) {
      return [];
    }

    const { data, error } = await this.client.from("app_users").select("*").in("id", userIds);
    if (error) {
      throw problem(500, "user_lookup_failed", "User lookup failed.", error.message);
    }

    return data as AppUserRow[];
  }

  private async findBoardProfileByUserId(userId: string): Promise<BoardProfileRow | null> {
    const { data, error } = await this.client.from("user_board_profiles").select("*").eq("user_id", userId).limit(1);
    if (error) {
      throw problem(500, "board_profile_lookup_failed", "Board profile lookup failed.", error.message);
    }

    return ((data as BoardProfileRow[])[0] ?? null);
  }

  private assertModerator(roles: PlatformRole[]): void {
    if (!roles.some((role) => role === "moderator" || role === "admin" || role === "super_admin")) {
      throw problem(
        403,
        "moderator_access_required",
        "Moderator access is required.",
        "Only moderators and above can view developer moderation tools."
      );
    }
  }

  private canCreateStudio(roles: PlatformRole[]): boolean {
    return roles.some((role) => role === "developer" || role === "verified_developer" || role === "admin" || role === "super_admin");
  }

  private validateStudioMutation(input: StudioMutationRequest): void {
    const errors: Record<string, string[]> = {};

    if (!validateStudioSlug(input.slug)) {
      errors.slug = ["Slug must contain only lowercase letters, numbers, and single hyphen separators."];
    }
    if (!input.displayName || input.displayName.trim().length === 0) {
      errors.displayName = ["Display name is required."];
    }
    if (input.logoUrl && !isAbsoluteUrl(input.logoUrl)) {
      errors.logoUrl = ["Logo URL must be an absolute URI."];
    }
    if (input.bannerUrl && !isAbsoluteUrl(input.bannerUrl)) {
      errors.bannerUrl = ["Banner URL must be an absolute URI."];
    }

    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }
  }

  private validateStudioLinkMutation(input: StudioLinkMutationRequest): void {
    const errors: Record<string, string[]> = {};
    if (!input.label || input.label.trim().length === 0) {
      errors.label = ["Label is required."];
    }
    if (!input.url || !isAbsoluteUrl(input.url)) {
      errors.url = ["URL must be an absolute URI."];
    }

    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }
  }

  private requireUploadFile(file: File | null): File {
    if (!file) {
      throw validationProblem({
        media: ["A media file is required."]
      });
    }
    if (!acceptedImageMimeTypes.has(file.type)) {
      throw validationProblem({
        media: ["Media image format must be JPEG, PNG, WEBP, GIF, or SVG."]
      });
    }
    if (file.size > maxUploadBytes) {
      throw validationProblem({
        media: ["Media image size must be 25 MB or less."]
      });
    }

    return file;
  }

  private extensionForMimeType(mimeType: string): string {
    switch (mimeType) {
      case "image/png":
        return ".png";
      case "image/jpeg":
        return ".jpg";
      case "image/webp":
        return ".webp";
      case "image/gif":
        return ".gif";
      case "image/svg+xml":
        return ".svg";
      default:
        return "";
    }
  }

  private async getDeveloperTitleDetails(userId: string, titleId: string): Promise<DeveloperTitle> {
    const title = await this.requireDeveloperTitleAccess(userId, titleId);
    const studio = await this.getStudioById(title.studio_id);
    const mediaRows = await this.getTitleMediaAssetsForTitle(title.id);
    const genreSlugs = await this.getGenreSlugsForMetadataVersion(title.id, title.current_metadata_revision);
    return buildDeveloperTitle(title, studio, mediaRows, genreSlugs);
  }

  private async requireDeveloperTitleAccess(userId: string, titleId: string): Promise<TitleRow> {
    const title = await this.getTitleById(titleId);
    await this.requireStudioAccess(userId, title.studio_id);
    return title;
  }

  private validateCreateDeveloperTitle(input: CreateDeveloperTitleRequest): void {
    const errors: Record<string, string[]> = {};
    if (!validateTitleSlug(input.slug)) {
      errors.slug = ["Slug must contain only lowercase letters, numbers, and single hyphen separators."];
    }
    if (input.contentKind !== "game" && input.contentKind !== "app") {
      errors.contentKind = ["Content kind must be either game or app."];
    }
    if (!["draft", "testing", "published", "archived"].includes(input.lifecycleStatus)) {
      errors.lifecycleStatus = ["Lifecycle status is invalid."];
    }
    if (!["private", "unlisted", "listed"].includes(input.visibility)) {
      errors.visibility = ["Visibility is invalid."];
    }
    if (input.lifecycleStatus === "draft" && input.visibility !== "private") {
      errors.visibility = ["Draft titles must remain private."];
    }
    Object.assign(errors, this.collectTitleMetadataErrors(input.metadata));
    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }
  }

  private validateUpdateDeveloperTitle(input: UpdateDeveloperTitleRequest): void {
    const errors: Record<string, string[]> = {};
    if (!validateTitleSlug(input.slug)) {
      errors.slug = ["Slug must contain only lowercase letters, numbers, and single hyphen separators."];
    }
    if (input.contentKind !== "game" && input.contentKind !== "app") {
      errors.contentKind = ["Content kind must be either game or app."];
    }
    if (!["draft", "testing", "published", "archived"].includes(input.lifecycleStatus)) {
      errors.lifecycleStatus = ["Lifecycle status is invalid."];
    }
    if (!["private", "unlisted", "listed"].includes(input.visibility)) {
      errors.visibility = ["Visibility is invalid."];
    }
    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }
  }

  private validateTitleMetadataInput(input: UpsertTitleMetadataRequest): void {
    const errors = this.collectTitleMetadataErrors(input);
    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }
  }

  private collectTitleMetadataErrors(input: UpsertTitleMetadataRequest): Record<string, string[]> {
    const errors: Record<string, string[]> = {};
    if (!input.displayName?.trim()) {
      errors.displayName = ["Display name is required."];
    }
    if (!input.shortDescription?.trim()) {
      errors.shortDescription = ["Short description is required."];
    }
    if (!input.description?.trim()) {
      errors.description = ["Description is required."];
    }
    if (!Array.isArray(input.genreSlugs) || input.genreSlugs.length === 0) {
      errors.genreSlugs = ["At least one genre is required."];
    } else if (input.genreSlugs.some((genreSlug) => !normalizeGenreSlug(genreSlug))) {
      errors.genreSlugs = ["Genres must use valid slugs."];
    }
    if (input.minPlayers < 1) {
      errors.minPlayers = ["Minimum players must be at least 1."];
    }
    if (input.maxPlayers < input.minPlayers) {
      errors.maxPlayers = ["Maximum players must be greater than or equal to minimum players."];
    }
    if (!input.ageRatingAuthority?.trim()) {
      errors.ageRatingAuthority = ["Age rating authority is required."];
    }
    if (!input.ageRatingValue?.trim()) {
      errors.ageRatingValue = ["Age rating value is required."];
    }
    if (input.minAgeYears < 0) {
      errors.minAgeYears = ["Minimum age must be zero or greater."];
    }
    return errors;
  }

  private validateTitleMediaRole(mediaRole: string): void {
    if (!["card", "hero", "logo"].includes(mediaRole)) {
      throw validationProblem({
        mediaRole: ["Media role must be one of: card, hero, logo."]
      });
    }
  }

  private validateTitleMediaAssetInput(input: UpsertTitleMediaAssetRequest): void {
    const errors: Record<string, string[]> = {};
    if (!input.sourceUrl?.trim() || !isAbsoluteUrl(input.sourceUrl)) {
      errors.sourceUrl = ["Media source URL must be an absolute URI."];
    }
    if ((input.width === null) !== (input.height === null)) {
      errors.dimensions = ["Width and height must both be supplied together."];
    }
    if ((input.width ?? 1) <= 0 || (input.height ?? 1) <= 0) {
      errors.dimensions = ["Width and height must be positive integers when supplied."];
    }
    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }
  }

  private validateTitleReleaseInput(input: UpsertTitleReleaseRequest): void {
    const errors: Record<string, string[]> = {};
    if (!input.version?.trim()) {
      errors.version = ["Release version is required."];
    }
    if (input.metadataRevisionNumber < 1) {
      errors.metadataRevisionNumber = ["Metadata revision number must be at least 1."];
    }
    if (input.acquisitionUrl?.trim() && !isAbsoluteUrl(input.acquisitionUrl)) {
      errors.acquisitionUrl = ["Acquisition URL must be an absolute URI when supplied."];
    }
    if (Object.keys(errors).length > 0) {
      throw validationProblem(errors);
    }
  }

  private async findTitleByStudioAndSlug(studioId: string, slug: string): Promise<TitleRow | null> {
    const { data, error } = await this.client
      .from("titles")
      .select("*")
      .eq("studio_id", studioId)
      .eq("slug", slug)
      .limit(1);
    if (error) {
      throw problem(500, "title_lookup_failed", "Title lookup failed.", error.message);
    }

    return (data as TitleRow[])[0] ?? null;
  }

  private async getTitlesByStudioId(studioId: string): Promise<TitleRow[]> {
    const { data, error } = await this.client.from("titles").select("*").eq("studio_id", studioId);
    if (error) {
      throw problem(500, "title_lookup_failed", "Title lookup failed.", error.message);
    }

    return data as TitleRow[];
  }

  private async getTitleMetadataVersionRows(titleId: string): Promise<TitleMetadataVersionRow[]> {
    const { data, error } = await this.client
      .from("title_metadata_versions")
      .select("*")
      .eq("title_id", titleId)
      .order("revision_number", { ascending: false });
    if (error) {
      throw problem(500, "title_metadata_lookup_failed", "Title metadata lookup failed.", error.message);
    }

    return data as TitleMetadataVersionRow[];
  }

  private async getGenres(): Promise<GenreRow[]> {
    const { data, error } = await this.client.from("genres").select("slug, display_name").order("display_name", { ascending: true });
    if (error) {
      throw problem(500, "genre_lookup_failed", "Genre lookup failed.", error.message);
    }

    return data as GenreRow[];
  }

  private async getAgeRatingAuthorities(): Promise<AgeRatingAuthorityRow[]> {
    const { data, error } = await this.client.from("age_rating_authorities").select("code, display_name").order("display_name", { ascending: true });
    if (error) {
      throw problem(500, "age_rating_authority_lookup_failed", "Age rating authority lookup failed.", error.message);
    }

    return data as AgeRatingAuthorityRow[];
  }

  private async getAgeRatingAuthorityByCode(authorityCode: string): Promise<AgeRatingAuthorityRow | null> {
    const normalizedCode = authorityCode.trim().toUpperCase();
    const { data, error } = await this.client.from("age_rating_authorities").select("code, display_name").eq("code", normalizedCode).limit(1);
    if (error) {
      throw problem(500, "age_rating_authority_lookup_failed", "Age rating authority lookup failed.", error.message);
    }

    return (data as AgeRatingAuthorityRow[])[0] ?? null;
  }

  private async getGenresBySlugs(genreSlugs: readonly string[]): Promise<GenreRow[]> {
    if (genreSlugs.length === 0) {
      return [];
    }

    const normalizedSlugs = Array.from(new Set(genreSlugs.map((genreSlug) => normalizeGenreSlug(genreSlug)).filter(Boolean)));
    const { data, error } = await this.client.from("genres").select("slug, display_name").in("slug", normalizedSlugs);
    if (error) {
      throw problem(500, "genre_lookup_failed", "Genre lookup failed.", error.message);
    }

    return data as GenreRow[];
  }

  private async requireGenres(genreSlugs: readonly string[]): Promise<GenreRow[]> {
    const requestedGenres = Array.from(
      new Map(
        genreSlugs
          .map((genreInput) => {
            const normalizedSlug = normalizeGenreSlug(genreInput);
            return normalizedSlug ? [normalizedSlug, genreInput.trim()] : null;
          })
          .filter(Boolean) as Array<[string, string]>
      ).entries()
    );
    const normalizedSlugs = requestedGenres.map(([normalizedSlug]) => normalizedSlug);
    let genres = await this.getGenresBySlugs(normalizedSlugs);
    const returnedSlugs = new Set(genres.map((genre) => genre.slug));
    const missingGenres = requestedGenres.filter(([normalizedSlug]) => !returnedSlugs.has(normalizedSlug));

    if (missingGenres.length > 0) {
      const { error } = await this.client.from("genres").upsert(
        missingGenres.map(([slug, sourceValue]) => ({
          slug,
          display_name: buildGenreDisplayNameFromInput(sourceValue) || slug
        })),
        { onConflict: "slug", ignoreDuplicates: true }
      );
      if (error) {
        throw problem(500, "genre_upsert_failed", "Genre upsert failed.", error.message);
      }

      genres = await this.getGenresBySlugs(normalizedSlugs);
    }

    return genres;
  }

  private async getTitleMetadataVersionGenreRows(titleId: string): Promise<TitleMetadataVersionGenreRow[]> {
    const { data, error } = await this.client
      .from("title_metadata_version_genres")
      .select("title_id, revision_number, genre_slug, display_order")
      .eq("title_id", titleId)
      .order("revision_number", { ascending: true })
      .order("display_order", { ascending: true });
    if (error) {
      throw problem(500, "title_metadata_lookup_failed", "Title metadata lookup failed.", error.message);
    }

    return data as TitleMetadataVersionGenreRow[];
  }

  private async getGenreSlugsByMetadataVersion(titleId: string): Promise<Map<number, string[]>> {
    const rows = await this.getTitleMetadataVersionGenreRows(titleId);
    const genreSlugsByRevision = new Map<number, string[]>();
    for (const row of rows) {
      const existing = genreSlugsByRevision.get(row.revision_number) ?? [];
      existing.push(row.genre_slug);
      genreSlugsByRevision.set(row.revision_number, existing);
    }

    return genreSlugsByRevision;
  }

  private async getGenreSlugsForMetadataVersion(titleId: string, revisionNumber: number): Promise<string[]> {
    return (await this.getGenreSlugsByMetadataVersion(titleId)).get(revisionNumber) ?? [];
  }

  private async replaceTitleMetadataVersionGenres(titleId: string, revisionNumber: number, genreSlugs: readonly string[]): Promise<void> {
    const normalizedSlugs = Array.from(new Set(genreSlugs.map((genreSlug) => normalizeGenreSlug(genreSlug)).filter(Boolean)));
    const { error: deleteError } = await this.client
      .from("title_metadata_version_genres")
      .delete()
      .eq("title_id", titleId)
      .eq("revision_number", revisionNumber);
    if (deleteError) {
      throw problem(500, "title_metadata_update_failed", "Title metadata update failed.", deleteError.message);
    }

    if (normalizedSlugs.length === 0) {
      return;
    }

    const { error: insertError } = await this.client.from("title_metadata_version_genres").insert(
      normalizedSlugs.map((genreSlug, index) => ({
        title_id: titleId,
        revision_number: revisionNumber,
        genre_slug: genreSlug,
        display_order: index
      }))
    );
    if (insertError) {
      throw problem(500, "title_metadata_update_failed", "Title metadata update failed.", insertError.message);
    }
  }

  private async getHighestMetadataRevisionNumber(titleId: string): Promise<number> {
    const rows = await this.getTitleMetadataVersionRows(titleId);
    return rows.reduce((max, row) => Math.max(max, row.revision_number), 0);
  }

  private async getCurrentTitleMetadataVersionRow(titleId: string): Promise<TitleMetadataVersionRow> {
    const { data, error } = await this.client
      .from("title_metadata_versions")
      .select("*")
      .eq("title_id", titleId)
      .eq("is_current", true)
      .limit(1);
    if (error) {
      throw problem(500, "title_metadata_lookup_failed", "Title metadata lookup failed.", error.message);
    }

    const row = (data as TitleMetadataVersionRow[])[0];
    if (!row) {
      throw problem(404, "title_metadata_not_found", "Title metadata was not found.", "The title does not have a current metadata revision.");
    }

    return row;
  }

  private async getTitleMetadataVersionRow(titleId: string, revisionNumber: number): Promise<TitleMetadataVersionRow> {
    const { data, error } = await this.client
      .from("title_metadata_versions")
      .select("*")
      .eq("title_id", titleId)
      .eq("revision_number", revisionNumber)
      .limit(1);
    if (error) {
      throw problem(500, "title_metadata_lookup_failed", "Title metadata lookup failed.", error.message);
    }

    const row = (data as TitleMetadataVersionRow[])[0];
    if (!row) {
      throw problem(404, "title_metadata_not_found", "Title metadata was not found.", "The requested metadata revision was not found.");
    }

    return row;
  }

  private async syncTitleFromMetadataVersion(titleId: string, version: TitleMetadataVersionRow): Promise<void> {
    const { error } = await this.client
      .from("titles")
      .update({
        current_metadata_revision: version.revision_number,
        display_name: version.display_name,
        short_description: version.short_description,
        description: version.description,
        genre_display: version.genre_display,
        min_players: version.min_players,
        max_players: version.max_players,
        age_rating_authority: version.age_rating_authority,
        age_rating_value: version.age_rating_value,
        min_age_years: version.min_age_years,
        updated_at: new Date().toISOString()
      })
      .eq("id", titleId);
    if (error) {
      throw problem(500, "title_sync_failed", "Title projection sync failed.", error.message);
    }
  }

  private async getTitleMediaAssetsForTitle(titleId: string): Promise<TitleMediaAssetRow[]> {
    const { data, error } = await this.client
      .from("title_media_assets")
      .select("*")
      .eq("title_id", titleId)
      .order("media_role", { ascending: true });
    if (error) {
      throw problem(500, "catalog_media_lookup_failed", "Catalog media lookup failed.", error.message);
    }

    return data as TitleMediaAssetRow[];
  }

  private async getTitleReportsByTitleId(titleId: string): Promise<TitleReportRow[]> {
    const { data, error } = await this.client
      .from("title_reports")
      .select("*")
      .eq("title_id", titleId)
      .order("updated_at", { ascending: false });
    if (error) {
      throw problem(500, "title_report_lookup_failed", "Title report lookup failed.", error.message);
    }

    return data as TitleReportRow[];
  }

  private async getTitleReleaseRows(titleId: string): Promise<TitleReleaseRow[]> {
    const { data, error } = await this.client
      .from("title_releases")
      .select("*")
      .eq("title_id", titleId)
      .order("created_at", { ascending: false });
    if (error) {
      throw problem(500, "title_release_lookup_failed", "Title release lookup failed.", error.message);
    }

    return data as TitleReleaseRow[];
  }

  private async requireTitleRelease(titleId: string, releaseId: string): Promise<TitleReleaseRow> {
    const { data, error } = await this.client
      .from("title_releases")
      .select("*")
      .eq("id", releaseId)
      .eq("title_id", titleId)
      .limit(1);
    if (error) {
      throw problem(500, "title_release_lookup_failed", "Title release lookup failed.", error.message);
    }

    const release = (data as TitleReleaseRow[])[0];
    if (!release) {
      throw problem(404, "title_release_not_found", "Title release not found.", "The requested title release was not found.");
    }

    return release;
  }

  private async syncTitleProjectionFromRelease(titleId: string, release: TitleReleaseRow): Promise<void> {
    const updates: Partial<TitleRow> & { updated_at: string } = {
      updated_at: new Date().toISOString()
    };

    if (release.is_current) {
      updates.current_release_id = release.id;
      updates.current_release_version = release.version;
      updates.current_release_published_at = release.published_at;
      updates.acquisition_url = release.acquisition_url;
    }

    const { error } = await this.client.from("titles").update(updates).eq("id", titleId);
    if (error) {
      throw problem(500, "title_sync_failed", "Title projection sync failed.", error.message);
    }
  }

  private async findDeveloperByIdentifier(identifier: string): Promise<AppUserRow> {
    const { data, error } = await this.client.from("app_users").select("*").or(`user_name.eq.${identifier},auth_user_id.eq.${identifier}`);
    if (error) {
      throw problem(500, "developer_lookup_failed", "Developer lookup failed.", error.message);
    }

    const developer = (data as AppUserRow[])[0];
    if (!developer) {
      throw problem(404, "developer_not_found", "Developer was not found.", "The target developer was not found.");
    }

    const roles = await this.getRolesForUser(developer.id);
    if (!roles.includes("developer")) {
      throw problem(404, "developer_not_found", "Developer was not found.", "The target developer was not found.");
    }

    return developer;
  }

  private async getStudios(): Promise<StudioRow[]> {
    const { data, error } = await this.client.from("studios").select("*");
    if (error) {
      throw problem(500, "studio_lookup_failed", "Studio lookup failed.", error.message);
    }

    return data as StudioRow[];
  }

  private async getStudiosByIds(studioIds: string[]): Promise<StudioRow[]> {
    if (studioIds.length === 0) {
      return [];
    }

    const { data, error } = await this.client.from("studios").select("*").in("id", studioIds);
    if (error) {
      throw problem(500, "studio_lookup_failed", "Studio lookup failed.", error.message);
    }

    return data as StudioRow[];
  }

  private async getStudioBySlug(slug: string): Promise<StudioRow> {
    const studio = await this.findStudioBySlug(slug);
    if (!studio) {
      throw problem(404, "studio_not_found", "Studio not found", "The requested studio was not found.");
    }

    return studio;
  }

  private async getStudioById(studioId: string): Promise<StudioRow> {
    const { data, error } = await this.client.from("studios").select("*").eq("id", studioId).limit(1);
    if (error) {
      throw problem(500, "studio_lookup_failed", "Studio lookup failed.", error.message);
    }

    const studio = (data as StudioRow[])[0];
    if (!studio) {
      throw problem(404, "studio_not_found", "Studio not found", "The requested studio was not found.");
    }

    return studio;
  }

  private async findStudioBySlug(slug: string): Promise<StudioRow | null> {
    const { data, error } = await this.client.from("studios").select("*").eq("slug", slug).limit(1);
    if (error) {
      throw problem(500, "studio_lookup_failed", "Studio lookup failed.", error.message);
    }

    return (data as StudioRow[])[0] ?? null;
  }

  private async getStudioMembershipsForUser(userId: string): Promise<StudioMembershipRow[]> {
    const { data, error } = await this.client.from("studio_memberships").select("*").eq("user_id", userId);
    if (error) {
      throw problem(500, "studio_membership_lookup_failed", "Studio membership lookup failed.", error.message);
    }

    return data as StudioMembershipRow[];
  }

  private async requireStudioAccess(userId: string, studioId: string): Promise<StudioMembershipRow> {
    const { data, error } = await this.client
      .from("studio_memberships")
      .select("*")
      .eq("studio_id", studioId)
      .eq("user_id", userId)
      .limit(1);
    if (error) {
      throw problem(500, "studio_membership_lookup_failed", "Studio membership lookup failed.", error.message);
    }

    const membership = (data as StudioMembershipRow[])[0];
    if (!membership) {
      throw problem(403, "studio_access_required", "Studio access is required.", "Caller does not have permission to manage this studio.");
    }

    return membership;
  }

  private async requireStudioOwner(userId: string, studioId: string): Promise<void> {
    const membership = await this.requireStudioAccess(userId, studioId);
    if (membership.role !== "owner") {
      throw problem(403, "studio_owner_required", "Studio owner access is required.", "Caller does not have permission to delete this studio.");
    }
  }

  private async getStudioLinksByStudioIds(studioIds: string[]): Promise<Map<string, StudioLinkRow[]>> {
    const linksByStudioId = new Map<string, StudioLinkRow[]>();
    if (studioIds.length === 0) {
      return linksByStudioId;
    }

    const { data, error } = await this.client.from("studio_links").select("*").in("studio_id", studioIds);
    if (error) {
      throw problem(500, "studio_link_lookup_failed", "Studio link lookup failed.", error.message);
    }

    for (const row of data as StudioLinkRow[]) {
      const current = linksByStudioId.get(row.studio_id) ?? [];
      current.push(row);
      linksByStudioId.set(row.studio_id, current);
    }

    for (const links of linksByStudioId.values()) {
      links.sort((left, right) => left.label.localeCompare(right.label));
    }

    return linksByStudioId;
  }

  private buildStudioSummary(studio: StudioRow, links: StudioLinkRow[]): Studio {
    return {
      id: studio.id,
      slug: studio.slug,
      displayName: studio.display_name,
      description: studio.description,
      logoUrl: studio.logo_url,
      bannerUrl: studio.banner_url,
      links: links.map(mapStudioLink),
      createdAt: studio.created_at,
      updatedAt: studio.updated_at
    };
  }

  private buildStudio(studio: StudioRow, links: StudioLinkRow[]): Studio {
    return this.buildStudioSummary(studio, links);
  }

  private async requireStudioLink(studioId: string, linkId: string): Promise<void> {
    const { data, error } = await this.client.from("studio_links").select("id").eq("id", linkId).eq("studio_id", studioId).limit(1);
    if (error) {
      throw problem(500, "studio_link_lookup_failed", "Studio link lookup failed.", error.message);
    }

    if ((data as Array<{ id: string }>).length === 0) {
      throw problem(404, "studio_link_not_found", "Studio or link not found.", "Studio or link not found.");
    }
  }

  private validateTitleReportMessage(message: string): void {
    if (!message || message.trim().length === 0) {
      throw validationProblem({
        message: ["Message is required."]
      });
    }
  }

  private assertTitleReportOpenForMessaging(report: TitleReportRow): void {
    if (report.status === "validated" || report.status === "invalidated") {
      throw problem(409, "title_report_closed", "Title report is closed.", "Closed title reports do not accept additional messages.");
    }
  }

  private async createNotifications(
    userIds: string[],
    notification: Omit<UserNotificationRow, "id" | "user_id" | "is_read" | "read_at" | "created_at" | "updated_at">
  ): Promise<void> {
    const recipients = Array.from(new Set(userIds.filter(Boolean)));
    if (recipients.length === 0) {
      return;
    }

    const now = new Date().toISOString();
    const rows = recipients.map((userId) => ({
      user_id: userId,
      category: notification.category,
      title: notification.title,
      body: notification.body,
      action_url: notification.action_url,
      is_read: false,
      read_at: null,
      created_at: now,
      updated_at: now
    }));
    const { error } = await this.client.from("user_notifications").insert(rows);
    if (error) {
      throw problem(500, "notification_create_failed", "Notification creation failed.", error.message);
    }
  }

  private async listModeratorNotificationUserIds(): Promise<string[]> {
    const { data, error } = await this.client
      .from("app_user_roles")
      .select("user_id, role")
      .in("role", ["moderator", "admin", "super_admin"]);
    if (error) {
      throw problem(500, "notification_recipient_lookup_failed", "Notification recipient lookup failed.", error.message);
    }

    return Array.from(new Set((data as AppUserRoleRow[]).map((row) => row.user_id)));
  }

  private async listStudioReportManagerUserIds(studioId: string): Promise<string[]> {
    const { data, error } = await this.client
      .from("studio_memberships")
      .select("*")
      .eq("studio_id", studioId);
    if (error) {
      throw problem(500, "notification_recipient_lookup_failed", "Notification recipient lookup failed.", error.message);
    }

    return Array.from(new Set(
      (data as StudioMembershipRow[])
        .filter((membership) => membership.role === "owner" || membership.role === "admin" || membership.role === "editor")
        .map((membership) => membership.user_id)
    ));
  }

  private buildPlayerReportActionUrl(reportId: string): string {
    return `/player?workflow=reported-titles&reportId=${encodeURIComponent(reportId)}`;
  }

  private buildDeveloperReportActionUrl(titleId: string, reportId: string): string {
    return `/develop?domain=titles&workflow=titles-reports&titleId=${encodeURIComponent(titleId)}&reportId=${encodeURIComponent(reportId)}`;
  }

  private buildModerationReportActionUrl(reportId: string): string {
    return `/moderate?workflow=reports-review&reportId=${encodeURIComponent(reportId)}`;
  }

  private async createTitleReportNotifications(report: TitleReportRow, title: TitleRow, reporter: AppUserRow): Promise<void> {
    const [moderators, studioManagers] = await Promise.all([
      this.listModeratorNotificationUserIds(),
      this.listStudioReportManagerUserIds(title.studio_id)
    ]);
    const reporterName = reporter.display_name ?? reporter.user_name ?? reporter.email ?? "A player";
    const shortReason = report.reason.length > 140 ? `${report.reason.slice(0, 137)}...` : report.reason;

    await Promise.all([
      this.createNotifications(moderators, {
        category: "title_report",
        title: "New title report submitted",
        body: `${reporterName} reported ${title.display_name}. ${shortReason}`,
        action_url: this.buildModerationReportActionUrl(report.id)
      }),
      this.createNotifications(studioManagers.filter((userId) => userId !== reporter.id), {
        category: "title_report",
        title: "A player reported your title",
        body: `${reporterName} reported ${title.display_name}. Review the report thread and respond from Develop.`,
        action_url: this.buildDeveloperReportActionUrl(title.id, report.id)
      })
    ]);
  }

  private async createPlayerReportReplyNotifications(report: TitleReportRow, message: string, author: AppUserRow): Promise<void> {
    const title = await this.getTitleById(report.title_id);
    const [moderators, studioManagers] = await Promise.all([
      this.listModeratorNotificationUserIds(),
      this.listStudioReportManagerUserIds(title.studio_id)
    ]);
    const authorName = author.display_name ?? author.user_name ?? author.email ?? "The player";
    const brief = message.length > 140 ? `${message.slice(0, 137)}...` : message;

    await Promise.all([
      this.createNotifications(moderators, {
        category: "title_report",
        title: "Player replied to a title report",
        body: `${authorName} replied about ${title.display_name}. ${brief}`,
        action_url: this.buildModerationReportActionUrl(report.id)
      }),
      this.createNotifications(studioManagers.filter((userId) => userId !== author.id), {
        category: "title_report",
        title: "Player follow-up received",
        body: `${authorName} replied about ${title.display_name}. Review the latest message in Develop.`,
        action_url: this.buildDeveloperReportActionUrl(title.id, report.id)
      })
    ]);
  }

  private async createDeveloperReportReplyNotifications(report: TitleReportRow, message: string, author: AppUserRow): Promise<void> {
    const title = await this.getTitleById(report.title_id);
    const moderators = await this.listModeratorNotificationUserIds();
    const authorName = author.display_name ?? author.user_name ?? author.email ?? "The developer";
    const brief = message.length > 140 ? `${message.slice(0, 137)}...` : message;

    await Promise.all([
      this.createNotifications([report.reporter_user_id], {
        category: "title_report",
        title: "Developer replied to your report",
        body: `${authorName} responded about ${title.display_name}. Open the report thread in Play.`,
        action_url: this.buildPlayerReportActionUrl(report.id)
      }),
      this.createNotifications(moderators.filter((userId) => userId !== author.id), {
        category: "title_report",
        title: "Developer replied to a title report",
        body: `${authorName} replied about ${title.display_name}. ${brief}`,
        action_url: this.buildModerationReportActionUrl(report.id)
      })
    ]);
  }

  private async createModerationReportMessageNotifications(
    report: TitleReportRow,
    message: string,
    recipientRole: "player" | "developer",
    author: AppUserRow
  ): Promise<void> {
    const title = await this.getTitleById(report.title_id);
    const authorName = author.display_name ?? author.user_name ?? author.email ?? "A moderator";
    const brief = message.length > 140 ? `${message.slice(0, 137)}...` : message;
    const recipients = recipientRole === "player"
      ? [report.reporter_user_id]
      : (await this.listStudioReportManagerUserIds(title.studio_id)).filter((userId) => userId !== author.id);

    await this.createNotifications(recipients, {
      category: "title_report",
      title: recipientRole === "player" ? "Moderator follow-up on your report" : "Moderator follow-up for your title",
      body: `${authorName} sent an update about ${title.display_name}. ${brief}`,
      action_url: recipientRole === "player"
        ? this.buildPlayerReportActionUrl(report.id)
        : this.buildDeveloperReportActionUrl(title.id, report.id)
    });
  }

  private async createModerationDecisionNotifications(
    report: TitleReportRow,
    validated: boolean,
    note: string | null,
    author: AppUserRow
  ): Promise<void> {
    const title = await this.getTitleById(report.title_id);
    const studioManagers = await this.listStudioReportManagerUserIds(title.studio_id);
    const authorName = author.display_name ?? author.user_name ?? author.email ?? "A moderator";
    const statusLabel = validated ? "validated" : "invalidated";
    const noteSuffix = note ? ` ${note}` : "";

    await Promise.all([
      this.createNotifications([report.reporter_user_id], {
        category: "title_report",
        title: `Your report was ${statusLabel}`,
        body: `${authorName} ${statusLabel} your report for ${title.display_name}.${noteSuffix}`,
        action_url: this.buildPlayerReportActionUrl(report.id)
      }),
      this.createNotifications(studioManagers.filter((userId) => userId !== author.id), {
        category: "title_report",
        title: `Report ${statusLabel} by moderation`,
        body: `${authorName} ${statusLabel} the report for ${title.display_name}.${noteSuffix}`,
        action_url: this.buildDeveloperReportActionUrl(title.id, report.id)
      })
    ]);
  }

  private async getPlayerCollectionTitles(userId: string, kind: "library" | "wishlist"): Promise<CatalogTitleSummary[]> {
    const rows = kind === "library"
      ? await this.getPlayerLibraryRows(userId)
      : await this.getPlayerWishlistRows(userId);
    const titleIds = rows.map((row) => row.title_id);
    if (titleIds.length === 0) {
      return [];
    }

    const titles = await this.getTitlesByIds(titleIds);
    const studios = await this.getStudiosByIds(titles.map((title) => title.studio_id));
    const mediaByTitleId = await this.getTitleMediaByTitleIds(titleIds);
    const studioById = new Map(studios.map((studio) => [studio.id, studio]));
    const titleById = new Map(titles.map((title) => [title.id, title]));

    return rows
      .map((row) => {
        const title = titleById.get(row.title_id);
        if (!title || !isPublicCatalogDetail(title)) {
          return null;
        }

        const studio = studioById.get(title.studio_id);
        if (!studio) {
          return null;
        }

        return buildCatalogSummary(title, studio, mediaByTitleId.get(title.id) ?? []);
      })
      .filter((title): title is CatalogTitleSummary => Boolean(title));
  }

  private async setPlayerCollectionState(
    userId: string,
    titleId: string,
    included: boolean,
    kind: "library" | "wishlist"
  ): Promise<PlayerCollectionMutationResponse> {
    const title = await this.getTitleById(titleId);
    if (!isPublicCatalogDetail(title)) {
      throw problem(404, "catalog_title_not_found", "Catalog title not found", "The requested title was not found.");
    }

    const tableName = kind === "library" ? "player_library_titles" : "player_wishlist_titles";
    const existingRows = kind === "library"
      ? await this.getPlayerLibraryRows(userId, [title.id])
      : await this.getPlayerWishlistRows(userId, [title.id]);
    const alreadyIncluded = existingRows.length > 0;

    if (included && !alreadyIncluded) {
      const { error } = await this.client.from(tableName).insert({
        user_id: userId,
        title_id: title.id,
        created_at: new Date().toISOString()
      });
      if (error) {
        throw problem(500, "player_collection_mutation_failed", "Player collection update failed.", error.message);
      }
    }

    if (!included && alreadyIncluded) {
      const { error } = await this.client.from(tableName).delete().eq("user_id", userId).eq("title_id", title.id);
      if (error) {
        throw problem(500, "player_collection_mutation_failed", "Player collection update failed.", error.message);
      }
    }

    return mapPlayerCollectionMutation(title.id, included, alreadyIncluded === included);
  }

  private async getPlayerLibraryRows(userId: string, titleIds?: string[]): Promise<PlayerLibraryRow[]> {
    let query = this.client.from("player_library_titles").select("*").eq("user_id", userId);
    if (titleIds && titleIds.length > 0) {
      query = query.in("title_id", titleIds);
    }
    const { data, error } = await query.order("created_at", { ascending: false });
    if (error) {
      throw problem(500, "player_library_lookup_failed", "Player library lookup failed.", error.message);
    }

    return data as PlayerLibraryRow[];
  }

  private async getPlayerWishlistRows(userId: string, titleIds?: string[]): Promise<PlayerWishlistRow[]> {
    let query = this.client.from("player_wishlist_titles").select("*").eq("user_id", userId);
    if (titleIds && titleIds.length > 0) {
      query = query.in("title_id", titleIds);
    }
    const { data, error } = await query.order("created_at", { ascending: false });
    if (error) {
      throw problem(500, "player_wishlist_lookup_failed", "Player wishlist lookup failed.", error.message);
    }

    return data as PlayerWishlistRow[];
  }

  private async getTitleReports(): Promise<TitleReportRow[]> {
    const { data, error } = await this.client.from("title_reports").select("*").order("updated_at", { ascending: false });
    if (error) {
      throw problem(500, "title_report_lookup_failed", "Title report lookup failed.", error.message);
    }

    return data as TitleReportRow[];
  }

  private async getTitleReportsByReporter(reporterUserId: string): Promise<TitleReportRow[]> {
    const { data, error } = await this.client
      .from("title_reports")
      .select("*")
      .eq("reporter_user_id", reporterUserId)
      .order("updated_at", { ascending: false });
    if (error) {
      throw problem(500, "title_report_lookup_failed", "Title report lookup failed.", error.message);
    }

    return data as TitleReportRow[];
  }

  private async findOpenTitleReportForPlayer(reporterUserId: string, titleId: string): Promise<TitleReportRow | null> {
    const { data, error } = await this.client
      .from("title_reports")
      .select("*")
      .eq("reporter_user_id", reporterUserId)
      .eq("title_id", titleId)
      .not("status", "in", "(validated,invalidated)")
      .order("updated_at", { ascending: false })
      .limit(1);
    if (error) {
      throw problem(500, "title_report_lookup_failed", "Title report lookup failed.", error.message);
    }

    return (data as TitleReportRow[])[0] ?? null;
  }

  private async getTitleReportById(reportId: string): Promise<TitleReportRow> {
    const { data, error } = await this.client.from("title_reports").select("*").eq("id", reportId).limit(1);
    if (error) {
      throw problem(500, "title_report_lookup_failed", "Title report lookup failed.", error.message);
    }

    const report = (data as TitleReportRow[])[0];
    if (!report) {
      throw problem(404, "title_report_not_found", "Title report not found.", "The requested title report was not found.");
    }

    return report;
  }

  private async getTitleReportMessages(reportId: string): Promise<TitleReportMessageRow[]> {
    const { data, error } = await this.client
      .from("title_report_messages")
      .select("*")
      .eq("report_id", reportId)
      .order("created_at", { ascending: true });
    if (error) {
      throw problem(500, "title_report_message_lookup_failed", "Title report message lookup failed.", error.message);
    }

    return data as TitleReportMessageRow[];
  }

  private async getTitles(): Promise<TitleRow[]> {
    const { data, error } = await this.client.from("titles").select("*");
    if (error) {
      throw problem(500, "catalog_lookup_failed", "Catalog lookup failed.", error.message);
    }

    return data as TitleRow[];
  }

  private async getTitlesByIds(titleIds: string[]): Promise<TitleRow[]> {
    if (titleIds.length === 0) {
      return [];
    }

    const { data, error } = await this.client.from("titles").select("*").in("id", titleIds);
    if (error) {
      throw problem(500, "catalog_lookup_failed", "Catalog lookup failed.", error.message);
    }

    return data as TitleRow[];
  }

  private async getTitleById(titleId: string): Promise<TitleRow> {
    const { data, error } = await this.client.from("titles").select("*").eq("id", titleId).limit(1);
    if (error) {
      throw problem(500, "catalog_lookup_failed", "Catalog lookup failed.", error.message);
    }

    const title = (data as TitleRow[])[0];
    if (!title) {
      throw problem(404, "catalog_title_not_found", "Catalog title not found", "The requested title was not found.");
    }

    return title;
  }

  private async getTitleByStudioAndSlug(studioId: string, titleSlug: string): Promise<TitleRow> {
    const { data, error } = await this.client
      .from("titles")
      .select("*")
      .eq("studio_id", studioId)
      .eq("slug", titleSlug)
      .limit(1);
    if (error) {
      throw problem(500, "catalog_lookup_failed", "Catalog lookup failed.", error.message);
    }

    const title = (data as TitleRow[])[0];
    if (!title) {
      throw problem(404, "catalog_title_not_found", "Catalog title not found", "The requested title was not found.");
    }

    return title;
  }

  private async getTitleMediaByTitleIds(titleIds: string[]): Promise<Map<string, TitleMediaAssetRow[]>> {
    const mediaByTitleId = new Map<string, TitleMediaAssetRow[]>();
    if (titleIds.length === 0) {
      return mediaByTitleId;
    }

    const { data, error } = await this.client.from("title_media_assets").select("*").in("title_id", titleIds);
    if (error) {
      throw problem(500, "catalog_media_lookup_failed", "Catalog media lookup failed.", error.message);
    }

    for (const row of data as TitleMediaAssetRow[]) {
      const current = mediaByTitleId.get(row.title_id) ?? [];
      current.push(row);
      mediaByTitleId.set(row.title_id, current);
    }

    for (const rows of mediaByTitleId.values()) {
      rows.sort((left, right) => left.media_role.localeCompare(right.media_role));
    }

    return mediaByTitleId;
  }

  private async buildTitleReportSummaries(reports: TitleReportRow[]): Promise<TitleReportSummary[]> {
    if (reports.length === 0) {
      return [];
    }

    const titles = await this.getTitlesByIds(reports.map((report) => report.title_id));
    const titleById = new Map(titles.map((title) => [title.id, title]));
    const studios = await this.getStudiosByIds(titles.map((title) => title.studio_id));
    const studioById = new Map(studios.map((studio) => [studio.id, studio]));
    const users = await this.getUsersByIds(
      Array.from(new Set(
        reports.flatMap((report) => [report.reporter_user_id, report.resolved_by_user_id].filter((value): value is string => Boolean(value)))
      ))
    );
    const userById = new Map(users.map((row) => [row.id, row]));
    const messageCounts = await this.getTitleReportMessageCounts(reports.map((report) => report.id));

    const summaries = reports
      .map((report): TitleReportSummary | null => {
        const title = titleById.get(report.title_id);
        const studio = title ? studioById.get(title.studio_id) : null;
        const reporter = userById.get(report.reporter_user_id);
        if (!title || !studio || !reporter) {
          return null;
        }

        return {
          id: report.id,
          titleId: title.id,
          studioId: studio.id,
          studioSlug: studio.slug,
          studioDisplayName: studio.display_name,
          titleSlug: title.slug,
          titleDisplayName: title.display_name,
          titleShortDescription: title.short_description,
          genreDisplay: title.genre_display,
          currentMetadataRevision: title.current_metadata_revision,
          reporterSubject: reporter.auth_user_id,
          reporterUserName: reporter.user_name,
          reporterDisplayName: reporter.display_name,
          reporterEmail: reporter.email,
          status: report.status,
          reason: report.reason,
          createdAt: report.created_at,
          updatedAt: report.updated_at,
          resolvedAt: report.resolved_at,
          messageCount: messageCounts.get(report.id) ?? 0
        } satisfies TitleReportSummary;
      })
      .filter((report): report is TitleReportSummary => report !== null);

    return summaries.sort((left, right) => right.updatedAt.localeCompare(left.updatedAt));
  }

  private async buildTitleReportDetail(report: TitleReportRow): Promise<TitleReportDetail> {
    const summary = (await this.buildTitleReportSummaries([report]))[0];
    if (!summary) {
      throw problem(404, "title_report_not_found", "Title report not found.", "The requested title report was not found.");
    }

    const messages = await this.getTitleReportMessages(report.id);
    const actorUserIds = Array.from(
      new Set(messages.map((message) => message.author_user_id).concat(report.resolved_by_user_id ? [report.resolved_by_user_id] : []))
    );
    const users = await this.getUsersByIds(actorUserIds);
    const userById = new Map(users.map((user) => [user.id, user]));
    const resolvedByUser = report.resolved_by_user_id ? userById.get(report.resolved_by_user_id) ?? null : null;

    return {
      report: summary,
      resolutionNote: report.resolution_note,
      resolvedBy: resolvedByUser ? this.buildTitleReportActor(resolvedByUser) : null,
      messages: messages
        .map((message): TitleReportMessage | null => {
          const author = userById.get(message.author_user_id);
          if (!author) {
            return null;
          }

          return {
            id: message.id,
            authorSubject: author.auth_user_id,
            authorUserName: author.user_name,
            authorDisplayName: author.display_name,
            authorEmail: author.email,
            authorRole: message.author_role,
            audience: message.audience,
            message: message.message,
            createdAt: message.created_at
          } satisfies TitleReportMessage;
        })
        .filter((message): message is TitleReportMessage => message !== null)
    };
  }

  private buildTitleReportActor(user: AppUserRow): TitleReportActor {
    return {
      subject: user.auth_user_id,
      userName: user.user_name,
      displayName: user.display_name,
      email: user.email
    };
  }

  private async getTitleReportMessageCounts(reportIds: string[]): Promise<Map<string, number>> {
    const counts = new Map<string, number>();
    if (reportIds.length === 0) {
      return counts;
    }

    const { data, error } = await this.client
      .from("title_report_messages")
      .select("report_id")
      .in("report_id", reportIds);
    if (error) {
      throw problem(500, "title_report_message_lookup_failed", "Title report message lookup failed.", error.message);
    }

    for (const row of data as Array<{ report_id: string }>) {
      counts.set(row.report_id, (counts.get(row.report_id) ?? 0) + 1);
    }

    return counts;
  }

  private async getMarketingContactByNormalizedEmail(normalizedEmail: string): Promise<MarketingContactRow | null> {
    const { data, error } = await this.client
      .from("marketing_contacts")
      .select("*")
      .eq("normalized_email", normalizedEmail)
      .limit(1);
    if (error) {
      throw problem(500, "marketing_contact_lookup_failed", "Marketing signup lookup failed.", error.message);
    }

    return ((data as MarketingContactRow[])[0] ?? null);
  }

  private async getMarketingContactRecordByNormalizedEmail(normalizedEmail: string): Promise<MarketingContactRecord | null> {
    const contact = await this.getMarketingContactByNormalizedEmail(normalizedEmail);
    if (!contact) {
      return null;
    }

    return {
      contact,
      roleInterests: await this.listMarketingContactRoleInterests(contact.id)
    };
  }

  private async listMarketingContactRoleInterests(contactId: string): Promise<MarketingContactRoleInterest[]> {
    const { data, error } = await this.client
      .from("marketing_contact_role_interests")
      .select("role")
      .eq("marketing_contact_id", contactId);
    if (error) {
      throw problem(500, "marketing_contact_lookup_failed", "Marketing signup lookup failed.", error.message);
    }

    const roleSet = new Set<MarketingContactRoleInterest>();
    for (const row of data as Array<Pick<MarketingContactRoleInterestRow, "role">>) {
      if (row.role === "player" || row.role === "developer") {
        roleSet.add(row.role);
      }
    }

    return marketingRoleInterestOrder.filter((role) => roleSet.has(role));
  }

  private async replaceMarketingContactRoleInterests(
    contactId: string,
    roleInterests: readonly MarketingContactRoleInterest[]
  ): Promise<void> {
    const { error: deleteError } = await this.client
      .from("marketing_contact_role_interests")
      .delete()
      .eq("marketing_contact_id", contactId);
    if (deleteError) {
      throw problem(500, "marketing_signup_failed", "Marketing signup could not be saved.", deleteError.message);
    }

    if (roleInterests.length === 0) {
      return;
    }

    const { error: insertError } = await this.client
      .from("marketing_contact_role_interests")
      .insert(roleInterests.map((role) => ({ marketing_contact_id: contactId, role })));
    if (insertError) {
      throw problem(500, "marketing_signup_failed", "Marketing signup could not be saved.", insertError.message);
    }
  }

  private async sendSupportIssueEmail(input: {
    subject: string;
    text: string;
    replyToEmail: string | null;
    replyToName: string | null;
  }): Promise<void> {
    if (this.context.envName === "local" && this.context.mailpitBaseUrl) {
      await this.sendSupportIssueEmailViaMailpit(input);
      return;
    }

    if (this.context.brevoApiKey) {
      await this.sendSupportIssueEmailViaBrevo(input);
      return;
    }

    if (this.context.mailpitBaseUrl) {
      await this.sendSupportIssueEmailViaMailpit(input);
      return;
    }

    throw problem(
      503,
      "support_issue_transport_unavailable",
      "Support issue reporting is temporarily unavailable.",
      "No support issue delivery transport has been configured for this environment."
    );
  }

  private async sendSupportIssueEmailViaMailpit(input: {
    subject: string;
    text: string;
    replyToEmail: string | null;
    replyToName: string | null;
  }): Promise<void> {
    const response = await fetch(`${this.context.mailpitBaseUrl!.replace(/\/$/, "")}/api/v1/send`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        accept: "application/json",
      },
      body: JSON.stringify({
        From: {
          Email: this.context.supportReportSenderEmail,
          Name: this.context.supportReportSenderName,
        },
        To: [
          {
            Email: this.context.supportReportRecipient,
            Name: "Board Enthusiasts Support",
          },
        ],
        ReplyTo: input.replyToEmail
          ? [
              {
                Email: input.replyToEmail,
                Name: input.replyToName ?? undefined,
              },
            ]
          : undefined,
        Subject: input.subject,
        Text: input.text,
        Tags: ["support", "bug-report", "landing-page"],
      }),
    });

    if (!response.ok) {
      const detail = (await response.text()).trim();
      throw problem(
        502,
        "support_issue_delivery_failed",
        "Support issue reporting is temporarily unavailable.",
        detail || `Mailpit returned ${response.status}.`
      );
    }
  }

  private async sendSupportIssueEmailViaBrevo(input: {
    subject: string;
    text: string;
    replyToEmail: string | null;
    replyToName: string | null;
  }): Promise<void> {
    const response = await fetch("https://api.brevo.com/v3/smtp/email", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        accept: "application/json",
        "api-key": this.context.brevoApiKey!,
      },
      body: JSON.stringify({
        sender: {
          email: this.context.supportReportSenderEmail,
          name: this.context.supportReportSenderName,
        },
        to: [
          {
            email: this.context.supportReportRecipient,
            name: "Board Enthusiasts Support",
          },
        ],
        replyTo: input.replyToEmail
          ? {
              email: input.replyToEmail,
              name: input.replyToName ?? undefined,
            }
          : undefined,
        subject: input.subject,
        textContent: input.text,
        tags: ["support", "bug-report", "landing-page"],
      }),
    });

    if (!response.ok) {
      const detail = (await response.text()).trim();
      throw problem(
        502,
        "support_issue_delivery_failed",
        "Support issue reporting is temporarily unavailable.",
        detail || `Brevo returned ${response.status}.`
      );
    }
  }

  private async verifyTurnstile(token: string | null): Promise<void> {
    const secretKey = this.context.turnstileSecretKey;
    if (!secretKey) {
      if (this.context.envName === "local") {
        return;
      }

      throw problem(
        503,
        "turnstile_not_configured",
        "Signup verification is temporarily unavailable.",
        "Turnstile verification has not been configured for this environment."
      );
    }

    const trimmedToken = (token ?? "").trim();
    if (!trimmedToken) {
      throw validationProblem({
        turnstileToken: ["Signup verification is required."]
      });
    }

    const payload = new URLSearchParams({
      secret: secretKey,
      response: trimmedToken
    });

    const response = await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", {
      method: "POST",
      headers: {
        "content-type": "application/x-www-form-urlencoded"
      },
      body: payload.toString()
    });

    if (!response.ok) {
      throw problem(503, "turnstile_verification_failed", "Signup verification is temporarily unavailable.", "Turnstile verification request failed.");
    }

    const verification = (await response.json()) as { success?: boolean };
    if (!verification.success) {
      throw validationProblem({
        turnstileToken: ["Signup verification failed. Please try again."]
      });
    }
  }

  private async syncMarketingContactToBrevo(
    contact: MarketingContactRow,
    roleInterests: readonly MarketingContactRoleInterest[]
  ): Promise<void> {
    if (!this.context.brevoApiKey || !this.context.brevoSignupsListId) {
      await this.updateMarketingContactBrevoState(contact.id, {
        brevo_sync_state: "skipped",
        brevo_last_error: null,
        brevo_synced_at: null
      });
      return;
    }

    try {
      const response = await fetch("https://api.brevo.com/v3/contacts", {
        method: "POST",
        headers: {
          "api-key": this.context.brevoApiKey,
          accept: "application/json",
          "content-type": "application/json"
        },
        body: JSON.stringify({
          email: contact.email,
          attributes: {
            FIRSTNAME: contact.first_name ?? undefined,
            SOURCE: contact.source,
            BE_LIFECYCLE_STATUS: contact.lifecycle_status,
            BE_ROLE_INTEREST: roleInterests.length > 0 ? [...roleInterests].sort().join(",") : "none"
          },
          listIds: [this.context.brevoSignupsListId],
          updateEnabled: true,
          emailBlacklisted: false
        })
      });

      if (!response.ok) {
        const detail = await response.text();
        await this.updateMarketingContactBrevoState(contact.id, {
          brevo_sync_state: "failed",
          brevo_last_error: detail.trim().slice(0, 1000) || `Brevo returned ${response.status}.`,
          brevo_synced_at: null
        });
        return;
      }

      const payload = (await response.json().catch(() => ({}))) as { id?: number | string };
      await this.updateMarketingContactBrevoState(contact.id, {
        brevo_contact_id: payload.id ? String(payload.id) : contact.brevo_contact_id,
        brevo_sync_state: "synced",
        brevo_last_error: null,
        brevo_synced_at: new Date().toISOString()
      });
    } catch (error) {
      await this.updateMarketingContactBrevoState(contact.id, {
        brevo_sync_state: "failed",
        brevo_last_error: (error instanceof Error ? error.message : String(error)).slice(0, 1000),
        brevo_synced_at: null
      });
    }
  }

  private async updateMarketingContactBrevoState(
    contactId: string,
    update: Partial<Pick<MarketingContactRow, "brevo_contact_id" | "brevo_sync_state" | "brevo_last_error" | "brevo_synced_at">>
  ): Promise<void> {
    const { error } = await this.client
      .from("marketing_contacts")
      .update({
        ...update,
        updated_at: new Date().toISOString()
      })
      .eq("id", contactId);

    if (error) {
      throw problem(500, "marketing_contact_update_failed", "Marketing signup update failed.", error.message);
    }
  }
}
