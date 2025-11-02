using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Migrations
{
    /// <inheritdoc />
    public partial class ClipViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clip_view",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("clip_view_pkey", x => new { x.user_id, x.clip_id });
                    table.ForeignKey(
                        name: "fk_clip_view__clip",
                        column: x => x.clip_id,
                        principalTable: "clip",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clip_view__user",
                        column: x => x.user_id,
                        principalTable: "discord_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clip_view__clip_id",
                table: "clip_view",
                column: "clip_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clip_view");
        }
    }
}
