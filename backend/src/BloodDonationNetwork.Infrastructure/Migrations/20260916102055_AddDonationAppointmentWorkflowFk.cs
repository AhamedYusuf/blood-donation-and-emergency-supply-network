using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodDonationNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationAppointmentWorkflowFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DonationAppointments_RelatedWorkflowId",
                table: "DonationAppointments",
                column: "RelatedWorkflowId");

            migrationBuilder.AddForeignKey(
                name: "FK_DonationAppointments_AgentWorkflows_RelatedWorkflowId",
                table: "DonationAppointments",
                column: "RelatedWorkflowId",
                principalTable: "AgentWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DonationAppointments_AgentWorkflows_RelatedWorkflowId",
                table: "DonationAppointments");

            migrationBuilder.DropIndex(
                name: "IX_DonationAppointments_RelatedWorkflowId",
                table: "DonationAppointments");
        }
    }
}
