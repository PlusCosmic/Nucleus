using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Migrations
{
    /// <inheritdoc />
    public partial class Tags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Avatar",
                table: "discord_user",
                newName: "avatar");

            migrationBuilder.CreateTable(
                name: "tag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tag_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clip_tag",
                columns: table => new
                {
                    clip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("clip_tag_pkey", x => new { x.clip_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_clip_tag__clip",
                        column: x => x.clip_id,
                        principalTable: "clip",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clip_tag__tag",
                        column: x => x.tag_id,
                        principalTable: "tag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clip_tag__tag_id",
                table: "clip_tag",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "uq_tag__name",
                table: "tag",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clip_tag");

            migrationBuilder.DropTable(
                name: "tag");

            migrationBuilder.RenameColumn(
                name: "avatar",
                table: "discord_user",
                newName: "Avatar");
        }
    }
}
