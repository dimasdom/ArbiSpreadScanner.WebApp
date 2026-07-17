using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArbiScannerWeb.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVolatility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SpreadsTicker");
            migrationBuilder.DropTable(name: "CurrentSpreads");
            migrationBuilder.DropTable(name: "ExchangeRates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Exchange = table.Column<string>(type: "text", nullable: false),
                    ExchangeRate = table.Column<double>(type: "double precision", nullable: false),
                    VolumeAsk = table.Column<double>(type: "double precision", nullable: false),
                    VolumeBid = table.Column<double>(type: "double precision", nullable: false),
                    SlippageLong = table.Column<double>(type: "double precision", nullable: false),
                    SlippageShort = table.Column<double>(type: "double precision", nullable: false),
                    SummarySlipage = table.Column<double>(type: "double precision", nullable: false),
                    FundingRateValue = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurrentSpreads",
                columns: table => new
                {
                    Guid = table.Column<Guid>(type: "uuid", nullable: false),
                    ExchangeRateAId = table.Column<int>(type: "integer", nullable: false),
                    ExchangeRateBId = table.Column<int>(type: "integer", nullable: false),
                    ExchangeShortId = table.Column<int>(type: "integer", nullable: false),
                    ExchangeLongId = table.Column<int>(type: "integer", nullable: false),
                    SummaryTarrif = table.Column<double>(type: "double precision", nullable: false),
                    PossibleProfit = table.Column<double>(type: "double precision", nullable: false),
                    TotalFunding = table.Column<double>(type: "double precision", nullable: false),
                    Spread = table.Column<double>(type: "double precision", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    StartSpread = table.Column<double>(type: "double precision", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    BidsExchangeA = table.Column<string>(type: "jsonb", nullable: true),
                    AsksExchangeA = table.Column<string>(type: "jsonb", nullable: true),
                    BidsExchangeB = table.Column<string>(type: "jsonb", nullable: true),
                    AsksExchangeB = table.Column<string>(type: "jsonb", nullable: true),
                    DateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentSpreads", x => x.Guid);
                    table.ForeignKey(
                        name: "FK_CurrentSpreads_ExchangeRates_ExchangeLongId",
                        column: x => x.ExchangeLongId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrentSpreads_ExchangeRates_ExchangeRateAId",
                        column: x => x.ExchangeRateAId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrentSpreads_ExchangeRates_ExchangeRateBId",
                        column: x => x.ExchangeRateBId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrentSpreads_ExchangeRates_ExchangeShortId",
                        column: x => x.ExchangeShortId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpreadsTicker",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Guid = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Spread = table.Column<double>(type: "double precision", nullable: false),
                    EchangeA = table.Column<string>(type: "text", nullable: false),
                    ExchangeB = table.Column<string>(type: "text", nullable: false),
                    ExchangeLong = table.Column<string>(type: "text", nullable: false),
                    ExchangeShort = table.Column<string>(type: "text", nullable: false),
                    RateA = table.Column<double>(type: "double precision", nullable: false),
                    RateB = table.Column<double>(type: "double precision", nullable: false),
                    DateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BidsExchangeA = table.Column<string>(type: "jsonb", nullable: true),
                    AsksExchangeA = table.Column<string>(type: "jsonb", nullable: true),
                    BidsExchangeB = table.Column<string>(type: "jsonb", nullable: true),
                    AsksExchangeB = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpreadsTicker", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpreadsTicker_CurrentSpreads_Guid",
                        column: x => x.Guid,
                        principalTable: "CurrentSpreads",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrentSpreads_ExchangeLongId",
                table: "CurrentSpreads",
                column: "ExchangeLongId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrentSpreads_ExchangeRateAId",
                table: "CurrentSpreads",
                column: "ExchangeRateAId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrentSpreads_ExchangeRateBId",
                table: "CurrentSpreads",
                column: "ExchangeRateBId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrentSpreads_ExchangeShortId",
                table: "CurrentSpreads",
                column: "ExchangeShortId");

            migrationBuilder.CreateIndex(
                name: "IX_SpreadsTicker_Guid",
                table: "SpreadsTicker",
                column: "Guid");
        }
    }
}
