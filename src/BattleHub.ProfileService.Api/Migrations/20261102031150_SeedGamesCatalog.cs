using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BattleHub.ProfileService.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedGamesCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "games",
                columns: new[] { "id", "enabled", "game_type", "name", "required_permission" },
                values: new object[,]
                {
                    { 1, true, "typing", "Typing Battle", "games.typing.play" },
                    { 2, true, "trivia", "Trivia Battle", "games.trivia.play" },
                    { 3, true, "memory", "Memory Match", "games.memory.play" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "games",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "games",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "games",
                keyColumn: "id",
                keyValue: 3);
        }
    }
}
