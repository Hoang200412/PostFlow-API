using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyAiLimitOverride",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "Trial");

            migrationBuilder.AddColumn<string>(
                name: "LastTokenError",
                table: "SocialAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReconnect",
                table: "SocialAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenErrorAt",
                table: "SocialAccounts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AdminId = table.Column<string>(type: "text", nullable: false),
                    AdminUsername = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<string>(type: "text", nullable: true),
                    TargetLabel = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDailyAiUsages",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestCount = table.Column<int>(type: "integer", nullable: false),
                    BonusCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyAiUsages", x => new { x.UserId, x.UsageDate });
                    table.ForeignKey(
                        name: "FK_UserDailyAiUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Seed hạn mức gọi AI mỗi ngày theo gói (-1 = không giới hạn)
            migrationBuilder.Sql(@"
                INSERT INTO ""SystemSettings"" (""Key"", ""Value"", ""Description"") VALUES
                ('AI_DAILY_LIMIT_TRIAL', '100', 'Số lượt gọi AI tối đa mỗi ngày cho gói Trial (-1 = không giới hạn)'),
                ('AI_DAILY_LIMIT_PRO', '-1', 'Số lượt gọi AI tối đa mỗi ngày cho gói Pro (-1 = không giới hạn)')
                ON CONFLICT (""Key"") DO NOTHING;");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminId",
                table: "AdminAuditLogs",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAt",
                table: "AdminAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_TargetType_TargetId",
                table: "AdminAuditLogs",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""SystemSettings"" WHERE ""Key"" IN ('AI_DAILY_LIMIT_TRIAL', 'AI_DAILY_LIMIT_PRO');");

            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "UserDailyAiUsages");

            migrationBuilder.DropColumn(
                name: "DailyAiLimitOverride",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastTokenError",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "NeedsReconnect",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "TokenErrorAt",
                table: "SocialAccounts");
        }
    }
}
