using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeenIt.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    AvatarFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EpisodiosAssistidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSeriesId = table.Column<int>(type: "integer", nullable: false),
                    ExternalEpisodeId = table.Column<int>(type: "integer", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    NumeroEpisodio = table.Column<int>(type: "integer", nullable: false),
                    NomeEpisodio = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ExternalCharacterId = table.Column<int>(type: "integer", nullable: false),
                    PersonagemFavoritoNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssistidoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodiosAssistidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpisodiosAssistidos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSeriesId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PosterUrl = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Summary = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSeries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EpisodiosAssistidosSentimentos",
                columns: table => new
                {
                    EpisodioAssistidoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sentimento = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodiosAssistidosSentimentos", x => new { x.EpisodioAssistidoId, x.Sentimento });
                    table.ForeignKey(
                        name: "FK_EpisodiosAssistidosSentimentos_EpisodiosAssistidos_Episodio~",
                        column: x => x.EpisodioAssistidoId,
                        principalTable: "EpisodiosAssistidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EpisodiosAssistidos_UserId_ExternalEpisodeId",
                table: "EpisodiosAssistidos",
                columns: new[] { "UserId", "ExternalEpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSeries_UserId_ExternalSeriesId",
                table: "UserSeries",
                columns: new[] { "UserId", "ExternalSeriesId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EpisodiosAssistidosSentimentos");

            migrationBuilder.DropTable(
                name: "UserSeries");

            migrationBuilder.DropTable(
                name: "EpisodiosAssistidos");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
