using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// A grade may hold any number of sections, but only one cohort per (grade, section).
    /// Two data fix-ups have to run before the unique index can be created:
    /// existing duplicates are renamed rather than deleted (a class cascades to its
    /// enrollments and offerings, so deleting one would take real records with it), and
    /// names are recomposed now that the domain derives them from grade + section.
    /// </summary>
    /// <inheritdoc />
    public partial class EnforceUniqueClassGradeSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the oldest cohort in each (grade, section) slot as-is and push the rest out
            // of the way. The id fragment guarantees the rescued section is unique and keeps the
            // result inside section's 50-character limit.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY level, lower(section)
                               ORDER BY created_at_utc, id
                           ) AS rn
                    FROM classes
                    WHERE section IS NOT NULL
                )
                UPDATE classes c
                SET section = left(c.section, 41) || '-' || left(c.id::text, 8)
                FROM ranked r
                WHERE c.id = r.id AND r.rn > 1;
                """);

            // Names are no longer entered by an admin; rewrite them the way Class.BuildName does.
            // Rows with no section predate the required-section rule and cannot be composed —
            // they keep whatever name they were given.
            migrationBuilder.Sql("""
                UPDATE classes
                SET name = 'Class '
                    || (ARRAY['I','II','III','IV','V','VI','VII','VIII','IX','X','XI','XII'])[level]
                    || ' - Section ' || section
                WHERE section IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_classes_level_section",
                table: "classes",
                columns: new[] { "level", "section" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the index comes back off. The section and name rewrites above are not
            // reversible — the values they replaced are not recorded anywhere.
            migrationBuilder.DropIndex(
                name: "ix_classes_level_section",
                table: "classes");
        }
    }
}
