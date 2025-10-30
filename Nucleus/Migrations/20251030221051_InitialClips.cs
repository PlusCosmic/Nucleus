using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Migrations
{
    /// <inheritdoc />
    public partial class InitialClips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clip",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    video_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("clip_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clip_collection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("clip_collection_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_clip_collection__discord_user",
                        column: x => x.owner_id,
                        principalTable: "discord_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clip_collection__owner_id",
                table: "clip_collection",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clip");

            migrationBuilder.DropTable(
                name: "clip_collection");
        }
    }
}
