using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Migrations
{
    /// <summary>
    /// Splits a class into its two real values. The composed <c>name</c> column
    /// ("Class IX - Section A") is dropped: grade and section are already stored beside it,
    /// so the column was a denormalised third copy that could only ever drift from them, and
    /// every caller now reads the pair.
    ///
    /// Student ids carried the same numeral ("IX-A-003"), because the prefix was built from
    /// the class. They are rewritten to the numeric grade ("9-A-003") so the ids issued
    /// before and after this migration read the same way — leaving them alone would split the
    /// roster into two id formats, and the sequence is issued per prefix, so the two would
    /// each start counting from 001.
    /// </summary>
    public partial class SplitClassGradeAndSection : Migration
    {
        /// <summary>
        /// The numeral↔number pairs, as a VALUES list usable from both directions. Matching
        /// requires the numeral to be followed by "-", so exactly one row can match a given id:
        /// "IX-A-003" cannot also match "I" (the next character is "X", not "-").
        /// </summary>
        private const string NumeralPairs =
            "(VALUES ('I',1),('II',2),('III',3),('IV',4),('V',5),('VI',6),"
            + "('VII',7),('VIII',8),('IX',9),('X',10),('XI',11),('XII',12)) AS m(numeral, grade)";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_classes_name",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "name",
                table: "classes");

            migrationBuilder.Sql($"""
                UPDATE users u
                SET student_id = m.grade::text || substring(u.student_id FROM char_length(m.numeral) + 1)
                FROM {NumeralPairs}
                WHERE u.student_id IS NOT NULL
                  AND u.student_id LIKE m.numeral || '-%';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE users u
                SET student_id = m.numeral || substring(u.student_id FROM char_length(m.grade::text) + 1)
                FROM {NumeralPairs}
                WHERE u.student_id IS NOT NULL
                  AND u.student_id LIKE m.grade::text || '-%';
                """);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "classes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            // Rebuilt from the pair it was composed from, so a rolled-back database is not
            // left with a table of empty names.
            migrationBuilder.Sql($"""
                UPDATE classes c
                SET name = 'Class ' || m.numeral || ' - Section ' || coalesce(c.section, '')
                FROM {NumeralPairs}
                WHERE c.level = m.grade;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_classes_name",
                table: "classes",
                column: "name");
        }
    }
}
