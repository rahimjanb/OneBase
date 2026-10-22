using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Весь AI — на Claude: основная модель Claude Opus 5.5, резервная Claude Sonnet 5.5, маршрутизация (если была отдельная модель
    /// не Claude) — Claude Haiku 5.5; у AI-сотрудников и Telegram-агентов модели не Claude сбрасываются на основную. Эмбеддинги не
    /// трогаются — у Anthropic их нет. Лимит ответа — не меньше 16 000: у Claude 5 в него входят рассуждения.
    /// Прежние значения сохраняются в ai."ClaudeSwitchBackup" — откат миграции возвращает их.
    /// </summary>
    public partial class SwitchAiToClaude : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE ai."ClaudeSwitchBackup" (
                    "Table" text NOT NULL,
                    "Key" text NOT NULL,
                    "Data" jsonb NULL,
                    CONSTRAINT "PK_ClaudeSwitchBackup" PRIMARY KEY ("Table", "Key")
                );

                -- Общие настройки AI. Строки нет — создаётся (в копии Data = NULL: откат её удалит).
                INSERT INTO ai."ClaudeSwitchBackup" ("Table", "Key", "Data")
                SELECT 'ai.Settings', "Id"::text, jsonb_build_object(
                    'PrimaryProvider', "PrimaryProvider", 'PrimaryModel', "PrimaryModel",
                    'FallbackProvider', "FallbackProvider", 'FallbackModel', "FallbackModel",
                    'RouterProvider', "RouterProvider", 'RouterModel', "RouterModel",
                    'MaxOutputTokens', "MaxOutputTokens")
                FROM ai."Settings";

                INSERT INTO ai."ClaudeSwitchBackup" ("Table", "Key", "Data")
                SELECT 'ai.Settings', '1', NULL WHERE NOT EXISTS (SELECT 1 FROM ai."Settings");

                INSERT INTO ai."Settings" ("Id", "Enabled", "MaxOutputTokens", "ReasoningEffort")
                SELECT 1, true, 16000, 'low' WHERE NOT EXISTS (SELECT 1 FROM ai."Settings");

                UPDATE ai."Settings" SET "PrimaryProvider" = 'anthropic', "PrimaryModel" = 'claude-opus-5-5'
                WHERE "PrimaryProvider" IS DISTINCT FROM 'anthropic' OR "PrimaryModel" IS NULL;

                UPDATE ai."Settings" SET "FallbackProvider" = 'anthropic', "FallbackModel" = 'claude-sonnet-5-5'
                WHERE "FallbackProvider" IS DISTINCT FROM 'anthropic' OR "FallbackModel" IS NULL;

                UPDATE ai."Settings" SET "RouterProvider" = 'anthropic', "RouterModel" = 'claude-haiku-5-5'
                WHERE "RouterProvider" IS NOT NULL AND "RouterProvider" <> 'anthropic';

                UPDATE ai."Settings" SET "MaxOutputTokens" = 16000 WHERE "MaxOutputTokens" < 16000;

                -- AI-сотрудники: своя модель не Claude → основная.
                INSERT INTO ai."ClaudeSwitchBackup" ("Table", "Key", "Data")
                SELECT 'ai.Agents', "Code", jsonb_build_object('ProviderCode', "ProviderCode", 'ModelId', "ModelId")
                FROM ai."Agents" WHERE "ProviderCode" IS NOT NULL AND "ProviderCode" <> 'anthropic';

                UPDATE ai."Agents" SET "ProviderCode" = NULL, "ModelId" = NULL
                WHERE "ProviderCode" IS NOT NULL AND "ProviderCode" <> 'anthropic';

                -- Telegram-агенты: модель ответов не Claude → основная, модель генерации не Claude → Claude из AI Data Manager.
                INSERT INTO ai."ClaudeSwitchBackup" ("Table", "Key", "Data")
                SELECT 'telegram.AgentSettings', "AccountId"::text, jsonb_build_object(
                    'ModelProvider', "ModelProvider", 'Model', "Model",
                    'GenerationModelProvider', "GenerationModelProvider", 'GenerationModel', "GenerationModel")
                FROM telegram."AgentSettings"
                WHERE ("ModelProvider" IS NOT NULL AND "ModelProvider" <> 'anthropic')
                   OR ("GenerationModelProvider" IS NOT NULL AND "GenerationModelProvider" <> 'anthropic');

                UPDATE telegram."AgentSettings" SET "ModelProvider" = NULL, "Model" = NULL
                WHERE "ModelProvider" IS NOT NULL AND "ModelProvider" <> 'anthropic';

                UPDATE telegram."AgentSettings" SET "GenerationModelProvider" = NULL, "GenerationModel" = NULL
                WHERE "GenerationModelProvider" IS NOT NULL AND "GenerationModelProvider" <> 'anthropic';

                -- Модели Claude в списке «Настройки → AI → Модели», разрешены к выбору. Цены USD за 1 млн токенов — только если не заданы.
                INSERT INTO ai."ClaudeSwitchBackup" ("Table", "Key", "Data")
                SELECT 'ai.Models', v.id,
                       (SELECT jsonb_build_object('Enabled', m."Enabled", 'InputPricePerMillion', m."InputPricePerMillion", 'OutputPricePerMillion', m."OutputPricePerMillion")
                        FROM ai."Models" m WHERE m."ProviderCode" = 'anthropic' AND m."ModelId" = v.id)
                FROM (VALUES ('claude-opus-5-5'), ('claude-sonnet-5-5'), ('claude-haiku-5-5')) AS v(id);

                INSERT INTO ai."Models" ("Id", "ProviderCode", "ModelId", "DisplayName", "Available", "Enabled", "InputPricePerMillion", "OutputPricePerMillion", "CreatedAt")
                VALUES
                    (gen_random_uuid(), 'anthropic', 'claude-opus-5-5', 'Claude Opus 5.5', true, true, 4, 20, now()),
                    (gen_random_uuid(), 'anthropic', 'claude-sonnet-5-5', 'Claude Sonnet 5.5', true, true, 2, 10, now()),
                    (gen_random_uuid(), 'anthropic', 'claude-haiku-5-5', 'Claude Haiku 5.5', true, true, 0.1, 0.5, now())
                ON CONFLICT ("ProviderCode", "ModelId") DO UPDATE SET
                    "Enabled" = true,
                    "InputPricePerMillion" = COALESCE(ai."Models"."InputPricePerMillion", EXCLUDED."InputPricePerMillion"),
                    "OutputPricePerMillion" = COALESCE(ai."Models"."OutputPricePerMillion", EXCLUDED."OutputPricePerMillion");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM ai."Settings" s USING ai."ClaudeSwitchBackup" b
                WHERE b."Table" = 'ai.Settings' AND b."Data" IS NULL AND s."Id"::text = b."Key";

                UPDATE ai."Settings" s SET
                    "PrimaryProvider" = b."Data" ->> 'PrimaryProvider', "PrimaryModel" = b."Data" ->> 'PrimaryModel',
                    "FallbackProvider" = b."Data" ->> 'FallbackProvider', "FallbackModel" = b."Data" ->> 'FallbackModel',
                    "RouterProvider" = b."Data" ->> 'RouterProvider', "RouterModel" = b."Data" ->> 'RouterModel',
                    "MaxOutputTokens" = (b."Data" ->> 'MaxOutputTokens')::integer
                FROM ai."ClaudeSwitchBackup" b
                WHERE b."Table" = 'ai.Settings' AND b."Data" IS NOT NULL AND s."Id"::text = b."Key";

                UPDATE ai."Agents" a SET "ProviderCode" = b."Data" ->> 'ProviderCode', "ModelId" = b."Data" ->> 'ModelId'
                FROM ai."ClaudeSwitchBackup" b
                WHERE b."Table" = 'ai.Agents' AND a."Code" = b."Key";

                UPDATE telegram."AgentSettings" t SET
                    "ModelProvider" = b."Data" ->> 'ModelProvider', "Model" = b."Data" ->> 'Model',
                    "GenerationModelProvider" = b."Data" ->> 'GenerationModelProvider', "GenerationModel" = b."Data" ->> 'GenerationModel'
                FROM ai."ClaudeSwitchBackup" b
                WHERE b."Table" = 'telegram.AgentSettings' AND t."AccountId"::text = b."Key";

                DELETE FROM ai."Models" m USING ai."ClaudeSwitchBackup" b
                WHERE b."Table" = 'ai.Models' AND b."Data" IS NULL AND m."ProviderCode" = 'anthropic' AND m."ModelId" = b."Key";

                UPDATE ai."Models" m SET
                    "Enabled" = (b."Data" ->> 'Enabled')::boolean,
                    "InputPricePerMillion" = (b."Data" ->> 'InputPricePerMillion')::numeric,
                    "OutputPricePerMillion" = (b."Data" ->> 'OutputPricePerMillion')::numeric
                FROM ai."ClaudeSwitchBackup" b
                WHERE b."Table" = 'ai.Models' AND b."Data" IS NOT NULL AND m."ProviderCode" = 'anthropic' AND m."ModelId" = b."Key";

                DROP TABLE ai."ClaudeSwitchBackup";
                """);
        }
    }
}
