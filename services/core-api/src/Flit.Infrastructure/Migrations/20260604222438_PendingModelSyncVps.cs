using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelSyncVps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente: en VPS/DEV la columna puede existir sin fila en __EFMigrationsHistory.
            migrationBuilder.Sql(
                """
                ALTER TABLE identity_verification.verification_sessions
                  ADD COLUMN IF NOT EXISTS "ChannelsCompleted" text[] NOT NULL DEFAULT ARRAY[]::text[];

                ALTER TABLE identity_verification.verification_sessions
                  ADD COLUMN IF NOT EXISTS "DocumentTypeCode" text;

                ALTER TABLE identity_verification.verification_sessions
                  ADD COLUMN IF NOT EXISTS "LivenessPerformed" boolean NOT NULL DEFAULT false;

                ALTER TABLE identity_verification.verification_sessions
                  ADD COLUMN IF NOT EXISTS "PerformedAt" timestamp with time zone;

                ALTER TABLE identity_verification.verification_sessions
                  ADD COLUMN IF NOT EXISTS "Score" numeric;

                ALTER TABLE identity_verification.verification_sessions
                  ADD COLUMN IF NOT EXISTS "Verdict" text;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE identity_verification.verification_sessions
                  DROP COLUMN IF EXISTS "ChannelsCompleted";

                ALTER TABLE identity_verification.verification_sessions
                  DROP COLUMN IF EXISTS "DocumentTypeCode";

                ALTER TABLE identity_verification.verification_sessions
                  DROP COLUMN IF EXISTS "LivenessPerformed";

                ALTER TABLE identity_verification.verification_sessions
                  DROP COLUMN IF EXISTS "PerformedAt";

                ALTER TABLE identity_verification.verification_sessions
                  DROP COLUMN IF EXISTS "Score";

                ALTER TABLE identity_verification.verification_sessions
                  DROP COLUMN IF EXISTS "Verdict";
                """);
        }
    }
}
