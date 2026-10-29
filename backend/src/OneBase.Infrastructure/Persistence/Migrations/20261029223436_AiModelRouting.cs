using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiModelRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelReason",
                schema: "ai",
                table: "Usage",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceBasis",
                schema: "ai",
                table: "Usage",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TurnId",
                schema: "ai",
                table: "Usage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsedFallback",
                schema: "ai",
                table: "Usage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AgentModel",
                schema: "ai",
                table: "Settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentProvider",
                schema: "ai",
                table: "Settings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplexityThreshold",
                schema: "ai",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "FallbackMode",
                schema: "ai",
                table: "Settings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.AddColumn<string>(
                name: "FinalAnswerMode",
                schema: "ai",
                table: "Settings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "fixed");

            migrationBuilder.AddColumn<string>(
                name: "FinalComplexModel",
                schema: "ai",
                table: "Settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalComplexProvider",
                schema: "ai",
                table: "Settings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalDefaultModel",
                schema: "ai",
                table: "Settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalDefaultProvider",
                schema: "ai",
                table: "Settings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelRoutingMode",
                schema: "ai",
                table: "Settings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "off");

            migrationBuilder.AddColumn<decimal>(
                name: "QuestionBudgetUsd",
                schema: "ai",
                table: "Settings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewMaxAttempts",
                schema: "ai",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "Routing",
                schema: "ai",
                table: "AuditLogs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TurnId",
                schema: "ai",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usage_TurnId",
                schema: "ai",
                table: "Usage",
                column: "TurnId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TurnId",
                schema: "ai",
                table: "AuditLogs",
                column: "TurnId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usage_TurnId",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TurnId",
                schema: "ai",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ModelReason",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "PriceBasis",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "TurnId",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "UsedFallback",
                schema: "ai",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "AgentModel",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AgentProvider",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ComplexityThreshold",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FallbackMode",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FinalAnswerMode",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FinalComplexModel",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FinalComplexProvider",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FinalDefaultModel",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FinalDefaultProvider",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ModelRoutingMode",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "QuestionBudgetUsd",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ReviewMaxAttempts",
                schema: "ai",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "Routing",
                schema: "ai",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TurnId",
                schema: "ai",
                table: "AuditLogs");
        }
    }
}
