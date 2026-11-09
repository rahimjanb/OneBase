using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OutputItemId",
                schema: "production",
                table: "Recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Shifts",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CorrectsId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftNo = table.Column<int>(type: "integer", nullable: false),
                    Line = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GoodOutput = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Packs = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Boxes = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionShifts", x => x.Id);
                    table.CheckConstraint("CK_ProductionShifts_Output", "(\"GoodOutput\" IS NULL OR \"GoodOutput\" >= 0) AND (\"Packs\" IS NULL OR \"Packs\" >= 0) AND (\"Boxes\" IS NULL OR \"Boxes\" >= 0)");
                    table.CheckConstraint("CK_ProductionShifts_ShiftNo", "\"ShiftNo\" BETWEEN 1 AND 9");
                    table.ForeignKey(
                        name: "FK_ProductionShifts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "production",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionShifts_Shifts_CorrectsId",
                        column: x => x.CorrectsId,
                        principalSchema: "production",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionShifts_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionShifts_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionShifts_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShiftBatches",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<int>(type: "integer", nullable: false),
                    MassKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionShiftBatches", x => x.Id);
                    table.CheckConstraint("CK_ProductionShiftBatches_MassKg", "\"MassKg\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductionShiftBatches_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "production",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftDefects",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageNo = table.Column<int>(type: "integer", nullable: false),
                    DefectTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QuantityKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Rework = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionShiftDefects", x => x.Id);
                    table.CheckConstraint("CK_ProductionShiftDefects_QuantityKg", "\"QuantityKg\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductionShiftDefects_DefectTypes_DefectTypeId",
                        column: x => x.DefectTypeId,
                        principalSchema: "production",
                        principalTable: "DefectTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionShiftDefects_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "production",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftMaterials",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageNo = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionShiftMaterials", x => x.Id);
                    table.CheckConstraint("CK_ProductionShiftMaterials_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductionShiftMaterials_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "procurement",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionShiftMaterials_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "production",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftStages",
                schema: "production",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageNo = table.Column<int>(type: "integer", nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    InputKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    OutputKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Moisture = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    ReworkKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionShiftStages", x => x.Id);
                    table.CheckConstraint("CK_ProductionShiftStages_Kg", "(\"InputKg\" IS NULL OR \"InputKg\" >= 0) AND (\"OutputKg\" IS NULL OR \"OutputKg\" >= 0) AND (\"ReworkKg\" IS NULL OR \"ReworkKg\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ProductionShiftStages_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "production",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OutputItemId",
                schema: "production",
                table: "Recipes",
                column: "OutputItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShiftBatches_ShiftId_No",
                schema: "production",
                table: "ShiftBatches",
                columns: new[] { "ShiftId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShiftDefects_ShiftId",
                schema: "production",
                table: "ShiftDefects",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDefects_DefectTypeId",
                schema: "production",
                table: "ShiftDefects",
                column: "DefectTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShiftMaterials_ItemId",
                schema: "production",
                table: "ShiftMaterials",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShiftMaterials_ShiftId",
                schema: "production",
                table: "ShiftMaterials",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShifts_CorrectsId_Open",
                schema: "production",
                table: "Shifts",
                column: "CorrectsId",
                unique: true,
                filter: "\"CorrectsId\" IS NOT NULL AND \"Status\" IN ('Draft', 'Closed')");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShifts_CreatedById_ClientKey",
                schema: "production",
                table: "Shifts",
                columns: new[] { "CreatedById", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShifts_Number",
                schema: "production",
                table: "Shifts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShifts_OrderId_ShiftDate",
                schema: "production",
                table: "Shifts",
                columns: new[] { "OrderId", "ShiftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShifts_Status_ShiftDate",
                schema: "production",
                table: "Shifts",
                columns: new[] { "Status", "ShiftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ApprovedById",
                schema: "production",
                table: "Shifts",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OperatorId",
                schema: "production",
                table: "Shifts",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_SupervisorId",
                schema: "production",
                table: "Shifts",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionShiftStages_ShiftId_StageNo",
                schema: "production",
                table: "ShiftStages",
                columns: new[] { "ShiftId", "StageNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionRecipes_Items_OutputItemId",
                schema: "production",
                table: "Recipes",
                column: "OutputItemId",
                principalSchema: "procurement",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionRecipes_Items_OutputItemId",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "ShiftBatches",
                schema: "production");

            migrationBuilder.DropTable(
                name: "ShiftDefects",
                schema: "production");

            migrationBuilder.DropTable(
                name: "ShiftMaterials",
                schema: "production");

            migrationBuilder.DropTable(
                name: "ShiftStages",
                schema: "production");

            migrationBuilder.DropTable(
                name: "Shifts",
                schema: "production");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_OutputItemId",
                schema: "production",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "OutputItemId",
                schema: "production",
                table: "Recipes");
        }
    }
}
