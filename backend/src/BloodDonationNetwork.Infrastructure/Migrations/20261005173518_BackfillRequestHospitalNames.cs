using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodDonationNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRequestHospitalNames : Migration
    {
        /// <inheritdoc />
        // Requests used to be saved with an empty HospitalName, so their workflow
        // objectives read "...blood for .". Fill both in from the requesting
        // organization. Data-only, so Down has nothing to undo.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "BloodRequests" AS r
                SET "HospitalName" = o."Name"
                FROM "Organizations" AS o
                WHERE r."OrganizationId" = o."Id"
                  AND (r."HospitalName" IS NULL OR btrim(r."HospitalName") = '');
                """);

            migrationBuilder.Sql("""
                UPDATE "AgentWorkflows" AS w
                SET "Objective" = left(w."Objective", length(w."Objective") - 1) || r."HospitalName" || '.'
                FROM "BloodRequests" AS r
                WHERE w."BloodRequestId" = r."Id"
                  AND w."Objective" LIKE '% for .'
                  AND btrim(r."HospitalName") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
