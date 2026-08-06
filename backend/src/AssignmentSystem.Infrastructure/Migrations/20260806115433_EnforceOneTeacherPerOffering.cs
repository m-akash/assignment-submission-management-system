using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOneTeacherPerOffering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_class_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_teacher_id_class_course_id",
                table: "teacher_assignments");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_class_course_id",
                table: "teacher_assignments",
                column: "class_course_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_teacher_id",
                table: "teacher_assignments",
                column: "teacher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_class_course_id",
                table: "teacher_assignments");

            migrationBuilder.DropIndex(
                name: "ix_teacher_assignments_teacher_id",
                table: "teacher_assignments");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_class_course_id",
                table: "teacher_assignments",
                column: "class_course_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_assignments_teacher_id_class_course_id",
                table: "teacher_assignments",
                columns: new[] { "teacher_id", "class_course_id" },
                unique: true);
        }
    }
}
