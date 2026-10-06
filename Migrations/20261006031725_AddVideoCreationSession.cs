using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoCreationSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoCreationSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    IdeaPrompt = table.Column<string>(type: "text", nullable: false),
                    SelectedVoice = table.Column<string>(type: "text", nullable: false),
                    SelectedStructure = table.Column<string>(type: "text", nullable: false),
                    SelectedTone = table.Column<string>(type: "text", nullable: false),
                    SelectedDuration = table.Column<string>(type: "text", nullable: false),
                    ScriptJson = table.Column<string>(type: "text", nullable: true),
                    CreatedVideoId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCreationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoCreationSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoCreationSessions_VideoAssets_CreatedVideoId",
                        column: x => x.CreatedVideoId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoCreationSessions_CreatedVideoId",
                table: "VideoCreationSessions",
                column: "CreatedVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoCreationSessions_UserId",
                table: "VideoCreationSessions",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoCreationSessions");
        }
    }
}
