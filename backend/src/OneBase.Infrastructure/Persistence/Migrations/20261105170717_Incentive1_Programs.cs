using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Incentive1_Programs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "incentive");

            migrationBuilder.CreateTable(
                name: "Programs",
                schema: "incentive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Product = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductTypeIds = table.Column<long[]>(type: "bigint[]", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TiersJson = table.Column<string>(type: "jsonb", nullable: false),
                    MonthsJson = table.Column<string>(type: "jsonb", nullable: false),
                    StoreSetsJson = table.Column<string>(type: "jsonb", nullable: false),
                    WholesaleAgentIds = table.Column<long[]>(type: "bigint[]", nullable: false),
                    SeparateWholesale = table.Column<bool>(type: "boolean", nullable: false),
                    SvCopies = table.Column<bool>(type: "boolean", nullable: false),
                    SvSummary = table.Column<bool>(type: "boolean", nullable: false),
                    RmMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StaleData = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentivePrograms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouteTargets",
                schema: "incentive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    MarketName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    RouteSource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Sub = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Set = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Prize = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BattleLevel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    AvgMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentiveRouteTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteTargets_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "incentive",
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncentivePrograms_Code",
                schema: "incentive",
                table: "Programs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveRouteTargets_Program_Agent_Date_Market",
                schema: "incentive",
                table: "RouteTargets",
                columns: new[] { "ProgramId", "AgentId", "Date", "MarketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveRouteTargets_Program_Date",
                schema: "incentive",
                table: "RouteTargets",
                columns: new[] { "ProgramId", "Date" });

            // Акция Twizos по ТЗ «Проект 3» (27.10.2026) и файлу расчёта: 01.10–31.12.2026, Twizos — тип Linko 22 «Снек» с подтипами, ступени
            // 1 500 / 2 400 / 4 000 кг, ориентиры месяцев 20 / 35 / 45%, наборы магазинов по среднему закупу; оптовые ТП — 16 ТП с типом «опт»
            // файла расчёта (сопоставлены с Linko по имени: номер в файле — код RITM). Выключена — включает администратор.
            migrationBuilder.Sql("""
                INSERT INTO incentive."Programs" ("Id", "Code", "Name", "Product", "ProductTypeIds", "StartDate", "EndDate", "TiersJson", "MonthsJson",
                    "StoreSetsJson", "WholesaleAgentIds", "SeparateWholesale", "SvCopies", "SvSummary", "RmMode", "StaleData", "Enabled", "CreatedAt")
                VALUES ('019a54f0-5a00-7000-8000-000000000001', 'twizos-2026q4', 'Накопительная акция Twizos', 'Twizos', ARRAY[22]::bigint[],
                    DATE '2026-10-01', DATE '2026-12-31',
                    '[{"kg":1500,"prize":"Планшет Samsung","short":"планшет","of":"планшета","image":"tablet","prizeUz":"Samsung planshet","shortUz":"planshet"},
                      {"kg":2400,"prize":"Smart TV 55″","short":"телевизор","of":"телевизора","image":"tv","prizeUz":"Smart TV 55″","shortUz":"televizor"},
                      {"kg":4000,"prize":"iPhone 17","short":"iPhone","of":"iPhone","image":"phone","prizeUz":"iPhone 17","shortUz":"iPhone"}]'::jsonb,
                    '[{"year":2026,"month":10,"share":0.20},{"year":2026,"month":11,"share":0.35},{"year":2026,"month":12,"share":0.45}]'::jsonb,
                    '[{"minAvg":5000000,"contents":"140 г + 200 г","kg":7.64,"prize":"чайник Tefal","level":"600","contentsUz":"140 g + 200 g","prizeUz":"Tefal choynagi"},
                      {"minAvg":1500000,"contents":"140 г + 35 г","kg":4.62,"prize":"термос","level":"400","contentsUz":"140 g + 35 g","prizeUz":"termos"},
                      {"minAvg":500000,"contents":"140 г × 1 кор.","kg":3.64,"prize":"кружка","level":"300","contentsUz":"140 g × 1 quti","prizeUz":"krujka"},
                      {"minAvg":0,"contents":"35 г × 1 кор.","kg":0.98,"contentsUz":"35 g × 1 quti"}]'::jsonb,
                    ARRAY[583, 234, 370, 380, 361, 588, 589, 348, 403, 527, 511, 471, 479, 534, 437, 332]::bigint[],
                    true, true, true, 'summary', 'stamp', false, now())
                ON CONFLICT ("Code") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteTargets",
                schema: "incentive");

            migrationBuilder.DropTable(
                name: "Programs",
                schema: "incentive");
        }
    }
}
