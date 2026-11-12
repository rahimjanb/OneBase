using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Incentive3_NovemberPromos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvgCheck",
                schema: "incentive",
                table: "RouteTargets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Placement",
                schema: "incentive",
                table: "RouteTargets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "route");

            migrationBuilder.AddColumn<bool>(
                name: "PlanAdded",
                schema: "incentive",
                table: "RouteTargets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Sets",
                schema: "incentive",
                table: "RouteTargets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Sleeping",
                schema: "incentive",
                table: "RouteTargets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PromosJson",
                schema: "incentive",
                table: "Programs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.CreateTable(
                name: "DayResults",
                schema: "incentive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Wholesale = table.Column<bool>(type: "boolean", nullable: false),
                    TwizosKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    OutOfRouteKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Sets = table.Column<int>(type: "integer", nullable: false),
                    Mugs = table.Column<int>(type: "integer", nullable: false),
                    Thermoses = table.Column<int>(type: "integer", nullable: false),
                    Kettles = table.Column<int>(type: "integer", nullable: false),
                    SleepingPlanned = table.Column<int>(type: "integer", nullable: false),
                    SleepingWoken = table.Column<int>(type: "integer", nullable: false),
                    Cashback = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BestPlace = table.Column<int>(type: "integer", nullable: true),
                    BestWinner = table.Column<bool>(type: "boolean", nullable: false),
                    BestPrize = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentiveDayResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DayResults_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "incentive",
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payouts",
                schema: "incentive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PaidById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentivePayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payouts_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "incentive",
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SleepingMarkets",
                schema: "incentive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    ListedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RemovedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FilePriority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentiveSleepingMarkets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SleepingMarkets_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "incentive",
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveDayResults_Program_Agent_Date",
                schema: "incentive",
                table: "DayResults",
                columns: new[] { "ProgramId", "AgentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveDayResults_Program_Date",
                schema: "incentive",
                table: "DayResults",
                columns: new[] { "ProgramId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_IncentivePayouts_Program_Agent_Week_Kind",
                schema: "incentive",
                table: "Payouts",
                columns: new[] { "ProgramId", "AgentId", "WeekStart", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveSleepingMarkets_Program_Market",
                schema: "incentive",
                table: "SleepingMarkets",
                columns: new[] { "ProgramId", "MarketId" },
                unique: true);

            // Акции ноября (ТЗ «Карточки ТК и расчёт целей по акциям ноября», 06.11.2026) — в той же строке акции Twizos, без второй акции: YM-01/02
            // (спящие Yamelly 06–30.11: скидка 7%, кэшбек 10%, 2 коробки одной фасовки), TW-01 (лучший агент дня: 6 / 30 кг, 500 000), TW-02 (подарок по
            // сумме Twizos в накладной: 288 600 кружка, 392 200 термос, 584 600 чайник — бонусные товары Linko 297 / 136 / 333; наборы без 200 г),
            // TW-03 (приз для расчёта цели — телефон; пороги опта 1 600 / 2 500 / 4 100), YM-03 (территории). Акция по-прежнему выключена.
            migrationBuilder.Sql("""
                UPDATE incentive."Programs" SET
                    "PromosJson" = '{"twizos":{"gifts":[{"minSum":288600,"gift":"mug","name":"кружка","nameUz":"krujka","productIds":[297],"stock":2000},{"minSum":392200,"gift":"thermos","name":"термос","nameUz":"termos","productIds":[136],"stock":2000},{"minSum":584600,"gift":"kettle","name":"чайник","nameUz":"choynak","productIds":[333],"stock":1000}],"pairs":[{"maxCheck":600000,"sets":["T","K140"]},{"maxCheck":1200000,"sets":["C","K140"]},{"maxCheck":null,"sets":["C","T"]}],"startSet":"K140","smallStartSet":"K35","smallCheck":250000,"minCheck":60000,"pairNewMinCheck":300000,"reserve":1.3,"conversion":0.4,"extraMax":5,"checkDays":90,"topDays":90,"targetPrize":"phone","extraPairs":[{"maxCheck":1200000,"sets":["T","K140"]},{"maxCheck":null,"sets":["C","K140"]}]},"yamelly":{"start":"2026-11-06","end":"2026-11-30","productTypeIds":[8],"packings":[{"unitKg":0.42,"perBox":8,"name":"420 г"},{"unitKg":1,"perBox":4,"name":"1 кг"}],"product":"Yamelly","discountPercent":7,"cashbackPercent":10,"minBoxes":2,"wakeShare":0.3,"minCashback":26338},"best":{"start":"2026-11-06","end":"2026-11-30","retailMinKg":6,"wholesaleMinKg":30,"prize":500000},"territory":{"start":"2026-11-01","end":"2026-11-30","territories":[{"name":"Самарканд","branches":["Самарканд"],"planKg":22500},{"name":"Кибрай","branches":["Кибрай"],"planKg":600},{"name":"Олмалык","branches":["Олмалик","AAA"],"planKg":730},{"name":"Пскент","branches":["Пскент"],"planKg":600}],"productTypeIds":[4,5,6,7,8,9,10],"threshold":0.85,"agentMinKg":50,"agentPrize":1000000,"supervisorPrize":2000000},"budget":{"cashback":0,"best":0,"territory":0}}'::jsonb,
                    "StoreSetsJson" = '[{"minAvg":0,"contents":"35 г × 3 кор.","kg":2.94,"prize":"кружка","contentsUz":"35 g × 3 quti","prizeUz":"krujka","code":"K35","sum":310800,"gift":"mug"},{"minAvg":0,"contents":"140 г × 1 кор.","kg":3.64,"prize":"кружка","contentsUz":"140 g × 1 quti","prizeUz":"krujka","code":"K140","sum":288600,"gift":"mug"},{"minAvg":0,"contents":"140 г ×1 + 35 г ×1","kg":4.62,"prize":"термос","contentsUz":"140 g ×1 + 35 g ×1","prizeUz":"termos","code":"T","sum":392200,"gift":"thermos"},{"minAvg":0,"contents":"140 г ×1 + 35 г ×3","kg":6.58,"prize":"чайник","contentsUz":"140 g ×1 + 35 g ×3","prizeUz":"choynak","code":"C","sum":599400,"gift":"kettle"}]'::jsonb,
                    "TiersJson" = (SELECT jsonb_agg(CASE t->>'image'
                            WHEN 'tablet' THEN t || '{"wholesaleKg":1600}'::jsonb
                            WHEN 'tv' THEN t || '{"wholesaleKg":2500}'::jsonb
                            WHEN 'phone' THEN t || '{"wholesaleKg":4100}'::jsonb
                            ELSE t END ORDER BY (t->>'kg')::numeric)
                        FROM jsonb_array_elements("TiersJson") t)
                WHERE "Code" = 'twizos-2026q4';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Наборы акции Twizos — как до акций ноября (по среднему закупу), пороги опта у ступеней убираются.
            migrationBuilder.Sql("""
                UPDATE incentive."Programs" SET
                    "StoreSetsJson" = '[{"minAvg":5000000,"contents":"140 г + 200 г","kg":7.64,"prize":"чайник Tefal","level":"600","contentsUz":"140 g + 200 g","prizeUz":"Tefal choynagi"},
                      {"minAvg":1500000,"contents":"140 г + 35 г","kg":4.62,"prize":"термос","level":"400","contentsUz":"140 g + 35 g","prizeUz":"termos"},
                      {"minAvg":500000,"contents":"140 г × 1 кор.","kg":3.64,"prize":"кружка","level":"300","contentsUz":"140 g × 1 quti","prizeUz":"krujka"},
                      {"minAvg":0,"contents":"35 г × 1 кор.","kg":0.98,"contentsUz":"35 g × 1 quti"}]'::jsonb,
                    "TiersJson" = (SELECT jsonb_agg(t - 'wholesaleKg' ORDER BY (t->>'kg')::numeric) FROM jsonb_array_elements("TiersJson") t)
                WHERE "Code" = 'twizos-2026q4';
                """);

            migrationBuilder.DropTable(
                name: "DayResults",
                schema: "incentive");

            migrationBuilder.DropTable(
                name: "Payouts",
                schema: "incentive");

            migrationBuilder.DropTable(
                name: "SleepingMarkets",
                schema: "incentive");

            migrationBuilder.DropColumn(
                name: "AvgCheck",
                schema: "incentive",
                table: "RouteTargets");

            migrationBuilder.DropColumn(
                name: "Placement",
                schema: "incentive",
                table: "RouteTargets");

            migrationBuilder.DropColumn(
                name: "PlanAdded",
                schema: "incentive",
                table: "RouteTargets");

            migrationBuilder.DropColumn(
                name: "Sets",
                schema: "incentive",
                table: "RouteTargets");

            migrationBuilder.DropColumn(
                name: "Sleeping",
                schema: "incentive",
                table: "RouteTargets");

            migrationBuilder.DropColumn(
                name: "PromosJson",
                schema: "incentive",
                table: "Programs");
        }
    }
}
