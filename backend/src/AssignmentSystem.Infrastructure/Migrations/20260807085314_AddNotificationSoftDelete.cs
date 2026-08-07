using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "notifications");
        }
    }
}
