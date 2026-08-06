using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxClaimAndBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_status_created_at_utc",
                table: "notifications");

            migrationBuilder.AddColumn<DateTime>(
                name: "claimed_at_utc",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_attempt_at_utc",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_status_created_at_utc",
                table: "notifications",
                columns: new[] { "status", "created_at_utc" },
                filter: "status IN (0, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_status_created_at_utc",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "claimed_at_utc",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "next_attempt_at_utc",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_status_created_at_utc",
                table: "notifications",
                columns: new[] { "status", "created_at_utc" },
                filter: "status = 0");
        }
    }
}
