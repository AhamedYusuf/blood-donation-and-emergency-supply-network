using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodDonationNetwork.Infrastructure.Migrations
{
    public partial class AlignAgentStepColumnNamesWithSpec : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InputJson",
                table: "AgentSteps",
                newName: "input_data");

            migrationBuilder.RenameColumn(
                name: "OutputJson",
                table: "AgentSteps",
                newName: "output_data");

            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "AgentSteps",
                newName: "retry_count");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "input_data",
                table: "AgentSteps",
                newName: "InputJson");

            migrationBuilder.RenameColumn(
                name: "output_data",
                table: "AgentSteps",
                newName: "OutputJson");

            migrationBuilder.RenameColumn(
                name: "retry_count",
                table: "AgentSteps",
                newName: "RetryCount");
        }
    }
}