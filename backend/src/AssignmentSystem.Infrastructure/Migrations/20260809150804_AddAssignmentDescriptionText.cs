using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Descriptions became HTML when the rich-text editor landed, which broke search: matching
    /// against markup makes "li" find every assignment that happens to be written as a list.
    /// This adds the stripped copy that search now runs against instead.
    ///
    /// The database generates it rather than the application writing it, so there is no backfill
    /// step and no way for the two columns to drift — PostgreSQL computes it for every existing
    /// row as the column is added, including descriptions that predate the editor entirely.
    /// </summary>
    public partial class AddAssignmentDescriptionText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description_text",
                table: "assignments",
                type: "text",
                nullable: false,
                computedColumnSql: "replace(replace(replace(replace(regexp_replace(description, '<[^>]*>', ' ', 'g'), '&nbsp;', ' '), '&lt;', '<'), '&gt;', '>'), '&amp;', '&')",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description_text",
                table: "assignments");
        }
    }
}
