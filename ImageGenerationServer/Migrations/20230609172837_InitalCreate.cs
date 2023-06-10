using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageGenerationServer.Migrations
{
    /// <inheritdoc />
    public partial class InitalCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingVerifyPhrases",
                columns: table => new
                {
                    Phrase = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingVerifyPhrases", x => x.Phrase);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingVerifyPhrases");
        }
    }
}
