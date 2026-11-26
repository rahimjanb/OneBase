using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TajikSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tajik");

            migrationBuilder.CreateTable(
                name: "Regions",
                schema: "tajik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Keywords = table.Column<string[]>(type: "text[]", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleLines",
                schema: "tajik",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Customer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Manager = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockItems",
                schema: "tajik",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Warehouse = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => new { x.Date, x.ProductCode, x.Warehouse });
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                schema: "tajik",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: true),
                    Lines = table.Column<int>(type: "integer", nullable: false),
                    Documents = table.Column<int>(type: "integer", nullable: false),
                    Added = table.Column<int>(type: "integer", nullable: false),
                    Changed = table.Column<int>(type: "integer", nullable: false),
                    Removed = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    StockRows = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegionAssignments",
                schema: "tajik",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionAssignments", x => new { x.Kind, x.Name });
                    table.ForeignKey(
                        name: "FK_RegionAssignments_Regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "tajik",
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegionAssignments_RegionId",
                schema: "tajik",
                table: "RegionAssignments",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_TajikRegions_Name",
                schema: "tajik",
                table: "Regions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TajikSaleLines_Customer",
                schema: "tajik",
                table: "SaleLines",
                column: "Customer");

            migrationBuilder.CreateIndex(
                name: "IX_TajikSaleLines_Doc",
                schema: "tajik",
                table: "SaleLines",
                columns: new[] { "DocNumber", "DocDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TajikSaleLines_DocDate",
                schema: "tajik",
                table: "SaleLines",
                column: "DocDate");

            migrationBuilder.CreateIndex(
                name: "IX_TajikSyncRuns_StartedAt",
                schema: "tajik",
                table: "SyncRuns",
                column: "StartedAt");

            // Начальные регионы отчёта «Экспорт → Таджикистан»: города и районы, которые встречаются в названиях точек 1С (26.11.2026).
            // Точки без этих слов (рынки «Фаровон», «Хочи Шариф», «Садбарг»…) — «Регион не определён», пока их не закрепят на странице
            // «Регионы» (регион ТП по умолчанию или точку вручную). Только при пустой таблице — повторный запуск ничего не дублирует.
            migrationBuilder.Sql("""
                INSERT INTO tajik."Regions" ("Id", "Name", "Keywords", "SortOrder", "UpdatedAt")
                SELECT * FROM (VALUES
                    (gen_random_uuid(), 'Кулоб', ARRAY['кулоб', 'куляб']::text[], 0, now()),
                    (gen_random_uuid(), 'Восе', ARRAY['восеъ', 'восе']::text[], 1, now()),
                    (gen_random_uuid(), 'Дангара', ARRAY['дангара']::text[], 2, now()),
                    (gen_random_uuid(), 'Фархор', ARRAY['фархор']::text[], 3, now()),
                    (gen_random_uuid(), 'Темурмалик', ARRAY['темурмалик']::text[], 4, now()),
                    (gen_random_uuid(), 'Московский', ARRAY['московский']::text[], 5, now()),
                    (gen_random_uuid(), 'Шахритуз', ARRAY['шахритуз', 'шахритус']::text[], 6, now()),
                    (gen_random_uuid(), 'Кубодиён', ARRAY['кубодиён', 'кабодиён']::text[], 7, now()),
                    (gen_random_uuid(), 'Панч', ARRAY['панч', 'пяндж']::text[], 8, now()),
                    (gen_random_uuid(), 'Чиликул', ARRAY['чиликул', 'джиликул']::text[], 9, now()),
                    (gen_random_uuid(), 'Колхозобод', ARRAY['колхозобод']::text[], 10, now()),
                    (gen_random_uuid(), 'Джоми', ARRAY['чоми', 'джоми']::text[], 11, now()),
                    (gen_random_uuid(), 'Вахш', ARRAY['вахш']::text[], 12, now()),
                    (gen_random_uuid(), 'Ёвон', ARRAY['ёвон', 'явань']::text[], 13, now()),
                    (gen_random_uuid(), 'Кумсангир', ARRAY['кумсангир']::text[], 14, now()),
                    (gen_random_uuid(), 'Хуросон', ARRAY['хуросон']::text[], 15, now()),
                    (gen_random_uuid(), 'Ворошилобод', ARRAY['варашилобод', 'ворошилобод']::text[], 16, now()),
                    (gen_random_uuid(), 'Бохтар', ARRAY['бохтар']::text[], 17, now()),
                    (gen_random_uuid(), 'Сарбанд', ARRAY['сарбанд', 'левакант']::text[], 18, now())
                ) AS v("Id", "Name", "Keywords", "SortOrder", "UpdatedAt")
                WHERE NOT EXISTS (SELECT 1 FROM tajik."Regions");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegionAssignments",
                schema: "tajik");

            migrationBuilder.DropTable(
                name: "SaleLines",
                schema: "tajik");

            migrationBuilder.DropTable(
                name: "StockItems",
                schema: "tajik");

            migrationBuilder.DropTable(
                name: "SyncRuns",
                schema: "tajik");

            migrationBuilder.DropTable(
                name: "Regions",
                schema: "tajik");
        }
    }
}
