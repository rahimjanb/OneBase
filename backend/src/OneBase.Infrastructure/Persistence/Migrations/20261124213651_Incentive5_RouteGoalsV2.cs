using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Incentive5_RouteGoalsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Goals",
                schema: "incentive",
                table: "RouteTargets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashbackAccruals",
                schema: "incentive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderCreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Segment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    ListSum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseSum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetSum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Issued = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeliveredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Stand = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentiveCashbackAccruals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashbackAccruals_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "incentive",
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveCashbackAccruals_Program_Agent_Date",
                schema: "incentive",
                table: "CashbackAccruals",
                columns: new[] { "ProgramId", "AgentId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveCashbackAccruals_Program_Kind_Order",
                schema: "incentive",
                table: "CashbackAccruals",
                columns: new[] { "ProgramId", "Kind", "OrderId" },
                unique: true);

            // «Цели на маршрут» v2 (ТЗ Улугбека v2 от 24.11.2026) — раздел goals в той же строке акции Twizos, без второй акции; остальные разделы
            // (YM-01/02, TW-02, TW-01, YM-03, бюджеты — в том числе правки администратора) не трогаются. Повторно не пишется.
            migrationBuilder.Sql("""
                UPDATE incentive."Programs" SET
                    "PromosJson" = "PromosJson" || jsonb_build_object('goals', '{"start":"2026-11-23","end":"2026-11-30","mogulGrowth":0.2,"mogulBoxes":[{"name":"420 г","unitKg":0.42,"perBox":8,"price":141600,"nameUz":"420 g"},{"name":"1 кг","unitKg":1,"perBox":4,"price":160000,"nameUz":"1 kg"}],"mogulExcludedBranches":["Самарканд","Кибрай","Олмалик","AAA","Пскент"],"mogulName":"Могул","twizosGrowth":0.3,"minCheck":60000,"thermosCheck":250000,"standCheck":600000,"existingPercent":5,"newPercent":8,"standPercent":10,"boxes":[{"code":"35","name":"35 г","price":103600,"kg":0.98,"nameUz":"35 g"},{"code":"140","name":"140 г","price":288600,"kg":3.64,"nameUz":"140 g"},{"code":"200","name":"200 г","price":296000,"kg":4,"nameUz":"200 g"}],"smallSet":"35x3","mediumSet":"140x1+35x1","standSet":"35x1+140x1+200x1","bigSet":"140x1+35x3","ladder":["140x1","140x1+35x1","140x1+35x3","140x2+35x1","140x2+35x3","140x3+35x1","140x3+35x3","140x4+35x1","140x4+35x3","140x5+35x2","140x6+35x2","140x8+35x2","140x10+35x2"],"ladderTail":"35x2","ladderMode":"first","standProductIds":[315],"standZeroTarget":40,"standStock":null,"twizosSince":"2026-08-01","checkDays":90,"standStockRegions":{"AAA":"Олмалик"}}'::jsonb)
                WHERE "Code" = 'twizos-2026q4' AND NOT ("PromosJson" ? 'goals');
                """);

            // ТЗ v2: TW-01 «Лучший агент дня» завершена 21.11 (бонусы по 21.11 выплачиваются) — если конец ещё прежний (30.11).
            migrationBuilder.Sql("""
                UPDATE incentive."Programs" SET "PromosJson" = jsonb_set("PromosJson", '{best,end}', '"2026-11-21"'::jsonb)
                WHERE "Code" = 'twizos-2026q4' AND "PromosJson" #>> '{best,end}' = '2026-11-30';
                """);

            // Расписание v2: итог команды СВР — 18:30 (было 19:30), итог месяца — последний рабочий день месяца 18:00 (было 1-е число 09:00). Только
            // запуски, время которых администратор не менял.
            migrationBuilder.Sql("""
                UPDATE telegram."ReportJobs" SET "Time" = '18:30', "Version" = "Version" + 1, "ArmedAt" = now(), "UpdatedAt" = now()
                WHERE "Code" = 'incentive.team' AND "Time" = '19:30';
                UPDATE telegram."ReportJobs" SET "Time" = '18:00', "Version" = "Version" + 1, "ArmedAt" = now(), "UpdatedAt" = now()
                WHERE "Code" = 'incentive.month' AND "Time" = '09:00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE incentive."Programs" SET "PromosJson" = "PromosJson" - 'goals' WHERE "Code" = 'twizos-2026q4';
                UPDATE incentive."Programs" SET "PromosJson" = jsonb_set("PromosJson", '{best,end}', '"2026-11-30"'::jsonb)
                WHERE "Code" = 'twizos-2026q4' AND "PromosJson" #>> '{best,end}' = '2026-11-21';
                UPDATE telegram."ReportJobs" SET "Time" = '19:30', "Version" = "Version" + 1, "UpdatedAt" = now() WHERE "Code" = 'incentive.team' AND "Time" = '18:30';
                UPDATE telegram."ReportJobs" SET "Time" = '09:00', "Version" = "Version" + 1, "UpdatedAt" = now() WHERE "Code" = 'incentive.month' AND "Time" = '18:00';
                """);

            migrationBuilder.DropTable(
                name: "CashbackAccruals",
                schema: "incentive");

            migrationBuilder.DropColumn(
                name: "Goals",
                schema: "incentive",
                table: "RouteTargets");
        }
    }
}
