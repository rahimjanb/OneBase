using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RawMaterialProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "production");

            migrationBuilder.AddColumn<string>(
                name: "DataOwner",
                schema: "procurement",
                table: "Warehouses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataOwnerSince",
                schema: "procurement",
                table: "Warehouses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductionOrderId",
                schema: "procurement",
                table: "StockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverconsumptionPercent",
                schema: "procurement",
                table: "Settings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.CreateTable(
                name: "Recipes",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LinkoProductId = table.Column<long>(type: "bigint", nullable: true),
                    OutputName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OutputUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipes", x => x.Id);
                    table.CheckConstraint("CK_ProductionRecipes_OutputQuantity", "\"OutputQuantity\" > 0");
                    table.CheckConstraint("CK_ProductionRecipes_VersionNo", "\"VersionNo\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductionRecipes_Units_OutputUnit",
                        column: x => x.OutputUnit,
                        principalSchema: "procurement",
                        principalTable: "Units",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRecipes_Users_ActivatedById",
                        column: x => x.ActivatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SheetImports",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sheet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Schedule = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: true),
                    DailyAt = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LastMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementSheetImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSheetImports_DataSourceConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "DataSourceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementSheetImports_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSheetImports_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OutputQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    ResponsibleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                    table.CheckConstraint("CK_ProductionOrders_OutputQty", "\"OutputQty\" IS NULL OR \"OutputQty\" >= 0");
                    table.CheckConstraint("CK_ProductionOrders_PlannedQty", "\"PlannedQty\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "production",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Users_ResponsibleId",
                        column: x => x.ResponsibleId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeLines",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    LossPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecipeLines", x => x.Id);
                    table.CheckConstraint("CK_ProductionRecipeLines_LossPercent", "\"LossPercent\" >= 0 AND \"LossPercent\" <= 100");
                    table.CheckConstraint("CK_ProductionRecipeLines_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductionRecipeLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionRecipeLines_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "production",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormQty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    LossPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RequiredQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    IssuedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReturnedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualNormQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Overconsumed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderLines", x => x.Id);
                    table.CheckConstraint("CK_ProductionOrderLines_Issued", "\"IssuedQty\" >= 0 AND \"ReturnedQty\" >= 0 AND \"ReturnedQty\" <= \"IssuedQty\"");
                    table.ForeignKey(
                        name: "FK_ProductionOrderLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "production",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementStockDocuments_ProductionOrderId",
                schema: "procurement",
                table: "StockDocuments",
                column: "ProductionOrderId",
                filter: "\"ProductionOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderLines_ItemId",
                schema: "production",
                table: "OrderLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderLines_OrderId_ItemId",
                schema: "production",
                table: "OrderLines",
                columns: new[] { "OrderId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DepartmentId",
                schema: "production",
                table: "Orders",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ResponsibleId",
                schema: "production",
                table: "Orders",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_WarehouseId",
                schema: "production",
                table: "Orders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CreatedById_ClientKey",
                schema: "production",
                table: "Orders",
                columns: new[] { "CreatedById", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_Number",
                schema: "production",
                table: "Orders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_RecipeId",
                schema: "production",
                table: "Orders",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_Status_PlannedDate",
                schema: "production",
                table: "Orders",
                columns: new[] { "Status", "PlannedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeLines_ItemId",
                schema: "production",
                table: "RecipeLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipeLines_RecipeId_ItemId",
                schema: "production",
                table: "RecipeLines",
                columns: new[] { "RecipeId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipes_Code_Draft",
                schema: "production",
                table: "Recipes",
                column: "Code",
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecipes_Code_VersionNo",
                schema: "production",
                table: "Recipes",
                columns: new[] { "Code", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ActivatedById",
                schema: "production",
                table: "Recipes",
                column: "ActivatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OutputUnit",
                schema: "production",
                table: "Recipes",
                column: "OutputUnit");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSheetImports_NextRunAt",
                schema: "procurement",
                table: "SheetImports",
                column: "NextRunAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSheetImports_Warehouse_Connection_Sheet",
                schema: "procurement",
                table: "SheetImports",
                columns: new[] { "WarehouseId", "ConnectionId", "Sheet" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SheetImports_ConnectionId",
                schema: "procurement",
                table: "SheetImports",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SheetImports_OwnerId",
                schema: "procurement",
                table: "SheetImports",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementStockDocuments_ProductionOrders_ProductionOrderId",
                schema: "procurement",
                table: "StockDocuments",
                column: "ProductionOrderId",
                principalSchema: "production",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementStockDocuments_ProductionOrders_ProductionOrderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "production");

            migrationBuilder.DropTable(
                name: "RecipeLines",
                schema: "production");

            migrationBuilder.DropTable(
                name: "SheetImports",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "production");

            migrationBuilder.DropTable(
                name: "Recipes",
                schema: "production");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementStockDocuments_ProductionOrderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "DataOwner",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DataOwnerSince",
                schema: "procurement",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ProductionOrderId",
                schema: "procurement",
                table: "StockDocuments");

            migrationBuilder.DropColumn(
                name: "OverconsumptionPercent",
                schema: "procurement",
                table: "Settings");
        }
    }
}
