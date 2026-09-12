using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodDonationNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffInvitationsAndForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffInvitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonationAppointments_DonorId",
                table: "DonationAppointments",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_Email",
                table: "StaffInvitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_InvitedByUserId",
                table: "StaffInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_OrganizationId",
                table: "StaffInvitations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_Token",
                table: "StaffInvitations",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DonationAppointments_Users_DonorId",
                table: "DonationAppointments",
                column: "DonorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DonorDevices_Users_DonorUserId",
                table: "DonorDevices",
                column: "DonorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DonationAppointments_Users_DonorId",
                table: "DonationAppointments");

            migrationBuilder.DropForeignKey(
                name: "FK_DonorDevices_Users_DonorUserId",
                table: "DonorDevices");

            migrationBuilder.DropTable(
                name: "StaffInvitations");

            migrationBuilder.DropIndex(
                name: "IX_DonationAppointments_DonorId",
                table: "DonationAppointments");
        }
    }
}
