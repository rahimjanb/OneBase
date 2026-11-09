using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Recruitment1_Core : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recruitment");

            migrationBuilder.CreateTable(
                name: "Candidates",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetentionFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsentTexts",
                schema: "recruitment",
                columns: table => new
                {
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentTexts", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "HiringRequests",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Headcount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Requirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SalaryFrom = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryTo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DesiredStart = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiringRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HiringRequests_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateConsents",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TextVersion = table.Column<int>(type: "integer", nullable: false),
                    GivenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateConsents_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateContacts",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateContacts_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateDuplicates",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OtherCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateDuplicates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateDuplicates_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidateDuplicates_Candidates_OtherCandidateId",
                        column: x => x.OtherCandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateResumes",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    TextError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateResumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateResumes_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSources",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateSources_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vacancies",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    HiringRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Requirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Conditions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SalaryFrom = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryTo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Headcount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecruiterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vacancies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_HiringRequests_HiringRequestId",
                        column: x => x.HiringRequestId,
                        principalSchema: "recruitment",
                        principalTable: "HiringRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VacancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StageChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalSchema: "recruitment",
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Applications_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalSchema: "recruitment",
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationEvents",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ToStage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationEvents_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "recruitment",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_ApplicationId_At",
                schema: "recruitment",
                table: "ApplicationEvents",
                columns: new[] { "ApplicationId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CandidateId_VacancyId",
                schema: "recruitment",
                table: "Applications",
                columns: new[] { "CandidateId", "VacancyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_VacancyId_Stage",
                schema: "recruitment",
                table: "Applications",
                columns: new[] { "VacancyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateConsents_CandidateId",
                schema: "recruitment",
                table: "CandidateConsents",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateContacts_CandidateId_Kind_Value",
                schema: "recruitment",
                table: "CandidateContacts",
                columns: new[] { "CandidateId", "Kind", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateContacts_Verified",
                schema: "recruitment",
                table: "CandidateContacts",
                columns: new[] { "Kind", "Value" },
                unique: true,
                filter: "\"Verified\"");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateDuplicates_CandidateId_OtherCandidateId_Kind_Value",
                schema: "recruitment",
                table: "CandidateDuplicates",
                columns: new[] { "CandidateId", "OtherCandidateId", "Kind", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateDuplicates_OtherCandidateId",
                schema: "recruitment",
                table: "CandidateDuplicates",
                column: "OtherCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateDuplicates_Status",
                schema: "recruitment",
                table: "CandidateDuplicates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateResumes_CandidateId_Sha256",
                schema: "recruitment",
                table: "CandidateResumes",
                columns: new[] { "CandidateId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_LastActivityAt",
                schema: "recruitment",
                table: "Candidates",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_RetentionFrom",
                schema: "recruitment",
                table: "Candidates",
                column: "RetentionFrom");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSources_CandidateId",
                schema: "recruitment",
                table: "CandidateSources",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSources_Source_ExternalId",
                schema: "recruitment",
                table: "CandidateSources",
                columns: new[] { "Source", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_CreatedById",
                schema: "recruitment",
                table: "HiringRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_DepartmentId",
                schema: "recruitment",
                table: "HiringRequests",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_Number",
                schema: "recruitment",
                table: "HiringRequests",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_Status_CreatedAt",
                schema: "recruitment",
                table: "HiringRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_DepartmentId",
                schema: "recruitment",
                table: "Vacancies",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_HiringRequestId",
                schema: "recruitment",
                table: "Vacancies",
                column: "HiringRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Number",
                schema: "recruitment",
                table: "Vacancies",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Status_CreatedAt",
                schema: "recruitment",
                table: "Vacancies",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationEvents",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "CandidateConsents",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "CandidateContacts",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "CandidateDuplicates",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "CandidateResumes",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "CandidateSources",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "ConsentTexts",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Settings",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Applications",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Candidates",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Vacancies",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "HiringRequests",
                schema: "recruitment");
        }
    }
}
