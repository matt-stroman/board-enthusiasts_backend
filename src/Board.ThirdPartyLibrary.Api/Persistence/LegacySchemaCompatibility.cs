using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Persistence;

/// <summary>
/// Normalizes legacy organization-era physical schema names to the current studio-era schema.
/// </summary>
internal static class LegacySchemaCompatibility
{
    private static readonly IReadOnlyList<string> NormalizationStatements =
    [
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'organizations')
               AND NOT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'studios')
            THEN
                ALTER TABLE public.organizations RENAME TO studios;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'organization_memberships')
               AND NOT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'studio_memberships')
            THEN
                ALTER TABLE public.organization_memberships RENAME TO studio_memberships;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'studio_memberships' AND column_name = 'organization_id')
               AND NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'studio_memberships' AND column_name = 'studio_id')
            THEN
                ALTER TABLE public.studio_memberships RENAME COLUMN organization_id TO studio_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'titles' AND column_name = 'organization_id')
               AND NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'titles' AND column_name = 'studio_id')
            THEN
                ALTER TABLE public.titles RENAME COLUMN organization_id TO studio_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'integration_connections' AND column_name = 'organization_id')
               AND NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'integration_connections' AND column_name = 'studio_id')
            THEN
                ALTER TABLE public.integration_connections RENAME COLUMN organization_id TO studio_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace AND conname = 'pk_organizations')
            THEN
                ALTER TABLE public.studios RENAME CONSTRAINT pk_organizations TO pk_studios;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace AND conname = 'pk_organization_memberships')
            THEN
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT pk_organization_memberships TO pk_studio_memberships;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace AND conname = 'ck_organization_memberships_role')
            THEN
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT ck_organization_memberships_role TO ck_studio_memberships_role;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace AND conname = 'fk_organization_memberships_organizations_organization_id')
            THEN
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT fk_organization_memberships_organizations_organization_id TO fk_studio_memberships_studios_studio_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace AND conname = 'fk_organization_memberships_users_user_id')
            THEN
                ALTER TABLE public.studio_memberships RENAME CONSTRAINT fk_organization_memberships_users_user_id TO fk_studio_memberships_users_user_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace AND conname = 'fk_titles_organizations_organization_id')
            THEN
                ALTER TABLE public.titles RENAME CONSTRAINT fk_titles_organizations_organization_id TO fk_titles_studios_studio_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE connamespace = 'public'::regnamespace AND conname = 'fk_integration_connections_organizations_organization_id')
            THEN
                ALTER TABLE public.integration_connections RENAME CONSTRAINT fk_integration_connections_organizations_organization_id TO fk_integration_connections_studios_studio_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'pk_organizations')
            THEN
                ALTER INDEX public.pk_organizations RENAME TO pk_studios;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'ux_organizations_slug')
            THEN
                ALTER INDEX public.ux_organizations_slug RENAME TO ux_studios_slug;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'pk_organization_memberships')
            THEN
                ALTER INDEX public.pk_organization_memberships RENAME TO pk_studio_memberships;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'ix_organization_memberships_user_id')
            THEN
                ALTER INDEX public.ix_organization_memberships_user_id RENAME TO ix_studio_memberships_user_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'ix_titles_organization_id')
            THEN
                ALTER INDEX public.ix_titles_organization_id RENAME TO ix_titles_studio_id;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'ux_titles_organization_id_slug')
            THEN
                ALTER INDEX public.ux_titles_organization_id_slug RENAME TO ux_titles_studio_id_slug;
            END IF;
        END
        $$;
        """,
        """
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public' AND indexname = 'ix_integration_connections_organization_id')
            THEN
                ALTER INDEX public.ix_integration_connections_organization_id RENAME TO ix_integration_connections_studio_id;
            END IF;
        END
        $$;
        """
    ];

    /// <summary>
    /// Updates legacy schema object names when a local database still carries organization-era names.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when normalization is done.</returns>
    public static async Task NormalizeAsync(BoardLibraryDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        foreach (var statement in NormalizationStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
