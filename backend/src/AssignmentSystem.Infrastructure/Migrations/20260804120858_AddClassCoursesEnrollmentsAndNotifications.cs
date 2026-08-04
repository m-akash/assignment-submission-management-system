using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Introduces course offerings (<c>class_courses</c>), student enrollments
    /// (<c>student_enrollments</c>) and the notification outbox (<c>notifications</c>), and
    /// repoints assignments and teaching mappings at the offering.
    ///
    /// Hand-ordered rather than left as scaffolded. The scaffolded version renamed
    /// <c>teacher_assignments.course_id</c> to <c>class_course_id</c> and
    /// <c>assignments.teacher_assignment_id</c> to <c>class_course_id</c> — keeping the old
    /// values in columns that now mean something else, so every row would have pointed at the
    /// wrong table. It also dropped <c>users.class_id</c> before anything could read it, which
    /// is the only place existing class membership was recorded.
    ///
    /// So the order below matters: create the new tables, derive the offerings from the pairs
    /// already in use, repoint the dependants, copy class membership into enrollments, and only
    /// then drop the old columns.
    /// </summary>
    public partial class AddClassCoursesEnrollmentsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. New tables ──────────────────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "class_courses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_courses", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_courses_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_class_courses_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Created before the backfill: the INSERT below relies on it for ON CONFLICT.
            migrationBuilder.CreateIndex(
                name: "ix_class_courses_class_id_course_id",
                table: "class_courses",
                columns: new[] { "class_id", "course_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_courses_course_id",
                table: "class_courses",
                column: "course_id");

            migrationBuilder.CreateTable(
                name: "student_enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrolled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_enrollments_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_student_enrollments_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_enrollments_student_id_class_id",
                table: "student_enrollments",
                columns: new[] { "student_id", "class_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_enrollments_student_id",
                table: "student_enrollments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_enrollments_class_id",
                table: "student_enrollments",
                column: "class_id");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_status_created_at_utc",
                table: "notifications",
                columns: new[] { "status", "created_at_utc" },
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id",
                table: "notifications",
                column: "recipient_id");

            // ── 2. Derive the offerings from the (class, course) pairs already in use ──
            // Both tables are read: a class might have an assignment for a course whose
            // teaching mapping has since been removed, or a mapping with no work set yet.
            // Soft-deleted assignments are included on purpose — their class_course_id still
            // has to be valid for the NOT NULL below, and for a restore to mean anything.
            migrationBuilder.Sql("""
                INSERT INTO class_courses (id, class_id, course_id, created_at_utc, updated_at_utc)
                SELECT gen_random_uuid(), pairs.class_id, pairs.course_id, now(), now()
                FROM (
                    SELECT class_id, course_id FROM teacher_assignments
                    UNION
                    SELECT class_id, course_id FROM assignments
                ) AS pairs
                ON CONFLICT (class_id, course_id) DO NOTHING;
                """);

            // ── 3. Repoint teaching mappings at the offering ──────────────────────
            // Added nullable, populated, then tightened — a NOT NULL column cannot be added to
            // a table that already has rows without inventing a default value for them.
            migrationBuilder.AddColumn<Guid>(
                name: "class_course_id",
                table: "teacher_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE teacher_assignments ta
                SET class_course_id = cc.id
                FROM class_courses cc
                WHERE cc.class_id = ta.class_id AND cc.course_id = ta.course_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "class_course_id",
                table: "teacher_assignments",
                type: "uuid",
                nullable: false);

            migrationBuilder.DropForeignKey(
                name: "fk_teacher_assignments_classes_class_id",
                table: "teacher_assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_teacher_assignments_courses_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_teacher_id_course_id_class_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_class_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropColumn(name: "class_id", table: "teacher_assignments");
            migrationBuilder.DropColumn(name: "course_id", table: "teacher_assignments");

            // ── 4. Repoint assignments at the offering ────────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "class_course_id",
                table: "assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE assignments a
                SET class_course_id = cc.id
                FROM class_courses cc
                WHERE cc.class_id = a.class_id AND cc.course_id = a.course_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "class_course_id",
                table: "assignments",
                type: "uuid",
                nullable: false);

            migrationBuilder.DropForeignKey(
                name: "fk_assignments_classes_class_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_assignments_courses_course_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_assignments_teacher_assignments_teacher_assignment_id",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_class_id_course_id_status",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_course_id",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_teacher_assignment_id",
                table: "assignments");

            migrationBuilder.DropColumn(name: "class_id", table: "assignments");
            migrationBuilder.DropColumn(name: "course_id", table: "assignments");

            // teacher_assignment_id goes too: authorship is assignments.teacher_id, and "may
            // this teacher set work for this offering?" is now answered by looking the mapping
            // up rather than by holding a reference to one an admin may later remove.
            migrationBuilder.DropColumn(name: "teacher_assignment_id", table: "assignments");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_class_course_id_status",
                table: "assignments",
                columns: new[] { "class_course_id", "status" });

            // ── 5. Move class membership into enrollments, then drop the column ────
            // users.class_id was only ever set for students, and created_at_utc is the closest
            // thing to a real join date the old shape recorded.
            migrationBuilder.Sql("""
                INSERT INTO student_enrollments (id, student_id, class_id, enrolled_at_utc, created_at_utc, updated_at_utc)
                SELECT gen_random_uuid(), u.id, u.class_id, COALESCE(u.created_at_utc, now()), now(), now()
                FROM users u
                WHERE u.class_id IS NOT NULL
                ON CONFLICT (student_id, class_id) DO NOTHING;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_users_classes_class_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_class_id",
                table: "users");

            migrationBuilder.DropColumn(name: "class_id", table: "users");

            // ── 6. New constraints ────────────────────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_teacher_id_class_course_id",
                table: "teacher_assignments",
                columns: new[] { "teacher_id", "class_course_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_class_course_id",
                table: "teacher_assignments",
                column: "class_course_id");

            migrationBuilder.AddForeignKey(
                name: "fk_teacher_assignments_class_courses_class_course_id",
                table: "teacher_assignments",
                column: "class_course_id",
                principalTable: "class_courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_class_courses_class_course_id",
                table: "assignments",
                column: "class_course_id",
                principalTable: "class_courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_users_teacher_id",
                table: "assignments",
                column: "teacher_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the old shape and copies the data back through the offering, so a
            // rollback keeps working assignments and class rosters rather than a valid schema
            // full of zero GUIDs.
            //
            // One thing it cannot restore: a student enrolled in more than one class collapses
            // back to a single users.class_id (their earliest enrollment is kept, the rest are
            // lost), because that is all the old column could hold.

            migrationBuilder.DropForeignKey(
                name: "fk_assignments_class_courses_class_course_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_assignments_users_teacher_id",
                table: "assignments");

            migrationBuilder.DropForeignKey(
                name: "fk_teacher_assignments_class_courses_class_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_teacher_id_class_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_class_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_class_course_id_status",
                table: "assignments");

            // ── users.class_id ────────────────────────────────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "class_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE users u
                SET class_id = earliest.class_id
                FROM (
                    SELECT DISTINCT ON (student_id) student_id, class_id
                    FROM student_enrollments
                    ORDER BY student_id, enrolled_at_utc
                ) AS earliest
                WHERE earliest.student_id = u.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_users_class_id",
                table: "users",
                column: "class_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_classes_class_id",
                table: "users",
                column: "class_id",
                principalTable: "classes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ── teacher_assignments (class_id, course_id) ─────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "class_id",
                table: "teacher_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "course_id",
                table: "teacher_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE teacher_assignments ta
                SET class_id = cc.class_id, course_id = cc.course_id
                FROM class_courses cc
                WHERE cc.id = ta.class_course_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "class_id", table: "teacher_assignments", type: "uuid", nullable: false);
            migrationBuilder.AlterColumn<Guid>(
                name: "course_id", table: "teacher_assignments", type: "uuid", nullable: false);

            // ── assignments (class_id, course_id, teacher_assignment_id) ──────────
            migrationBuilder.AddColumn<Guid>(
                name: "class_id", table: "assignments", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "course_id", table: "assignments", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "teacher_assignment_id", table: "assignments", type: "uuid", nullable: true);

            migrationBuilder.Sql("""
                UPDATE assignments a
                SET class_id = cc.class_id, course_id = cc.course_id
                FROM class_courses cc
                WHERE cc.id = a.class_course_id;
                """);

            // Re-attach each assignment to its author's mapping for the same offering. Any
            // assignment whose mapping was removed in the meantime has no row to point at, so
            // it takes the offering's earliest remaining mapping instead; one with no mapping
            // at all cannot be represented by the old NOT NULL column and is dropped.
            migrationBuilder.Sql("""
                UPDATE assignments a
                SET teacher_assignment_id = ta.id
                FROM teacher_assignments ta
                WHERE ta.class_course_id = a.class_course_id AND ta.teacher_id = a.teacher_id;

                UPDATE assignments a
                SET teacher_assignment_id = fallback.id
                FROM (
                    SELECT DISTINCT ON (class_course_id) class_course_id, id
                    FROM teacher_assignments
                    ORDER BY class_course_id, created_at_utc
                ) AS fallback
                WHERE fallback.class_course_id = a.class_course_id
                  AND a.teacher_assignment_id IS NULL;

                DELETE FROM assignments WHERE teacher_assignment_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "class_id", table: "assignments", type: "uuid", nullable: false);
            migrationBuilder.AlterColumn<Guid>(
                name: "course_id", table: "assignments", type: "uuid", nullable: false);
            migrationBuilder.AlterColumn<Guid>(
                name: "teacher_assignment_id", table: "assignments", type: "uuid", nullable: false);

            migrationBuilder.DropColumn(name: "class_course_id", table: "assignments");
            migrationBuilder.DropColumn(name: "class_course_id", table: "teacher_assignments");

            migrationBuilder.DropTable(name: "notifications");
            migrationBuilder.DropTable(name: "student_enrollments");
            migrationBuilder.DropTable(name: "class_courses");

            // ── Old indexes and foreign keys ──────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_teacher_id_course_id_class_id",
                table: "teacher_assignments",
                columns: new[] { "teacher_id", "course_id", "class_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_class_id",
                table: "teacher_assignments",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_course_id",
                table: "teacher_assignments",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_class_id_course_id_status",
                table: "assignments",
                columns: new[] { "class_id", "course_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_course_id",
                table: "assignments",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_teacher_assignment_id",
                table: "assignments",
                column: "teacher_assignment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_teacher_assignments_classes_class_id",
                table: "teacher_assignments",
                column: "class_id",
                principalTable: "classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_teacher_assignments_courses_course_id",
                table: "teacher_assignments",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_classes_class_id",
                table: "assignments",
                column: "class_id",
                principalTable: "classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_courses_course_id",
                table: "assignments",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_assignments_teacher_assignments_teacher_assignment_id",
                table: "assignments",
                column: "teacher_assignment_id",
                principalTable: "teacher_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
