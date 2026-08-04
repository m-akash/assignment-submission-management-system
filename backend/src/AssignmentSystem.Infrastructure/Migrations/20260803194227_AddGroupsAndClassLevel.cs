using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Adds student groups, and turns the free-text class grade into a number.
    ///
    /// Hand-adjusted from the scaffolded version, which dropped <c>grade</c> and added
    /// <c>level</c> defaulting to 0 — that throws the grades away and leaves every class
    /// at a level the domain rejects (valid range is 1..12). Instead the numeral is
    /// translated across first, and only then is the old column removed.
    /// </summary>
    public partial class AddGroupsAndClassLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "users",
                type: "uuid",
                nullable: true);

            // Nullable first so existing rows survive the add, then backfilled from the
            // Roman numeral that used to live in `grade`, then tightened.
            migrationBuilder.AddColumn<int>(
                name: "level",
                table: "classes",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE classes SET level = CASE upper(trim(grade))
                    WHEN 'I'    THEN 1
                    WHEN 'II'   THEN 2
                    WHEN 'III'  THEN 3
                    WHEN 'IV'   THEN 4
                    WHEN 'V'    THEN 5
                    WHEN 'VI'   THEN 6
                    WHEN 'VII'  THEN 7
                    WHEN 'VIII' THEN 8
                    WHEN 'IX'   THEN 9
                    WHEN 'X'    THEN 10
                    WHEN 'XI'   THEN 11
                    WHEN 'XII'  THEN 12
                    -- Anything else (a plain number, or a grade never set) is coerced into
                    -- range so the NOT NULL below cannot fail on legacy data.
                    ELSE LEAST(GREATEST(COALESCE(NULLIF(regexp_replace(COALESCE(grade, ''), '\D', '', 'g'), '')::int, 1), 1), 12)
                END;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "level",
                table: "classes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "grade",
                table: "classes");

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_group_id",
                table: "users",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_groups_code",
                table: "groups",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_groups_group_id",
                table: "users",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_groups_group_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropIndex(
                name: "ix_users_group_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "level",
                table: "classes");

            migrationBuilder.AddColumn<string>(
                name: "grade",
                table: "classes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
