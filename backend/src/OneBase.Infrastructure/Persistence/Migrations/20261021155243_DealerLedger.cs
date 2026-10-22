using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DealerLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DealerLedger",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Dealer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DealerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceRef = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerLedger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealerTerms",
                schema: "sales",
                columns: table => new
                {
                    DealerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dealer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormDays = table.Column<int>(type: "integer", nullable: true),
                    Opening = table.Column<decimal>(type: "numeric", nullable: true),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerTerms", x => x.DealerKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealerLedger_Field_DealerKey_Date",
                schema: "sales",
                table: "DealerLedger",
                columns: new[] { "Field", "DealerKey", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealerLedger",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "DealerTerms",
                schema: "sales");
        }
    }
}
