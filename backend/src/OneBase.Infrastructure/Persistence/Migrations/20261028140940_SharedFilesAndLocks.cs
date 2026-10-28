using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedFilesAndLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SharedRootId",
                table: "Folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SharedRootId",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LinkExpiresAt",
                table: "FileConnections",
                type: "timestamp with time zone",
                nullable: true);

            // Ссылки, выпущенные до срока действия, — год от выпуска (как Files:LinkLifetimeDays по умолчанию): подключённые диски
            // продолжают работать, а бессрочных ссылок не остаётся. Продление — «Новая ссылка».
            migrationBuilder.Sql(
                "UPDATE \"FileConnections\" SET \"LinkExpiresAt\" = COALESCE(\"LinkChangedAt\", \"CreatedAt\") + interval '365 days' " +
                "WHERE \"LinkTokenHash\" IS NOT NULL AND \"LinkExpiresAt\" IS NULL;");

            migrationBuilder.CreateTable(
                name: "FileLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Owner = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileLocks_Files_FileItemId",
                        column: x => x.FileItemId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileUserLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedLinkToken = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileUserLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileUserLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_SharedRootId_ParentId",
                table: "Folders",
                columns: new[] { "SharedRootId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Files_SharedRootId_DeletedAt",
                table: "Files",
                columns: new[] { "SharedRootId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileLocks_FileItemId_ExpiresAt",
                table: "FileLocks",
                columns: new[] { "FileItemId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileLocks_Token",
                table: "FileLocks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileUserLinks_LinkTokenHash",
                table: "FileUserLinks",
                column: "LinkTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileUserLinks_UserId",
                table: "FileUserLinks",
                column: "UserId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileLocks");

            migrationBuilder.DropTable(
                name: "FileUserLinks");

            migrationBuilder.DropIndex(
                name: "IX_Folders_SharedRootId_ParentId",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Files_SharedRootId_DeletedAt",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "SharedRootId",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "SharedRootId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "LinkExpiresAt",
                table: "FileConnections");
        }
    }
}
