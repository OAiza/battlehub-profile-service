using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BattleHub.ProfileService.Api.Data.Migrations
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
                name: "permissions",
                columns: table => new
                {
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    granted_by_default = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.code);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    auth0_user_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    last_synced_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    required_permission_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.code);
                    table.ForeignKey(
                        name: "fk_games_required_permission",
                        column: x => x.required_permission_code,
                        principalTable: "permissions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_profile_permissions",
                columns: table => new
                {
                    user_profile_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    permission_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    granted_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profile_permissions", x => new { x.user_profile_id, x.permission_code });
                    table.ForeignKey(
                        name: "fk_user_profile_permissions_permission",
                        column: x => x.permission_code,
                        principalTable: "permissions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_profile_permissions_user_profile",
                        column: x => x.user_profile_id,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "code", "description", "granted_by_default" },
                values: new object[,]
                {
                    { "games.memory.play", "Jugar Memory Match", true },
                    { "games.trivia.play", "Jugar Trivia Battle", true },
                    { "games.typing.play", "Jugar Typing Battle", true },
                    { "matches.create", "Crear partidas en el lobby de Matchmaking", true }
                });

            migrationBuilder.InsertData(
                table: "games",
                columns: new[] { "code", "description", "is_active", "name", "required_permission_code", "sort_order" },
                values: new object[,]
                {
                    { "memory", "Juego de memoria: encontrar los pares en el menor tiempo posible.", true, "Memory Match", "games.memory.play", 3 },
                    { "trivia", "Preguntas y respuestas por categorías contra otros jugadores.", true, "Trivia Battle", "games.trivia.play", 2 },
                    { "typing", "Competencia de velocidad y precisión de escritura.", true, "Typing Battle", "games.typing.play", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_games_required_permission_code",
                table: "games",
                column: "required_permission_code");

            migrationBuilder.CreateIndex(
                name: "ix_permissions_granted_by_default",
                table: "permissions",
                column: "granted_by_default");

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_permissions_permission_code",
                table: "user_profile_permissions",
                column: "permission_code");

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_auth0_user_id",
                table: "user_profiles",
                column: "auth0_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_email",
                table: "user_profiles",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "user_profile_permissions");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "user_profiles");
        }
    }
}
