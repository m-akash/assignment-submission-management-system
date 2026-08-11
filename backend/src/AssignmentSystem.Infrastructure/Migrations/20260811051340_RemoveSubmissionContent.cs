using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Students hand in files, not prose: the editor is gone from their screen, so a
    /// submission is its attachments and nothing else. This drops the text column that
    /// backed the old written answer.
    ///
    /// Destructive by nature — any answer typed before this point goes with it, which is
    /// the intent. <c>Down</c> puts the (empty) column back so the migration is reversible
    /// in shape, not in data.
    /// </summary>
    public partial class RemoveSubmissionContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content",
                table: "submissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content",
                table: "submissions",
                type: "text",
                nullable: true);
        }
    }
}
