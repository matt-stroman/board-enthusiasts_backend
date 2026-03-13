import { describe, expect, it, vi } from "vitest";
import { handleMarketingSignupRoute, handleSupportIssueRoute } from "./worker";

describe("handleMarketingSignupRoute", () => {
  it("returns a created marketing signup response", async () => {
    const service = {
      getContext: vi.fn().mockReturnValue({
        allowedWebOrigins: ["https://boardenthusiasts.com"],
      }),
      createMarketingSignup: vi.fn().mockResolvedValue({
        accepted: true,
        duplicate: false,
        signup: {
          email: "matt@example.com",
          firstName: "Matt",
          status: "subscribed",
          lifecycleStatus: "waitlisted",
          roleInterests: ["player"],
          source: "landing_page",
          consentedAt: "2026-03-12T18:00:00Z",
          updatedAt: "2026-03-12T18:00:00Z",
        },
      }),
    };

    const response = await handleMarketingSignupRoute(
      new Request("http://example.test/marketing/signups", {
        method: "POST",
        headers: { "content-type": "application/json", origin: "https://boardenthusiasts.com" },
        body: JSON.stringify({
          email: "matt@example.com",
          firstName: "Matt",
          source: "landing_page",
          consentTextVersion: "landing-page-v1",
          turnstileToken: "token-123",
          roleInterests: ["player"],
        }),
      }),
      service as never,
      {},
    );

    expect(response.status).toBe(201);
    expect(service.createMarketingSignup).toHaveBeenCalledWith({
      email: "matt@example.com",
      firstName: "Matt",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "token-123",
      roleInterests: ["player"],
    });
  });

  it("rejects invalid request bodies", async () => {
    const service = {
      getContext: vi.fn().mockReturnValue({
        allowedWebOrigins: ["https://boardenthusiasts.com"],
      }),
      createMarketingSignup: vi.fn(),
    };

    await expect(
      handleMarketingSignupRoute(
        new Request("http://example.test/marketing/signups", {
          method: "POST",
          headers: { "content-type": "application/json", origin: "https://boardenthusiasts.com" },
          body: "{bad-json",
        }),
        service as never,
        {},
      ),
    ).rejects.toMatchObject({
      status: 422,
      payload: {
        errors: {
          body: ["Request body must be valid JSON."],
        },
      },
    });
  });

  it("rejects marketing signups from unapproved origins", async () => {
    const service = {
      getContext: vi.fn().mockReturnValue({
        allowedWebOrigins: ["https://boardenthusiasts.com"],
      }),
      createMarketingSignup: vi.fn(),
    };

    await expect(
      handleMarketingSignupRoute(
        new Request("http://example.test/marketing/signups", {
          method: "POST",
          headers: { "content-type": "application/json", origin: "https://evil.example" },
          body: JSON.stringify({
            email: "matt@example.com",
            source: "landing_page",
            consentTextVersion: "landing-page-v1",
          }),
        }),
        service as never,
        {},
      ),
    ).rejects.toMatchObject({
      status: 403,
      payload: {
        code: "marketing_origin_forbidden",
      },
    });
    expect(service.createMarketingSignup).not.toHaveBeenCalled();
  });
});

describe("handleSupportIssueRoute", () => {
  it("accepts an internal support issue report", async () => {
    const service = {
      getContext: vi.fn().mockReturnValue({
        allowedWebOrigins: ["https://boardenthusiasts.com"],
      }),
      reportSupportIssue: vi.fn().mockResolvedValue({
        accepted: true,
      }),
    };

    const response = await handleSupportIssueRoute(
      new Request("http://example.test/support/issues", {
        method: "POST",
        headers: { "content-type": "application/json", origin: "https://boardenthusiasts.com" },
        body: JSON.stringify({
          category: "email_signup",
          firstName: "Taylor",
          email: "taylor@example.com",
          pageUrl: "https://boardenthusiasts.com/#signup",
          apiBaseUrl: "https://api.boardenthusiasts.com",
          occurredAt: "2026-03-12T22:30:00Z",
          errorMessage: "We couldn't submit your signup right now.",
          technicalDetails: "Network request failed with a connection error.",
          userAgent: "Vitest Browser",
        }),
      }),
      service as never,
      {},
    );

    expect(response.status).toBe(202);
    expect(service.reportSupportIssue).toHaveBeenCalledWith(
      expect.objectContaining({
        category: "email_signup",
        firstName: "Taylor",
        email: "taylor@example.com",
      }),
    );
  });

  it("rejects support issue reports from unapproved origins", async () => {
    const service = {
      getContext: vi.fn().mockReturnValue({
        allowedWebOrigins: ["https://boardenthusiasts.com"],
      }),
      reportSupportIssue: vi.fn(),
    };

    await expect(
      handleSupportIssueRoute(
        new Request("http://example.test/support/issues", {
          method: "POST",
          headers: { "content-type": "application/json", origin: "https://evil.example" },
          body: JSON.stringify({
            category: "email_signup",
            pageUrl: "https://boardenthusiasts.com/#signup",
            apiBaseUrl: "https://api.boardenthusiasts.com",
            occurredAt: "2026-03-12T22:30:00Z",
            errorMessage: "We couldn't submit your signup right now.",
          }),
        }),
        service as never,
        {},
      ),
    ).rejects.toMatchObject({
      status: 403,
      payload: {
        code: "support_issue_origin_forbidden",
      },
    });
    expect(service.reportSupportIssue).not.toHaveBeenCalled();
  });
});
