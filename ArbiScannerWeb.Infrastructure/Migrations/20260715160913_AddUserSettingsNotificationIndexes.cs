using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArbiScannerWeb.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettingsNotificationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_AccountId",
                table: "UserSettings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Active_Funding_SpreadSize",
                table: "UserSettings",
                column: "SpreadSize",
                filter: "\"Active\" = true AND \"FundingSpread\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Active_Futures_SpreadSize",
                table: "UserSettings",
                column: "SpreadSize",
                filter: "\"Active\" = true AND \"FuturesSpread\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_Active_Spot_SpreadSize",
                table: "UserSettings",
                column: "SpreadSize",
                filter: "\"Active\" = true AND \"SpotSpread\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_ChatId",
                table: "UserSettings",
                column: "ChatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSettings_AccountId",
                table: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserSettings_Active_Funding_SpreadSize",
                table: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserSettings_Active_Futures_SpreadSize",
                table: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserSettings_Active_Spot_SpreadSize",
                table: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserSettings_ChatId",
                table: "UserSettings");
        }
    }
}
