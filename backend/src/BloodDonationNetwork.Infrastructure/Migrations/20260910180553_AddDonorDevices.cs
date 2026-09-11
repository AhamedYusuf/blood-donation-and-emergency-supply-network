using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodDonationNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDonorDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DonorDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FcmToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonorDevices_DonorUserId",
                table: "DonorDevices",
                column: "DonorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DonorDevices_FcmToken",
                table: "DonorDevices",
                column: "FcmToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonorDevices");
        }
    }
}
