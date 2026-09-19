using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodDonationNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentStepRetryCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "AgentSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "AgentSteps");
        }
    }
}
