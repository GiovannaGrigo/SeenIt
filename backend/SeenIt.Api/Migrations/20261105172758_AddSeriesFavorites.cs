using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeenIt.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "UserSeries",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "UserSeries");
        }
    }
}
