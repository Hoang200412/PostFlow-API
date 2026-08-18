using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddUsecaseEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentDNAId",
                table: "VideoAssets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorDetails",
                table: "UploadJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAnalyticsAt",
                table: "UploadJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "UploadJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformMetadataOverride",
                table: "UploadJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlatformPostId",
                table: "UploadJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "UploadJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "UploadJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Analytics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UploadJobId = table.Column<string>(type: "text", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false),
                    Likes = table.Column<int>(type: "integer", nullable: false),
                    Shares = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<int>(type: "integer", nullable: false),
                    Saves = table.Column<int>(type: "integer", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Analytics_UploadJobs_UploadJobId",
                        column: x => x.UploadJobId,
                        principalTable: "UploadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiUsageLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    CostEstimate = table.Column<decimal>(type: "numeric", nullable: false),
                    CallCount = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiUsageLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentDNAs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BrandName = table.Column<string>(type: "text", nullable: false),
                    VoiceProfile = table.Column<string>(type: "text", nullable: false),
                    AvatarAssetPath = table.Column<string>(type: "text", nullable: false),
                    ScriptStructure = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDNAs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentDNAs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssets_ContentDNAId",
                table: "VideoAssets",
                column: "ContentDNAId");

            migrationBuilder.CreateIndex(
                name: "IX_Analytics_UploadJobId",
                table: "Analytics",
                column: "UploadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiUsageLogs_UserId",
                table: "ApiUsageLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDNAs_UserId",
                table: "ContentDNAs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoAssets_ContentDNAs_ContentDNAId",
                table: "VideoAssets",
                column: "ContentDNAId",
                principalTable: "ContentDNAs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoAssets_ContentDNAs_ContentDNAId",
                table: "VideoAssets");

            migrationBuilder.DropTable(
                name: "Analytics");

            migrationBuilder.DropTable(
                name: "ApiUsageLogs");

            migrationBuilder.DropTable(
                name: "ContentDNAs");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_VideoAssets_ContentDNAId",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "ContentDNAId",
                table: "VideoAssets");

            migrationBuilder.DropColumn(
                name: "ErrorDetails",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "LastAnalyticsAt",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "PlatformMetadataOverride",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "PlatformPostId",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "UploadJobs");
        }
    }
}
