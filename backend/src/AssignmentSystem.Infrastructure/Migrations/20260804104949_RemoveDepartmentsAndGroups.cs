using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDepartmentsAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_courses_departments_department_id",
                table: "courses");

            migrationBuilder.DropForeignKey(
                name: "fk_courses_groups_group_id",
                table: "courses");

            migrationBuilder.DropForeignKey(
                name: "fk_users_departments_department_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_users_groups_group_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropIndex(
                name: "ix_users_department_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_group_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_courses_department_id",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "ix_courses_group_id",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "courses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "courses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "courses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_group_id",
                table: "users",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_department_id",
                table: "courses",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_group_id",
                table: "courses",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_code",
                table: "departments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_groups_code",
                table: "groups",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_departments_department_id",
                table: "courses",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_groups_group_id",
                table: "courses",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_departments_department_id",
                table: "users",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_users_groups_group_id",
                table: "users",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
