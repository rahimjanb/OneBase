using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "field");

            migrationBuilder.CreateTable(
                name: "CustomerStats",
                schema: "field",
                columns: table => new
                {
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    LastOrderDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastVisitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Sales7 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Sales30 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Sales90 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SalesPrev30 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Kg30 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Orders30 = table.Column<int>(type: "integer", nullable: false),
                    Visits30 = table.Column<int>(type: "integer", nullable: false),
                    RefreshedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerStats", x => x.MarketId);
                });

            migrationBuilder.CreateTable(
                name: "JointVisits",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MarketId = table.Column<long>(type: "bigint", nullable: true),
                    Objective = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JointVisits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceData = table.Column<string>(type: "jsonb", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarketId = table.Column<long>(type: "bigint", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    GeoRadiusM = table.Column<int>(type: "integer", nullable: false),
                    GpsToleranceM = table.Column<int>(type: "integer", nullable: false),
                    MaxRoutePoints = table.Column<int>(type: "integer", nullable: false),
                    VisitMinutes = table.Column<int>(type: "integer", nullable: false),
                    TravelSpeedKmh = table.Column<int>(type: "integer", nullable: false),
                    DayStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    NotVisitedDays = table.Column<int>(type: "integer", nullable: false),
                    DeclinePct = table.Column<int>(type: "integer", nullable: false),
                    DeclineMinSales = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BehindPlanPct = table.Column<int>(type: "integer", nullable: false),
                    AutoPlanning = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "field",
                columns: table => new
                {
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MonthlyTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.MarketId);
                });

            migrationBuilder.CreateTable(
                name: "JointVisitParticipants",
                schema: "field",
                columns: table => new
                {
                    JointVisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JointVisitParticipants", x => new { x.JointVisitId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_JointVisitParticipants_JointVisits_JointVisitId",
                        column: x => x.JointVisitId,
                        principalSchema: "field",
                        principalTable: "JointVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkoUserId = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchIds = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Members_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Members_RecipientId",
                        column: x => x.RecipientId,
                        principalSchema: "field",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DistanceKm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Routes_Members_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "field",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarketId = table.Column<long>(type: "bigint", nullable: true),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Members_AssignedToId",
                        column: x => x.AssignedToId,
                        principalSchema: "field",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Members_SupervisorId",
                        column: x => x.SupervisorId,
                        principalSchema: "field",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Visits",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoutePointId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    AccuracyM = table.Column<double>(type: "double precision", nullable: true),
                    DistanceM = table.Column<double>(type: "double precision", nullable: true),
                    GeoStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Result = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PhotoKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    JointVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldVisits_Members_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "field",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoutePoints",
                schema: "field",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    PlannedTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ActualArrival = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActualDeparture = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutePoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutePoints_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "field",
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AssignedAgentId",
                schema: "field",
                table: "Customers",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStats_LastVisitDate",
                schema: "field",
                table: "CustomerStats",
                column: "LastVisitDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerStats_Sales30",
                schema: "field",
                table: "CustomerStats",
                column: "Sales30");

            migrationBuilder.CreateIndex(
                name: "IX_JointVisitParticipants_MemberId",
                schema: "field",
                table: "JointVisitParticipants",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_JointVisits_Date",
                schema: "field",
                table: "JointVisits",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Members_LinkoUserId",
                schema: "field",
                table: "Members",
                column: "LinkoUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_Role_IsActive",
                schema: "field",
                table: "Members",
                columns: new[] { "Role", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Members_TeamId",
                schema: "field",
                table: "Members",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_UserId",
                schema: "field",
                table: "Members",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_CreatedAt",
                schema: "field",
                table: "Notifications",
                columns: new[] { "RecipientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_ReadAt",
                schema: "field",
                table: "Notifications",
                columns: new[] { "RecipientId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_AgentId",
                schema: "field",
                table: "Recommendations",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_Key_Status",
                schema: "field",
                table: "Recommendations",
                columns: new[] { "Key", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_Status_SupervisorId",
                schema: "field",
                table: "Recommendations",
                columns: new[] { "Status", "SupervisorId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutePoints_MarketId",
                schema: "field",
                table: "RoutePoints",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePoints_RouteId_Sequence",
                schema: "field",
                table: "RoutePoints",
                columns: new[] { "RouteId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_AgentId_Date",
                schema: "field",
                table: "Routes",
                columns: new[] { "AgentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Date",
                schema: "field",
                table: "Routes",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedToId_Status",
                schema: "field",
                table: "Tasks",
                columns: new[] { "AssignedToId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_DueDate",
                schema: "field",
                table: "Tasks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_MarketId",
                schema: "field",
                table: "Tasks",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_SupervisorId_Status",
                schema: "field",
                table: "Tasks",
                columns: new[] { "SupervisorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SupervisorId",
                schema: "field",
                table: "Teams",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldVisits_AgentId_StartedAt",
                schema: "field",
                table: "Visits",
                columns: new[] { "AgentId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldVisits_MarketId_StartedAt",
                schema: "field",
                table: "Visits",
                columns: new[] { "MarketId", "StartedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Members_AssignedAgentId",
                schema: "field",
                table: "Customers",
                column: "AssignedAgentId",
                principalSchema: "field",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JointVisitParticipants_Members_MemberId",
                schema: "field",
                table: "JointVisitParticipants",
                column: "MemberId",
                principalSchema: "field",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Teams_TeamId",
                schema: "field",
                table: "Members",
                column: "TeamId",
                principalSchema: "field",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Members_SupervisorId",
                schema: "field",
                table: "Teams");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "field");

            migrationBuilder.DropTable(
                name: "CustomerStats",
                schema: "field");

            migrationBuilder.DropTable(
                name: "JointVisitParticipants",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Recommendations",
                schema: "field");

            migrationBuilder.DropTable(
                name: "RoutePoints",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Settings",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Tasks",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Visits",
                schema: "field");

            migrationBuilder.DropTable(
                name: "JointVisits",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Routes",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Members",
                schema: "field");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "field");
        }
    }
}
