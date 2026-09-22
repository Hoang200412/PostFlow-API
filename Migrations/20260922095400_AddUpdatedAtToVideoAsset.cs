using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtToVideoAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "VideoAssets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "VideoAssets");
        }
    }
}
