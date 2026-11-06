using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Twin1_Objects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "twin");

            migrationBuilder.CreateTable(
                name: "Objects",
                schema: "twin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwinObjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ObjectLinks",
                schema: "twin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkoStockId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwinObjectLinks", x => x.Id);
                    table.CheckConstraint("CK_TwinObjectLinks_OneTarget", "(CASE WHEN \"DepartmentId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"WarehouseId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"LinkoStockId\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_TwinObjectLinks_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwinObjectLinks_ProcurementWarehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "procurement",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TwinObjectLinks_TwinObjects_ObjectId",
                        column: x => x.ObjectId,
                        principalSchema: "twin",
                        principalTable: "Objects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TwinObjectLinks_DepartmentId",
                schema: "twin",
                table: "ObjectLinks",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TwinObjectLinks_ObjectId",
                schema: "twin",
                table: "ObjectLinks",
                column: "ObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TwinObjectLinks_WarehouseId",
                schema: "twin",
                table: "ObjectLinks",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_TwinObjects_Key",
                schema: "twin",
                table: "Objects",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObjectLinks",
                schema: "twin");

            migrationBuilder.DropTable(
                name: "Objects",
                schema: "twin");
        }
    }
}
