using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BattleHub.ProfileService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    game_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    required_permission = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    enabled = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.game_type);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.code);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "profile_permissions",
                columns: table => new
                {
                    PermissionsCode = table.Column<string>(type: "varchar(100)", nullable: false),
                    ProfilesId = table.Column<string>(type: "varchar(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_permissions", x => new { x.PermissionsCode, x.ProfilesId });
                    table.ForeignKey(
                        name: "FK_profile_permissions_permissions_PermissionsCode",
                        column: x => x.PermissionsCode,
                        principalTable: "permissions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_profile_permissions_profiles_ProfilesId",
                        column: x => x.ProfilesId,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "games",
                columns: new[] { "game_type", "enabled", "name", "required_permission" },
                values: new object[,]
                {
                    { "memory", true, "Memory Match", "games.memory.play" },
                    { "trivia", true, "Trivia Battle", "games.trivia.play" },
                    { "typing", true, "Typing Battle", "games.typing.play" }
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "code", "description" },
                values: new object[,]
                {
                    { "games.memory.play", "Jugar a Memory Match" },
                    { "games.trivia.play", "Jugar a Trivia Battle" },
                    { "games.typing.play", "Jugar a Typing Battle" },
                    { "matches.create", "Crear salas de partidas" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_permissions_ProfilesId",
                table: "profile_permissions",
                column: "ProfilesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "profile_permissions");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "profiles");
        }
    }
}
