using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement9_Tenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenders",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TenderNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Participants = table.Column<int>(type: "integer", nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeliveredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResponsibleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementTenders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementTenders_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "procurement",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "procurement",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenders_Users_ResponsibleId",
                        column: x => x.ResponsibleId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderItems",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementTenderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderItems_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalSchema: "procurement",
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderItems_TenderId",
                schema: "procurement",
                table: "TenderItems",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_CategoryId",
                schema: "procurement",
                table: "Tenders",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_Number",
                schema: "procurement",
                table: "Tenders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_ResponsibleId",
                schema: "procurement",
                table: "Tenders",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_ResponsibleId_ClientKey",
                schema: "procurement",
                table: "Tenders",
                columns: new[] { "ResponsibleId", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_Status_DeliveryDate",
                schema: "procurement",
                table: "Tenders",
                columns: new[] { "Status", "DeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_SupplierId_TenderDate",
                schema: "procurement",
                table: "Tenders",
                columns: new[] { "SupplierId", "TenderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenders_TenderDate",
                schema: "procurement",
                table: "Tenders",
                column: "TenderDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenderItems",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Tenders",
                schema: "procurement");
        }
    }
}
