using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Introduces <c>departments</c> and turns <c>subjects</c> into <c>courses</c>.
    ///
    /// Hand-adjusted from the scaffolded version, which wanted to DROP subjects and
    /// CREATE courses — that discards every existing row, and the re-added foreign keys
    /// would then fail against any assignments still referencing the old ids. Renaming
    /// keeps the data and the references intact.
    /// </summary>
    public partial class AddDepartmentsRenameSubjectToCourse : Migration
    {
        /// <summary>Home for courses that predate departments, so the new FK can be required.</summary>
        private const string GeneralDepartmentId = "00000000-0000-0000-0000-00000000d001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── departments ──────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "departments",
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
                    table.PrimaryKey("pk_departments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_departments_code",
                table: "departments",
                column: "code",
                unique: true);

            // ── subjects → courses ───────────────────────────────────────────────
            // Drop the inbound FKs first so the table can be renamed, then re-add them
            // at the end under their new names.
            migrationBuilder.DropForeignKey(
                name: "fk_assignments_subjects_subject_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_teacher_assignments_subjects_subject_id",
                table: "teacher_assignments");

            migrationBuilder.RenameTable(name: "subjects", newName: "courses");
            migrationBuilder.Sql("ALTER TABLE courses RENAME CONSTRAINT pk_subjects TO pk_courses;");
            migrationBuilder.RenameIndex(
                name: "ix_subjects_code",
                table: "courses",
                newName: "ix_courses_code");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "teacher_assignments",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_teacher_assignments_teacher_id_subject_id_class_id",
                table: "teacher_assignments",
                newName: "ix_teacher_assignments_teacher_id_course_id_class_id");

            migrationBuilder.RenameIndex(
                name: "ix_teacher_assignments_subject_id",
                table: "teacher_assignments",
                newName: "ix_teacher_assignments_course_id");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "assignments",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_assignments_subject_id",
                table: "assignments",
                newName: "ix_assignments_course_id");

            migrationBuilder.RenameIndex(
                name: "ix_assignments_class_id_subject_id_status",
                table: "assignments",
                newName: "ix_assignments_class_id_course_id_status");

            // ── courses.department_id ────────────────────────────────────────────
            // Added nullable, backfilled, then tightened — a required column cannot be
            // added straight onto a table that may already hold rows.
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "courses",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($@"
                INSERT INTO departments (id, name, code, created_at_utc, updated_at_utc)
                SELECT '{GeneralDepartmentId}', 'General', 'GEN', now(), now()
                WHERE EXISTS (SELECT 1 FROM courses);

                UPDATE courses
                SET department_id = '{GeneralDepartmentId}'
                WHERE department_id IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "department_id",
                table: "courses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_courses_department_id",
                table: "courses",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "fk_courses_departments_department_id",
                table: "courses",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ── users: department + teacher id ───────────────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teacher_id",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_teacher_id",
                table: "users",
                column: "teacher_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_departments_department_id",
                table: "users",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ── inbound FKs, now pointing at courses ─────────────────────────────
            migrationBuilder.AddForeignKey(
                name: "fk_assignments_courses_course_id",
                table: "assignments",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teacher_assignments_courses_course_id",
                table: "teacher_assignments",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assignments_courses_course_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_teacher_assignments_courses_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_users_departments_department_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_courses_departments_department_id",
                table: "courses");

            migrationBuilder.DropIndex(name: "ix_users_department_id", table: "users");
            migrationBuilder.DropIndex(name: "ix_users_teacher_id", table: "users");
            migrationBuilder.DropColumn(name: "department_id", table: "users");
            migrationBuilder.DropColumn(name: "teacher_id", table: "users");

            migrationBuilder.DropIndex(name: "ix_courses_department_id", table: "courses");
            migrationBuilder.DropColumn(name: "department_id", table: "courses");

            migrationBuilder.DropTable(name: "departments");

            // ── courses → subjects ───────────────────────────────────────────────
            migrationBuilder.RenameTable(name: "courses", newName: "subjects");
            migrationBuilder.Sql("ALTER TABLE subjects RENAME CONSTRAINT pk_courses TO pk_subjects;");
            migrationBuilder.RenameIndex(
                name: "ix_courses_code",
                table: "subjects",
                newName: "ix_subjects_code");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "teacher_assignments",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_teacher_assignments_teacher_id_course_id_class_id",
                table: "teacher_assignments",
                newName: "ix_teacher_assignments_teacher_id_subject_id_class_id");

            migrationBuilder.RenameIndex(
                name: "ix_teacher_assignments_course_id",
                table: "teacher_assignments",
                newName: "ix_teacher_assignments_subject_id");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "assignments",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_assignments_course_id",
                table: "assignments",
                newName: "ix_assignments_subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_assignments_class_id_course_id_status",
                table: "assignments",
                newName: "ix_assignments_class_id_subject_id_status");

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_subjects_subject_id",
                table: "assignments",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teacher_assignments_subjects_subject_id",
                table: "teacher_assignments",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
