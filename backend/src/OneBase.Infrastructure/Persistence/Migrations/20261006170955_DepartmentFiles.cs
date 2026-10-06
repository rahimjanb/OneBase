using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Folders_DepartmentId",
                table: "Folders");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Folders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "FileVersions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByConnectionId",
                table: "FileVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "Files",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByConnectionId",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProtectedPassword = table.Column<string>(type: "text", nullable: false),
                    Access = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PasswordChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileConnections_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DepartmentId_ParentId",
                table: "Folders",
                columns: new[] { "DepartmentId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DepartmentRoot",
                table: "Folders",
                column: "DepartmentId",
                unique: true,
                filter: "\"ParentId\" IS NULL AND \"DepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Files_DepartmentId_DeletedAt",
                table: "Files",
                columns: new[] { "DepartmentId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileConnections_DepartmentId",
                table: "FileConnections",
                column: "DepartmentId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FileConnections_Username",
                table: "FileConnections",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileConnections");

            migrationBuilder.DropIndex(
                name: "IX_Folders_DepartmentId_ParentId",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Folders_DepartmentRoot",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Files_DepartmentId_DeletedAt",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "UploadedByConnectionId",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "UpdatedByConnectionId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "Files");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "FileVersions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DepartmentId",
                table: "Folders",
                column: "DepartmentId");
        }
    }
}
