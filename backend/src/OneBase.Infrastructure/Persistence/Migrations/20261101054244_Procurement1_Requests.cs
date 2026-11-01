using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Procurement1_Requests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "procurement");

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementAuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementAuditLog_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Counters",
                schema: "procurement",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Last = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementCounters", x => new { x.Prefix, x.Year });
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementDocuments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    DesiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EstimatedTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ForwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRequests_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequests_Users_InitiatorId",
                        column: x => x.InitiatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                schema: "procurement",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementUnits", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "RequestItems",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    EstimatedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRequestItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "procurement",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequestItems_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "procurement",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementRequestItems_Units_UnitCode",
                        column: x => x.UnitCode,
                        principalSchema: "procurement",
                        principalTable: "Units",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAuditLog_ActorId",
                schema: "procurement",
                table: "AuditLog",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAuditLog_DocType_DocId_CreatedAt",
                schema: "procurement",
                table: "AuditLog",
                columns: new[] { "DocType", "DocId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCategories_Code",
                schema: "procurement",
                table: "Categories",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementCategories_Name",
                schema: "procurement",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementDocuments_OwnerType_OwnerId",
                schema: "procurement",
                table: "Documents",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementDocuments_UploadedById",
                schema: "procurement",
                table: "Documents",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequestItems_CategoryId",
                schema: "procurement",
                table: "RequestItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequestItems_RequestId_LineNo",
                schema: "procurement",
                table: "RequestItems",
                columns: new[] { "RequestId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequestItems_UnitCode",
                schema: "procurement",
                table: "RequestItems",
                column: "UnitCode");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequests_DepartmentId_CreatedAt",
                schema: "procurement",
                table: "Requests",
                columns: new[] { "DepartmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequests_InitiatorId_ClientKey",
                schema: "procurement",
                table: "Requests",
                columns: new[] { "InitiatorId", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequests_InitiatorId_Status",
                schema: "procurement",
                table: "Requests",
                columns: new[] { "InitiatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequests_Number",
                schema: "procurement",
                table: "Requests",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequests_Status_DepartmentId",
                schema: "procurement",
                table: "Requests",
                columns: new[] { "Status", "DepartmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Counters",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "RequestItems",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Requests",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Units",
                schema: "procurement");
        }
    }
}
