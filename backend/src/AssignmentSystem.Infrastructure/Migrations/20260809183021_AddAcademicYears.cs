using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Adds academic years and hangs every enrollment off one.
    ///
    /// Hand-edited from the scaffolded version, which added <c>academic_year_id</c> as NOT
    /// NULL defaulting to the all-zeros Guid — fine on an empty database, a foreign-key
    /// violation on any deployment that already has students. The column is therefore added
    /// nullable, backfilled, and only then tightened, in the order below:
    ///
    ///   1. create <c>academic_years</c>
    ///   2. insert one session, but only where enrollments already exist
    ///   3. add the column nullable and point every existing row at that session
    ///   4. set NOT NULL, then add the foreign key and indexes
    ///
    /// The backfilled session is flagged current so an upgraded deployment keeps working:
    /// creating a student defaults their enrollment to the current year, and a school with
    /// none would be told to go and make one before it could add anybody. Its name and dates
    /// are a guess from the migration date — the admin is expected to correct them, which is
    /// why it is one row rather than an attempt to reconstruct the school's history.
    /// </summary>
    public partial class AddAcademicYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "academic_years",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_academic_years", x => x.id);
                });

            // Only where there is something to backfill. A fresh database gets its years from
            // the seeder (or from the admin), and a placeholder session sitting in the list
            // of an installation that never needed one is noise.
            //
            // The session is taken to run July–June, the same assumption the seeder makes and
            // the only thing either has to go on. Nothing in the domain depends on it.
            migrationBuilder.Sql("""
                WITH session AS (
                    SELECT CASE
                               WHEN EXTRACT(MONTH FROM CURRENT_DATE) >= 7
                               THEN EXTRACT(YEAR FROM CURRENT_DATE)::int
                               ELSE EXTRACT(YEAR FROM CURRENT_DATE)::int - 1
                           END AS start_year
                )
                INSERT INTO academic_years
                    (id, name, start_date, end_date, is_current, created_at_utc, updated_at_utc)
                SELECT
                    gen_random_uuid(),
                    start_year || '-' || (start_year + 1),
                    make_date(start_year, 7, 1),
                    make_date(start_year + 1, 6, 30),
                    TRUE,
                    NOW(),
                    NOW()
                FROM session
                WHERE EXISTS (SELECT 1 FROM student_enrollments);
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "academic_year_id",
                table: "student_enrollments",
                type: "uuid",
                nullable: true);

            // Every pre-existing enrollment belongs to the one session inserted above. If the
            // insert did not run there are no rows here either, so this updates nothing.
            migrationBuilder.Sql("""
                UPDATE student_enrollments
                SET academic_year_id = (SELECT id FROM academic_years WHERE is_current LIMIT 1)
                WHERE academic_year_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "academic_year_id",
                table: "student_enrollments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Replaced by the three-column index below: the same student in the same class in
            // a later session is a new enrollment, which the old pair rejected.
            migrationBuilder.DropIndex(
                name: "ix_student_enrollments_student_id_class_id",
                table: "student_enrollments");

            migrationBuilder.CreateIndex(
                name: "ix_student_enrollments_academic_year_id",
                table: "student_enrollments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_enrollments_student_id_class_id_academic_year_id",
                table: "student_enrollments",
                columns: new[] { "student_id", "class_id", "academic_year_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_academic_years_is_current_unique",
                table: "academic_years",
                column: "is_current",
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "ix_academic_years_name",
                table: "academic_years",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_academic_years_start_date",
                table: "academic_years",
                column: "start_date");

            migrationBuilder.AddForeignKey(
                name: "fk_student_enrollments_academic_years_academic_year_id",
                table: "student_enrollments",
                column: "academic_year_id",
                principalTable: "academic_years",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Recreating the old two-column unique index fails if the data has since used the
        /// freedom this migration granted — the same student in the same class across two
        /// sessions. That is the honest outcome: those rows cannot be represented by the old
        /// schema, and silently dropping one of them would be worse than refusing.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_student_enrollments_academic_years_academic_year_id",
                table: "student_enrollments");

            migrationBuilder.DropTable(
                name: "academic_years");

            migrationBuilder.DropIndex(
                name: "ix_student_enrollments_academic_year_id",
                table: "student_enrollments");

            migrationBuilder.DropIndex(
                name: "ix_student_enrollments_student_id_class_id_academic_year_id",
                table: "student_enrollments");

            migrationBuilder.DropColumn(
                name: "academic_year_id",
                table: "student_enrollments");

            migrationBuilder.CreateIndex(
                name: "ix_student_enrollments_student_id_class_id",
                table: "student_enrollments",
                columns: new[] { "student_id", "class_id" },
                unique: true);
        }
    }
}
