import type { MarketingContactRoleInterest } from "@board-enthusiasts/migration-contract";

const brandSiteUrl = "https://boardenthusiasts.com";
const brandDiscordUrl = "https://discord.gg/cz2zReWqcA";

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function formatRoleInterests(roleInterests: readonly MarketingContactRoleInterest[]): string {
  const labels: Record<MarketingContactRoleInterest, string> = {
    developer: "creating third-party content for Board",
    player: "following new Board games and apps",
  };

  return [...roleInterests]
    .sort()
    .map((role) => labels[role] ?? role)
    .join(", ");
}

export interface MarketingSignupWelcomeEmail {
  subject: string;
  recipientName: string;
  text: string;
  html: string;
}

export function renderMarketingSignupWelcomeEmail(input: {
  firstName: string | null;
  roleInterests: readonly MarketingContactRoleInterest[];
}): MarketingSignupWelcomeEmail {
  const subject = "You're on the BE list!";
  const greetingName = input.firstName?.trim() || "there";
  const recipientName = input.firstName?.trim() || "Interested";
  const roleInterestLine =
    input.roleInterests.length === 0
      ? "You will hear from us about launch progress, early invites, and new BE resources as they become available."
      : `We noted your current interests as: ${formatRoleInterests(input.roleInterests)}.`;

  const text = [
    `Hi ${greetingName},`,
    "",
    "You've been added to the Board Enthusiasts (BE) early-access list.",
    "",
    "We'll send occasional updates about launch progress, early invites, and new community resources as the BE platform and broader ecosystem continue taking shape.",
    roleInterestLine,
    "",
    "You do not need to do anything else right now. We'll reach out when there is something worth sharing.",
    "",
    "Board Enthusiasts",
    brandSiteUrl,
  ].join("\n");

  const escapedGreetingName = escapeHtml(greetingName);
  const escapedRoleInterestLine = escapeHtml(roleInterestLine);

  const html = `<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>${escapeHtml(subject)}</title>
  </head>
  <body style="margin: 0; padding: 0; background-color: #060811; color: #f8fafc; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;">
    <div style="display: none; max-height: 0; overflow: hidden; opacity: 0;">
      You've been added to the Board Enthusiasts early-access list.
    </div>
    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color: #060811; background-image: radial-gradient(circle at top left, rgba(61, 203, 159, 0.18), transparent 38%), radial-gradient(circle at top right, rgba(120, 119, 198, 0.14), transparent 34%);">
      <tr>
        <td align="center" style="padding: 32px 16px;">
          <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width: 640px;">
            <tr>
              <td style="padding-bottom: 16px;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                  <tr>
                    <td valign="middle" style="padding-right: 12px;">
                      <img src="${brandSiteUrl}/favicon_sm.png" alt="Board Enthusiasts" width="34" height="34" style="display: block; border: 0; width: 34px; height: 34px; border-radius: 8px;" />
                    </td>
                    <td valign="middle" style="color: #9fb2d8; font-size: 13px; letter-spacing: 0.24em; text-transform: uppercase;">
                      Board Enthusiasts
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
            <tr>
              <td style="border: 1px solid rgba(159, 178, 216, 0.18); border-radius: 28px; background: linear-gradient(180deg, rgba(16, 22, 38, 0.98), rgba(8, 12, 24, 0.98)); box-shadow: 0 22px 50px rgba(0, 0, 0, 0.35); overflow: hidden;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                  <tr>
                    <td style="padding: 36px 36px 12px 36px;">
                      <div style="display: inline-block; padding: 8px 14px; border-radius: 999px; background-color: rgba(61, 203, 159, 0.12); color: #b9f3de; font-size: 12px; letter-spacing: 0.18em; text-transform: uppercase;">
                        Early access
                      </div>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding: 0 36px 10px 36px; color: #f8fafc; font-size: 34px; line-height: 1.15; font-weight: 700;">
                      You're on the BE list
                    </td>
                  </tr>
                  <tr>
                    <td style="padding: 0 36px; color: #d5def0; font-size: 18px; line-height: 1.8;">
                      Hi ${escapedGreetingName},
                      <br /><br />
                      You've been added to the Board Enthusiasts (BE) early-access list.
                      <br /><br />
                      We'll send occasional updates about launch progress, early invites, and new community resources as the BE platform and broader ecosystem continue taking shape.
                      <br /><br />
                      ${escapedRoleInterestLine}
                    </td>
                  </tr>
                  <tr>
                    <td style="padding: 28px 36px 0 36px;">
                      <a href="${brandSiteUrl}" style="display: inline-block; padding: 16px 28px; border-radius: 999px; background-color: #3dcb9f; color: #061018; font-size: 14px; font-weight: 700; letter-spacing: 0.22em; text-transform: uppercase; text-decoration: none;">
                        Visit Board Enthusiasts
                      </a>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding: 28px 36px 0 36px; color: #9fb2d8; font-size: 15px; line-height: 1.8;">
                      You do not need to do anything else right now. We'll reach out when there is something worth sharing.
                    </td>
                  </tr>
                  <tr>
                    <td style="padding: 32px 36px 36px 36px;">
                      <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="border-top: 1px solid rgba(159, 178, 216, 0.14);">
                        <tr>
                          <td style="padding-top: 24px;">
                            <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                              <tr>
                                <td valign="top" style="padding-right: 14px;">
                                  <img src="${brandSiteUrl}/favicon_sm.png" alt="Board Enthusiasts" width="42" height="42" style="display: block; border: 0; width: 42px; height: 42px; border-radius: 10px;" />
                                </td>
                                <td valign="top">
                                  <div style="color: #f8fafc; font-size: 18px; font-weight: 700;">Board Enthusiasts</div>
                                  <div style="padding-top: 4px; color: #9fb2d8; font-size: 14px; line-height: 1.6;">For Board Players and Builders</div>
                                </td>
                              </tr>
                            </table>
                          </td>
                          <td align="right" valign="middle" style="padding-top: 24px;">
                            <a href="${brandDiscordUrl}" style="display: inline-block; padding: 10px 14px; border: 1px solid rgba(159, 178, 216, 0.18); border-radius: 999px; color: #d5def0; font-size: 13px; font-weight: 600; text-decoration: none;">
                              Join us on Discord
                            </a>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>`;

  return {
    subject,
    recipientName,
    text,
    html,
  };
}
