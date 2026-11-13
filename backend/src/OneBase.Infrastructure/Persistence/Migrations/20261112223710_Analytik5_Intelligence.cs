using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analytik5_Intelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Advanced Intelligence: только новые таблицы и nullable-колонки у находок. Идемпотентно (IF NOT EXISTS): API разработки собирается
            // из общей папки и мог применить раннюю сборку этой миграции — повтор ничего не ломает. Данные не меняются и не удаляются.
            migrationBuilder.Sql("""
                ALTER TABLE analytik."Insights" ADD COLUMN IF NOT EXISTS "Confidence" numeric(5,4) NULL;
                ALTER TABLE analytik."Insights" ADD COLUMN IF NOT EXISTS "EvidenceClass" character varying(16) NULL;
                ALTER TABLE analytik."Insights" ADD COLUMN IF NOT EXISTS "InvestigatedAt" timestamp with time zone NULL;
                ALTER TABLE analytik."Insights" ADD COLUMN IF NOT EXISTS "Investigation" jsonb NULL;

                CREATE TABLE IF NOT EXISTS analytik."BenchmarkRuns" (
                    "Id" uuid NOT NULL,
                    "Mode" character varying(16) NOT NULL,
                    "Model" character varying(100) NULL,
                    "Status" character varying(16) NOT NULL,
                    "Total" integer NOT NULL,
                    "Passed" integer NOT NULL,
                    "Score" numeric(7,4) NOT NULL,
                    "ByCategory" jsonb NULL,
                    "Results" jsonb NULL,
                    "CostUsd" numeric(18,6) NULL,
                    "StartedById" uuid NULL,
                    "StartedAt" timestamp with time zone NOT NULL,
                    "FinishedAt" timestamp with time zone NULL,
                    "Error" character varying(2000) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_AnalytikBenchmarkRuns" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS analytik."Computations" (
                    "Id" uuid NOT NULL,
                    "UserId" uuid NULL,
                    "Origin" character varying(16) NOT NULL,
                    "InvestigationId" uuid NULL,
                    "ConversationId" uuid NULL,
                    "Method" character varying(48) NOT NULL,
                    "Domain" character varying(32) NOT NULL,
                    "Algorithm" character varying(300) NOT NULL,
                    "Version" character varying(16) NOT NULL,
                    "Parameters" jsonb NOT NULL,
                    "Input" jsonb NOT NULL,
                    "Result" jsonb NULL,
                    "Sources" jsonb NULL,
                    "Limitations" jsonb NULL,
                    "PeriodFrom" date NULL,
                    "PeriodTo" date NULL,
                    "EvidenceClass" character varying(16) NULL,
                    "Status" character varying(16) NOT NULL,
                    "Error" character varying(2000) NULL,
                    "Scope" character varying(500) NULL,
                    "ElapsedMs" bigint NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_AnalytikComputations" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS analytik."Decisions" (
                    "Id" uuid NOT NULL,
                    "Tenant" character varying(64) NOT NULL,
                    "AuthorId" uuid NOT NULL,
                    "Title" character varying(300) NOT NULL,
                    "Description" character varying(4000) NULL,
                    "Reason" character varying(4000) NULL,
                    "Module" character varying(32) NOT NULL,
                    "DecidedOn" date NOT NULL,
                    "Metric" jsonb NOT NULL,
                    "MetricTitle" character varying(300) NULL,
                    "BaselineFrom" date NOT NULL,
                    "BaselineTo" date NOT NULL,
                    "BaselineValue" numeric(24,4) NULL,
                    "Baseline" jsonb NULL,
                    "ExpectedDirection" character varying(8) NOT NULL,
                    "ExpectedChangePct" numeric(12,4) NULL,
                    "ExpectedText" character varying(2000) NULL,
                    "ReviewOn" date NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "Outcome" character varying(16) NULL,
                    "ActualValue" numeric(24,4) NULL,
                    "Actual" jsonb NULL,
                    "EvaluatedAt" timestamp with time zone NULL,
                    "Links" jsonb NULL,
                    "Notes" character varying(4000) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_AnalytikDecisions" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS analytik."InsightEvents" (
                    "Id" uuid NOT NULL,
                    "InsightId" uuid NOT NULL,
                    "At" timestamp with time zone NOT NULL,
                    "Event" character varying(16) NOT NULL,
                    "Severity" character varying(16) NULL,
                    "Impact" numeric(20,3) NULL,
                    "Note" character varying(2000) NULL,
                    "Data" jsonb NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_AnalytikInsightEvents" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS analytik."Investigations" (
                    "Id" uuid NOT NULL,
                    "UserId" uuid NULL,
                    "ConversationId" uuid NULL,
                    "TurnId" uuid NULL,
                    "Origin" character varying(16) NOT NULL,
                    "Question" character varying(4000) NOT NULL,
                    "Goal" character varying(2000) NULL,
                    "Complexity" integer NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "Roles" jsonb NULL,
                    "Plan" jsonb NULL,
                    "Steps" jsonb NULL,
                    "Hypotheses" jsonb NULL,
                    "Contradictions" jsonb NULL,
                    "Reviews" jsonb NULL,
                    "Trace" jsonb NULL,
                    "Limitations" jsonb NULL,
                    "Summary" character varying(4000) NULL,
                    "Depth" integer NOT NULL,
                    "StepCount" integer NOT NULL,
                    "ToolCalls" integer NOT NULL,
                    "LlmCalls" integer NOT NULL,
                    "InputTokens" bigint NOT NULL,
                    "OutputTokens" bigint NOT NULL,
                    "CostUsd" numeric(18,6) NULL,
                    "DurationMs" bigint NOT NULL,
                    "ReviewRejections" integer NOT NULL,
                    "Replans" integer NOT NULL,
                    "FailureReason" character varying(64) NULL,
                    "StartedAt" timestamp with time zone NOT NULL,
                    "FinishedAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_AnalytikInvestigations" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_AnalytikBenchmarkRuns_StartedAt" ON analytik."BenchmarkRuns" ("StartedAt");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikComputations_CreatedAt" ON analytik."Computations" ("CreatedAt");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikComputations_InvestigationId" ON analytik."Computations" ("InvestigationId");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikComputations_UserId_CreatedAt" ON analytik."Computations" ("UserId", "CreatedAt");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikDecisions_AuthorId_DecidedOn" ON analytik."Decisions" ("AuthorId", "DecidedOn");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikDecisions_Tenant_Status_ReviewOn" ON analytik."Decisions" ("Tenant", "Status", "ReviewOn");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikInsightEvents_InsightId_At" ON analytik."InsightEvents" ("InsightId", "At");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikInvestigations_StartedAt" ON analytik."Investigations" ("StartedAt");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikInvestigations_TurnId" ON analytik."Investigations" ("TurnId");
                CREATE INDEX IF NOT EXISTS "IX_AnalytikInvestigations_UserId_StartedAt" ON analytik."Investigations" ("UserId", "StartedAt");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenchmarkRuns",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "Computations",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "Decisions",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "InsightEvents",
                schema: "analytik");

            migrationBuilder.DropTable(
                name: "Investigations",
                schema: "analytik");

            migrationBuilder.DropColumn(
                name: "Confidence",
                schema: "analytik",
                table: "Insights");

            migrationBuilder.DropColumn(
                name: "EvidenceClass",
                schema: "analytik",
                table: "Insights");

            migrationBuilder.DropColumn(
                name: "InvestigatedAt",
                schema: "analytik",
                table: "Insights");

            migrationBuilder.DropColumn(
                name: "Investigation",
                schema: "analytik",
                table: "Insights");
        }
    }
}
