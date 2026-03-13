import type { MarketingSignupRequest, UpdateUserProfileRequest, UpsertBoardProfileRequest } from "@board-enthusiasts/migration-contract";
import { empty, json, ApiError, corsHeaders, problem, validationProblem } from "./http";
import { Env, WorkerAppService, type WorkerAppContext } from "./service-boundary";

const localMarketingOrigins = [
  "http://localhost:5173",
  "http://127.0.0.1:5173",
  "http://localhost:4173",
  "http://127.0.0.1:4173"
];

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

function getBearerToken(request: Request): string {
  const header = request.headers.get("authorization") ?? request.headers.get("Authorization") ?? "";
  return header.startsWith("Bearer ") ? header.slice("Bearer ".length).trim() : "";
}

async function readJson<T>(request: Request): Promise<T> {
  try {
    return (await request.json()) as T;
  } catch {
    throw validationProblem({
      body: ["Request body must be valid JSON."]
    });
  }
}

function normalizeOrigin(value: string | null | undefined): string | null {
  if (!value || !value.trim()) {
    return null;
  }

  try {
    return new URL(value).origin;
  } catch {
    return null;
  }
}

function getAllowedMarketingOrigins(context: WorkerAppContext): readonly string[] {
  if (context.allowedWebOrigins.length > 0) {
    return context.allowedWebOrigins;
  }

  if (context.envName === "local") {
    return localMarketingOrigins;
  }

  throw problem(
    503,
    "marketing_origin_not_configured",
    "Marketing signup is temporarily unavailable.",
    "No approved web origins have been configured for marketing signup submissions."
  );
}

function assertAllowedMarketingOrigin(request: Request, context: WorkerAppContext): void {
  const requestOrigin = normalizeOrigin(request.headers.get("origin"));
  const allowedOrigins = getAllowedMarketingOrigins(context);

  if (!requestOrigin) {
    throw problem(
      403,
      "marketing_origin_required",
      "Origin header is required.",
      "Marketing signups are only accepted from approved Board Enthusiasts web origins."
    );
  }

  if (!allowedOrigins.includes(requestOrigin)) {
    throw problem(
      403,
      "marketing_origin_forbidden",
      "Origin is not allowed.",
      "Marketing signups are only accepted from approved Board Enthusiasts web origins."
    );
  }
}

function assertAllowedSupportIssueOrigin(request: Request, context: WorkerAppContext): void {
  const requestOrigin = normalizeOrigin(request.headers.get("origin"));
  const allowedOrigins = getAllowedMarketingOrigins(context);

  if (!requestOrigin) {
    throw problem(
      403,
      "support_issue_origin_required",
      "Origin header is required.",
      "Support issue reports are only accepted from approved Board Enthusiasts web origins."
    );
  }

  if (!allowedOrigins.includes(requestOrigin)) {
    throw problem(
      403,
      "support_issue_origin_forbidden",
      "Origin is not allowed.",
      "Support issue reports are only accepted from approved Board Enthusiasts web origins."
    );
  }
}

export async function handleMarketingSignupRoute(
  request: Request,
  service: Pick<WorkerAppService, "createMarketingSignup" | "getContext">,
  responseHeaders: HeadersInit
): Promise<Response> {
  assertAllowedMarketingOrigin(request, service.getContext());
  return json(await service.createMarketingSignup(await readJson<MarketingSignupRequest>(request)), {
    status: 201,
    headers: responseHeaders
  });
}

export async function handleSupportIssueRoute(
  request: Request,
  service: Pick<WorkerAppService, "reportSupportIssue" | "getContext">,
  responseHeaders: HeadersInit
): Promise<Response> {
  assertAllowedSupportIssueOrigin(request, service.getContext());
  return json(await service.reportSupportIssue(await readJson<SupportIssueReportRequest>(request)), {
    status: 202,
    headers: responseHeaders
  });
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const service = new WorkerAppService(env);
    const url = new URL(request.url);
    const token = getBearerToken(request);
    const responseHeaders = corsHeaders(request.headers.get("origin"));

    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: responseHeaders });
    }

    try {
      if (url.pathname === "/") {
        return json({
          service: "board-enthusiasts-workers-api",
          stack: "workers-supabase",
          environment: service.getContext().envName
        }, { headers: responseHeaders });
      }

      if (url.pathname === "/health/live") {
        return json({ status: "healthy", environment: service.getContext().envName }, { headers: responseHeaders });
      }

      if (url.pathname === "/health/ready") {
        return json(await service.getReadyState(), { headers: responseHeaders });
      }

      if (request.method === "POST" && url.pathname === "/marketing/signups") {
        return handleMarketingSignupRoute(request, service, responseHeaders);
      }

      if (request.method === "POST" && url.pathname === "/support/issues") {
        return handleSupportIssueRoute(request, service, responseHeaders);
      }

      if (request.method === "GET" && url.pathname === "/genres") {
        return json(await service.listGenres(), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/age-rating-authorities") {
        return json(await service.listAgeRatingAuthorities(), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/catalog") {
        const pageNumber = url.searchParams.get("pageNumber");
        const pageSize = url.searchParams.get("pageSize");
        const studioSlugs = url.searchParams.getAll("studioSlug");
        const genres = url.searchParams.getAll("genre");
        return json(
          await service.listCatalogTitles({
            studioSlug: studioSlugs.length > 0 ? studioSlugs : undefined,
            contentKind: (url.searchParams.get("contentKind") as "game" | "app" | null) ?? undefined,
            genre: genres.length > 0 ? genres : undefined,
            search: url.searchParams.get("search") ?? undefined,
            minPlayers: url.searchParams.get("minPlayers") ? Number(url.searchParams.get("minPlayers")) : undefined,
            maxPlayers: url.searchParams.get("maxPlayers") ? Number(url.searchParams.get("maxPlayers")) : undefined,
            sort:
              (url.searchParams.get("sort") as
                | "title-asc"
                | "title-desc"
                | "studio-asc"
                | "studio-desc"
                | "genre-asc"
                | "players-asc"
                | "players-desc"
                | "age-asc"
                | "age-desc"
                | null) ?? undefined,
            pageNumber: pageNumber ? Number(pageNumber) : undefined,
            pageSize: pageSize ? Number(pageSize) : undefined
          }),
          { headers: responseHeaders }
        );
      }

      const catalogMatch = url.pathname.match(/^\/catalog\/([^/]+)\/([^/]+)$/);
      if (request.method === "GET" && catalogMatch) {
        return json(await service.getCatalogTitle(catalogMatch[1]!, catalogMatch[2]!), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/studios") {
        return json(await service.listPublicStudios(), { headers: responseHeaders });
      }

      const studioSlugMatch = url.pathname.match(/^\/studios\/([^/]+)$/);
      if (request.method === "GET" && studioSlugMatch) {
        return json(await service.getPublicStudio(studioSlugMatch[1]!), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/identity/user-name-availability") {
        const requestedUserName = url.searchParams.get("userName") ?? "";
        if (!requestedUserName.trim()) {
          throw validationProblem({
            userName: ["userName is required."]
          });
        }

        return json(await service.getUserNameAvailability(requestedUserName), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/identity/me") {
        return json(await service.getCurrentUserResponse(token), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/identity/me/profile") {
        return json(await service.getCurrentUserProfile(token), { headers: responseHeaders });
      }

      if (request.method === "PUT" && url.pathname === "/identity/me/profile") {
        const body = await readJson<UpdateUserProfileRequest>(request);
        return json(await service.updateCurrentUserProfile(token, body), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/identity/me/board-profile") {
        return json(await service.getBoardProfile(token), { headers: responseHeaders });
      }

      if (request.method === "PUT" && url.pathname === "/identity/me/board-profile") {
        return json(await service.upsertBoardProfile(token, await readJson<UpsertBoardProfileRequest>(request)), { headers: responseHeaders });
      }

      if (request.method === "DELETE" && url.pathname === "/identity/me/board-profile") {
        await service.deleteBoardProfile(token);
        return new Response(null, { status: 204, headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/identity/me/notifications") {
        return json(await service.getCurrentUserNotifications(token), { headers: responseHeaders });
      }

      const currentUserNotificationReadMatch = url.pathname.match(/^\/identity\/me\/notifications\/([^/]+)\/read$/);
      if (currentUserNotificationReadMatch && request.method === "POST") {
        return json(await service.markCurrentUserNotificationRead(token, currentUserNotificationReadMatch[1]!), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/identity/me/developer-enrollment") {
        return json(await service.getDeveloperEnrollment(token), { headers: responseHeaders });
      }

      if (request.method === "POST" && url.pathname === "/identity/me/developer-enrollment") {
        return json(await service.enrollCurrentUserAsDeveloper(token), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/player/library") {
        return json(await service.getPlayerLibrary(token), { headers: responseHeaders });
      }

      const playerLibraryTitleMatch = url.pathname.match(/^\/player\/library\/titles\/([^/]+)$/);
      if (playerLibraryTitleMatch && request.method === "PUT") {
        return json(await service.setPlayerLibraryState(token, playerLibraryTitleMatch[1]!, true), { headers: responseHeaders });
      }

      if (playerLibraryTitleMatch && request.method === "DELETE") {
        return json(await service.setPlayerLibraryState(token, playerLibraryTitleMatch[1]!, false), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/player/wishlist") {
        return json(await service.getPlayerWishlist(token), { headers: responseHeaders });
      }

      const playerWishlistTitleMatch = url.pathname.match(/^\/player\/wishlist\/titles\/([^/]+)$/);
      if (playerWishlistTitleMatch && request.method === "PUT") {
        return json(await service.setPlayerWishlistState(token, playerWishlistTitleMatch[1]!, true), { headers: responseHeaders });
      }

      if (playerWishlistTitleMatch && request.method === "DELETE") {
        return json(await service.setPlayerWishlistState(token, playerWishlistTitleMatch[1]!, false), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/player/reports") {
        return json(await service.listPlayerTitleReports(token), { headers: responseHeaders });
      }

      if (request.method === "POST" && url.pathname === "/player/reports") {
        return json(await service.createPlayerTitleReport(token, await readJson(request)), { status: 201, headers: responseHeaders });
      }

      const playerReportMatch = url.pathname.match(/^\/player\/reports\/([^/]+)$/);
      if (playerReportMatch && request.method === "GET") {
        return json(await service.getPlayerTitleReport(token, playerReportMatch[1]!), { headers: responseHeaders });
      }

      const playerReportMessageMatch = url.pathname.match(/^\/player\/reports\/([^/]+)\/messages$/);
      if (playerReportMessageMatch && request.method === "POST") {
        return json(await service.addPlayerTitleReportMessage(token, playerReportMessageMatch[1]!, await readJson(request)), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/moderation/developers") {
        return json(await service.listModerationDevelopers(token, url.searchParams.get("search")), { headers: responseHeaders });
      }

      const developerVerificationMatch = url.pathname.match(/^\/moderation\/developers\/([^/]+)\/verification$/);
      if (request.method === "GET" && developerVerificationMatch) {
        return json(await service.getVerifiedDeveloperState(token, developerVerificationMatch[1]!), { headers: responseHeaders });
      }

      const verifiedDeveloperMatch = url.pathname.match(/^\/moderation\/developers\/([^/]+)\/verified-developer$/);
      if (verifiedDeveloperMatch && request.method === "PUT") {
        return json(await service.setVerifiedDeveloperState(token, verifiedDeveloperMatch[1]!, true), { headers: responseHeaders });
      }

      if (verifiedDeveloperMatch && request.method === "DELETE") {
        return json(await service.setVerifiedDeveloperState(token, verifiedDeveloperMatch[1]!, false), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/moderation/title-reports") {
        return json(await service.listModerationTitleReports(token), { headers: responseHeaders });
      }

      const moderationTitleReportMatch = url.pathname.match(/^\/moderation\/title-reports\/([^/]+)$/);
      if (moderationTitleReportMatch && request.method === "GET") {
        return json(await service.getModerationTitleReport(token, moderationTitleReportMatch[1]!), { headers: responseHeaders });
      }

      const moderationTitleReportMessageMatch = url.pathname.match(/^\/moderation\/title-reports\/([^/]+)\/messages$/);
      if (moderationTitleReportMessageMatch && request.method === "POST") {
        return json(await service.addModerationTitleReportMessage(token, moderationTitleReportMessageMatch[1]!, await readJson(request)), { headers: responseHeaders });
      }

      const moderationTitleReportValidateMatch = url.pathname.match(/^\/moderation\/title-reports\/([^/]+)\/validate$/);
      if (moderationTitleReportValidateMatch && request.method === "POST") {
        return json(await service.resolveModerationTitleReport(token, moderationTitleReportValidateMatch[1]!, true, await readJson(request)), { headers: responseHeaders });
      }

      const moderationTitleReportInvalidateMatch = url.pathname.match(/^\/moderation\/title-reports\/([^/]+)\/invalidate$/);
      if (moderationTitleReportInvalidateMatch && request.method === "POST") {
        return json(await service.resolveModerationTitleReport(token, moderationTitleReportInvalidateMatch[1]!, false, await readJson(request)), { headers: responseHeaders });
      }

      if (request.method === "GET" && url.pathname === "/developer/studios") {
        return json(await service.listManagedStudios(token), { headers: responseHeaders });
      }

      if (request.method === "POST" && url.pathname === "/studios") {
        return json(await service.createStudio(token, await readJson(request)), { status: 201, headers: responseHeaders });
      }

      const studioIdMatch = url.pathname.match(/^\/developer\/studios\/([^/]+)$/);
      if (studioIdMatch && request.method === "PUT") {
        return json(await service.updateStudio(token, studioIdMatch[1]!, await readJson(request)), { headers: responseHeaders });
      }

      if (studioIdMatch && request.method === "DELETE") {
        await service.deleteStudio(token, studioIdMatch[1]!);
        return new Response(null, { status: 204, headers: responseHeaders });
      }

      const studioLinksMatch = url.pathname.match(/^\/developer\/studios\/([^/]+)\/links$/);
      if (studioLinksMatch && request.method === "GET") {
        return json(await service.listStudioLinks(token, studioLinksMatch[1]!), { headers: responseHeaders });
      }

      if (studioLinksMatch && request.method === "POST") {
        return json(await service.createStudioLink(token, studioLinksMatch[1]!, await readJson(request)), { status: 201, headers: responseHeaders });
      }

      const studioLinkMatch = url.pathname.match(/^\/developer\/studios\/([^/]+)\/links\/([^/]+)$/);
      if (studioLinkMatch && request.method === "PUT") {
        return json(await service.updateStudioLink(token, studioLinkMatch[1]!, studioLinkMatch[2]!, await readJson(request)), { headers: responseHeaders });
      }

      if (studioLinkMatch && request.method === "DELETE") {
        await service.deleteStudioLink(token, studioLinkMatch[1]!, studioLinkMatch[2]!);
        return new Response(null, { status: 204, headers: responseHeaders });
      }

      const studioLogoMatch = url.pathname.match(/^\/developer\/studios\/([^/]+)\/logo-upload$/);
      if (studioLogoMatch && request.method === "POST") {
        const formData = await request.formData();
        return json(await service.uploadStudioMedia(token, studioLogoMatch[1]!, "logo", formData.get("media") as File | null), { headers: responseHeaders });
      }

      const studioAvatarMatch = url.pathname.match(/^\/developer\/studios\/([^/]+)\/avatar-upload$/);
      if (studioAvatarMatch && request.method === "POST") {
        const formData = await request.formData();
        return json(await service.uploadStudioMedia(token, studioAvatarMatch[1]!, "avatar", formData.get("media") as File | null), { headers: responseHeaders });
      }

      const studioBannerMatch = url.pathname.match(/^\/developer\/studios\/([^/]+)\/banner-upload$/);
      if (studioBannerMatch && request.method === "POST") {
        const formData = await request.formData();
        return json(await service.uploadStudioMedia(token, studioBannerMatch[1]!, "banner", formData.get("media") as File | null), { headers: responseHeaders });
      }

      const studioTitlesMatch = url.pathname.match(/^\/developer\/studios\/([^/]+)\/titles$/);
      if (studioTitlesMatch && request.method === "GET") {
        return json(await service.getStudioTitles(token, studioTitlesMatch[1]!), { headers: responseHeaders });
      }

      if (studioTitlesMatch && request.method === "POST") {
        return json(await service.createTitle(token, studioTitlesMatch[1]!, await readJson(request)), { status: 201, headers: responseHeaders });
      }

      const developerTitleMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)$/);
      if (developerTitleMatch && request.method === "GET") {
        return json(await service.getDeveloperTitle(token, developerTitleMatch[1]!), { headers: responseHeaders });
      }

      if (developerTitleMatch && request.method === "PUT") {
        return json(await service.updateTitle(token, developerTitleMatch[1]!, await readJson(request)), { headers: responseHeaders });
      }

      const developerTitleMetadataCurrentMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/metadata\/current$/);
      if (developerTitleMetadataCurrentMatch && request.method === "PUT") {
        return json(await service.upsertTitleMetadata(token, developerTitleMetadataCurrentMatch[1]!, await readJson(request)), { headers: responseHeaders });
      }

      const developerTitleMetadataVersionsMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/metadata-versions$/);
      if (developerTitleMetadataVersionsMatch && request.method === "GET") {
        return json(await service.getTitleMetadataVersions(token, developerTitleMetadataVersionsMatch[1]!), { headers: responseHeaders });
      }

      const developerTitleMetadataActivateMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/metadata-versions\/([^/]+)\/activate$/);
      if (developerTitleMetadataActivateMatch && request.method === "POST") {
        return json(await service.activateTitleMetadataVersion(token, developerTitleMetadataActivateMatch[1]!, Number(developerTitleMetadataActivateMatch[2]!)), { headers: responseHeaders });
      }

      const developerTitleMediaListMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/media$/);
      if (developerTitleMediaListMatch && request.method === "GET") {
        return json(await service.getTitleMediaAssets(token, developerTitleMediaListMatch[1]!), { headers: responseHeaders });
      }

      const developerTitleMediaMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/media\/([^/]+)$/);
      if (developerTitleMediaMatch && request.method === "PUT") {
        return json(await service.upsertTitleMediaAsset(token, developerTitleMediaMatch[1]!, developerTitleMediaMatch[2]!, await readJson(request)), { headers: responseHeaders });
      }

      if (developerTitleMediaMatch && request.method === "DELETE") {
        await service.deleteTitleMediaAsset(token, developerTitleMediaMatch[1]!, developerTitleMediaMatch[2]!);
        return new Response(null, { status: 204, headers: responseHeaders });
      }

      const developerTitleMediaUploadMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/media\/([^/]+)\/upload$/);
      if (developerTitleMediaUploadMatch && request.method === "POST") {
        const formData = await request.formData();
        return json(
          await service.uploadTitleMediaAsset(
            token,
            developerTitleMediaUploadMatch[1]!,
            developerTitleMediaUploadMatch[2]!,
            formData.get("media") as File | null,
            (formData.get("altText") as string | null) ?? null
          ),
          { headers: responseHeaders }
        );
      }

      const developerTitleReportsMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/reports$/);
      if (developerTitleReportsMatch && request.method === "GET") {
        return json(await service.getDeveloperTitleReports(token, developerTitleReportsMatch[1]!), { headers: responseHeaders });
      }

      const developerTitleReportMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/reports\/([^/]+)$/);
      if (developerTitleReportMatch && request.method === "GET") {
        return json(await service.getDeveloperTitleReport(token, developerTitleReportMatch[1]!, developerTitleReportMatch[2]!), { headers: responseHeaders });
      }

      const developerTitleReportMessageMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/reports\/([^/]+)\/messages$/);
      if (developerTitleReportMessageMatch && request.method === "POST") {
        return json(await service.addDeveloperTitleReportMessage(token, developerTitleReportMessageMatch[1]!, developerTitleReportMessageMatch[2]!, await readJson(request)), { headers: responseHeaders });
      }

      const developerTitleReleasesMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/releases$/);
      if (developerTitleReleasesMatch && request.method === "GET") {
        return json(await service.getTitleReleases(token, developerTitleReleasesMatch[1]!), { headers: responseHeaders });
      }

      if (developerTitleReleasesMatch && request.method === "POST") {
        return json(await service.createTitleRelease(token, developerTitleReleasesMatch[1]!, await readJson(request)), { status: 201, headers: responseHeaders });
      }

      const developerTitleReleaseMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/releases\/([^/]+)$/);
      if (developerTitleReleaseMatch && request.method === "PUT") {
        return json(await service.updateTitleRelease(token, developerTitleReleaseMatch[1]!, developerTitleReleaseMatch[2]!, await readJson(request)), { headers: responseHeaders });
      }

      const developerTitleReleasePublishMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/releases\/([^/]+)\/publish$/);
      if (developerTitleReleasePublishMatch && request.method === "POST") {
        return json(await service.publishTitleRelease(token, developerTitleReleasePublishMatch[1]!, developerTitleReleasePublishMatch[2]!), { headers: responseHeaders });
      }

      const developerTitleReleaseActivateMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/releases\/([^/]+)\/activate$/);
      if (developerTitleReleaseActivateMatch && request.method === "POST") {
        return json(await service.activateTitleRelease(token, developerTitleReleaseActivateMatch[1]!, developerTitleReleaseActivateMatch[2]!), { headers: responseHeaders });
      }

      const developerTitleReleaseWithdrawMatch = url.pathname.match(/^\/developer\/titles\/([^/]+)\/releases\/([^/]+)\/withdraw$/);
      if (developerTitleReleaseWithdrawMatch && request.method === "POST") {
        return json(await service.withdrawTitleRelease(token, developerTitleReleaseWithdrawMatch[1]!, developerTitleReleaseWithdrawMatch[2]!), { headers: responseHeaders });
      }

      return json(
        {
          error: "not_found",
          message: `No route matched ${request.method} ${url.pathname}`
        },
        { status: 404, headers: responseHeaders }
      );
    } catch (error: unknown) {
      if (error instanceof ApiError) {
        return json(error.payload, { status: error.status, headers: responseHeaders });
      }

      const message = error instanceof Error ? error.message : String(error);
      return json(
        {
          type: "https://boardtpl.dev/problems/unhandled-error",
          title: "Unhandled worker error",
          status: 500,
          detail: message,
          code: "unhandled_worker_error"
        },
        { status: 500, headers: responseHeaders }
      );
    }
  }
};
