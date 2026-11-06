using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeenIt.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesAnimeClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAnime",
                table: "UserSeries",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAnime",
                table: "UserSeries");
        }
    }
}
