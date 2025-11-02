using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Migrations
{
    /// <inheritdoc />
    public partial class Md5Hash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "md5_hash",
                table: "clip",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "md5_hash",
                table: "clip");
        }
    }
}
