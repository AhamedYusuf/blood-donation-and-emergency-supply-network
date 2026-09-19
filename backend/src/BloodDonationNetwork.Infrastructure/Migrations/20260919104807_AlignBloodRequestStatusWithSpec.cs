using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodDonationNetwork.Infrastructure.Migrations
{
    public partial class AlignBloodRequestStatusWithSpec : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert old integer enum values to the new
            // specification-compliant string statuses.
            migrationBuilder.Sql(
                """
                ALTER TABLE "BloodRequests"
                ALTER COLUMN "Status" TYPE text
                USING (
                    CASE "Status"
                        WHEN 0 THEN 'open'
                        WHEN 1 THEN 'awaiting_approval'
                        WHEN 2 THEN 'awaiting_approval'
                        WHEN 3 THEN 'donors_notified'
                        WHEN 4 THEN 'fulfilled'
                        WHEN 5 THEN 'cancelled'
                        WHEN 6 THEN 'cancelled'
                        ELSE 'open'
                    END
                );
                """
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert the new string statuses back to the closest
            // values supported by the old enum.
            migrationBuilder.Sql(
                """
                ALTER TABLE "BloodRequests"
                ALTER COLUMN "Status" TYPE integer
                USING (
                    CASE "Status"
                        WHEN 'open' THEN 0
                        WHEN 'matching' THEN 0
                        WHEN 'awaiting_approval' THEN 1
                        WHEN 'donors_notified' THEN 3
                        WHEN 'partially_fulfilled' THEN 3
                        WHEN 'fulfilled' THEN 4
                        WHEN 'expired' THEN 5
                        WHEN 'cancelled' THEN 6
                        ELSE 0
                    END
                );
                """
            );
        }
    }
}