using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkoMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "linko");

            migrationBuilder.CreateTable(
                name: "Borders",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    IsDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Borders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KpiPlans",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Plan = table.Column<decimal>(type: "numeric", nullable: false),
                    IndicatorId = table.Column<long>(type: "bigint", nullable: true),
                    IndicatorName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Markets",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MarketTypeId = table.Column<long>(type: "bigint", nullable: true),
                    MarketTypeName = table.Column<string>(type: "text", nullable: true),
                    ResponsibleAgentId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    BranchName = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lon = table.Column<double>(type: "double precision", nullable: true),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Markets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketUsers",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    IsDelete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderReturns",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: true),
                    AgentId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    BranchName = table.Column<string>(type: "text", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    BranchName = table.Column<string>(type: "text", nullable: true),
                    AgentId = table.Column<long>(type: "bigint", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    IsFullReturn = table.Column<bool>(type: "boolean", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    TypeId = table.Column<long>(type: "bigint", nullable: true),
                    IsWeighted = table.Column<bool>(type: "boolean", nullable: false),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductTypes",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Tm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncState",
                schema: "linko",
                columns: table => new
                {
                    Entity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastTm = table.Column<decimal>(type: "numeric", nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    LastRows = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncState", x => x.Entity);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    SecondName = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    JobId = table.Column<long>(type: "bigint", nullable: true),
                    JobName = table.Column<string>(type: "text", nullable: true),
                    PositionId = table.Column<long>(type: "bigint", nullable: true),
                    PositionName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Visits",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsInPlan = table.Column<bool>(type: "boolean", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderReturnLines",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ReturnId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderReturnLines_OrderReturns_ReturnId",
                        column: x => x.ReturnId,
                        principalSchema: "linko",
                        principalTable: "OrderReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "linko",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "linko",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Markets_BranchId",
                schema: "linko",
                table: "Markets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Markets_ResponsibleAgentId",
                schema: "linko",
                table: "Markets",
                column: "ResponsibleAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketUsers_MarketId",
                schema: "linko",
                table: "MarketUsers",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketUsers_UserId",
                schema: "linko",
                table: "MarketUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                schema: "linko",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ProductId",
                schema: "linko",
                table: "OrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturnLines_ReturnId",
                schema: "linko",
                table: "OrderReturnLines",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_CreatedDate",
                schema: "linko",
                table: "OrderReturns",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AgentId",
                schema: "linko",
                table: "Orders",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedDate",
                schema: "linko",
                table: "Orders",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryDate",
                schema: "linko",
                table: "Orders",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MarketId",
                schema: "linko",
                table: "Orders",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Day",
                schema: "linko",
                table: "Visits",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_UserId",
                schema: "linko",
                table: "Visits",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Borders",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "KpiPlans",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Markets",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "MarketUsers",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "OrderReturnLines",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "ProductTypes",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "SyncState",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Visits",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "linko");

            migrationBuilder.DropTable(
                name: "OrderReturns",
                schema: "linko");
        }
    }
}
