using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrendChannels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: false),
                    SubscriberCount = table.Column<long>(type: "bigint", nullable: true),
                    VideoCount = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrendCollectionRuns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    CategorySlug = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    VideosFound = table.Column<int>(type: "integer", nullable: false),
                    VideosNew = table.Column<int>(type: "integer", nullable: false),
                    SnapshotsWritten = table.Column<int>(type: "integer", nullable: false),
                    QuotaUnitsUsed = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendCollectionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrendVideos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    EmbedUrl = table.Column<string>(type: "text", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    TrendChannelId = table.Column<string>(type: "text", nullable: true),
                    ChannelExternalId = table.Column<string>(type: "text", nullable: false),
                    ChannelTitle = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsShort = table.Column<bool>(type: "boolean", nullable: false),
                    CategorySlug = table.Column<string>(type: "text", nullable: false),
                    PlatformCategoryId = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    InPopularChart = table.Column<bool>(type: "boolean", nullable: false),
                    InViralSample = table.Column<bool>(type: "boolean", nullable: false),
                    InControlSample = table.Column<bool>(type: "boolean", nullable: false),
                    DataSource = table.Column<string>(type: "text", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "text", nullable: true),
                    IsTracking = table.Column<bool>(type: "boolean", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastSnapshotAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LatestViews = table.Column<long>(type: "bigint", nullable: true),
                    LatestLikes = table.Column<long>(type: "bigint", nullable: true),
                    LatestComments = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrendVideos_TrendChannels_TrendChannelId",
                        column: x => x.TrendChannelId,
                        principalTable: "TrendChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrendVideoSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TrendVideoId = table.Column<string>(type: "text", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AgeHours = table.Column<double>(type: "double precision", nullable: true),
                    MilestoneHours = table.Column<int>(type: "integer", nullable: true),
                    Views = table.Column<long>(type: "bigint", nullable: true),
                    Likes = table.Column<long>(type: "bigint", nullable: true),
                    Comments = table.Column<long>(type: "bigint", nullable: true),
                    DataSource = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrendVideoSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrendVideoSnapshots_TrendVideos_TrendVideoId",
                        column: x => x.TrendVideoId,
                        principalTable: "TrendVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrendChannels_Platform_ExternalId",
                table: "TrendChannels",
                columns: new[] { "Platform", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrendCollectionRuns_StartedAt",
                table: "TrendCollectionRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrendVideos_IsTracking_PublishedAt",
                table: "TrendVideos",
                columns: new[] { "IsTracking", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendVideos_Platform_CategorySlug_PublishedAt",
                table: "TrendVideos",
                columns: new[] { "Platform", "CategorySlug", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TrendVideos_Platform_ExternalId",
                table: "TrendVideos",
                columns: new[] { "Platform", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrendVideos_TrendChannelId",
                table: "TrendVideos",
                column: "TrendChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_TrendVideoSnapshots_TrendVideoId_CapturedAt",
                table: "TrendVideoSnapshots",
                columns: new[] { "TrendVideoId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrendCollectionRuns");

            migrationBuilder.DropTable(
                name: "TrendVideoSnapshots");

            migrationBuilder.DropTable(
                name: "TrendVideos");

            migrationBuilder.DropTable(
                name: "TrendChannels");
        }
    }
}
