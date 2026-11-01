using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement3_SuppliersOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Link",
                schema: "work",
                table: "Tasks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedQty",
                schema: "procurement",
                table: "RequestItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "procurement",
                table: "Documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Inn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankMfo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BankAccount = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementSuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Offers",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryDays = table.Column<int>(type: "integer", nullable: true),
                    DeliveryCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PricesIncludeVat = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentTerms = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Warranty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Quality = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementOffers_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "procurement",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementOffers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "procurement",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsibleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PricesIncludeVat = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    OrderedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "procurement",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementPurchaseOrders_Users_ResponsibleId",
                        column: x => x.ResponsibleId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCategories",
                schema: "procurement",
                columns: table => new
                {
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementSupplierCategories", x => new { x.SupplierId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "procurement",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierCategories_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "procurement",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfferItems",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementOfferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementOfferItems_Offers_OfferId",
                        column: x => x.OfferId,
                        principalSchema: "procurement",
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementOfferItems_RequestItems_RequestItemId",
                        column: x => x.RequestItemId,
                        principalSchema: "procurement",
                        principalTable: "RequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderApprovals",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DeciderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementOrderApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementOrderApprovals_PurchaseOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementOrderApprovals_Users_DeciderId",
                        column: x => x.DeciderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    RequestItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPurchaseOrderItems_PurchaseOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementPurchaseOrderItems_RequestItems_RequestItemId",
                        column: x => x.RequestItemId,
                        principalSchema: "procurement",
                        principalTable: "RequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOfferItems_OfferId",
                schema: "procurement",
                table: "OfferItems",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOfferItems_RequestItemId",
                schema: "procurement",
                table: "OfferItems",
                column: "RequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOffers_RequestId",
                schema: "procurement",
                table: "Offers",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOffers_SupplierId",
                schema: "procurement",
                table: "Offers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOrderApprovals_DeciderId",
                schema: "procurement",
                table: "OrderApprovals",
                column: "DeciderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOrderApprovals_OrderId_Round",
                schema: "procurement",
                table: "OrderApprovals",
                columns: new[] { "OrderId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrderItems_OrderId",
                schema: "procurement",
                table: "PurchaseOrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrderItems_RequestItemId",
                schema: "procurement",
                table: "PurchaseOrderItems",
                column: "RequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_CreatedById_ClientKey",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "CreatedById", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_Number",
                schema: "procurement",
                table: "PurchaseOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_OfferId",
                schema: "procurement",
                table: "PurchaseOrders",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_ResponsibleId_Status",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "ResponsibleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_Status_ExpectedDate",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "Status", "ExpectedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPurchaseOrders_SupplierId_Status",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierCategories_CategoryId",
                schema: "procurement",
                table: "SupplierCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSuppliers_Inn",
                schema: "procurement",
                table: "Suppliers",
                column: "Inn",
                unique: true,
                filter: "\"Inn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSuppliers_Name",
                schema: "procurement",
                table: "Suppliers",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfferItems",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "OrderApprovals",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierCategories",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Offers",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "procurement");

            migrationBuilder.DropColumn(
                name: "Link",
                schema: "work",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OrderedQty",
                schema: "procurement",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "procurement",
                table: "Documents");
        }
    }
}
