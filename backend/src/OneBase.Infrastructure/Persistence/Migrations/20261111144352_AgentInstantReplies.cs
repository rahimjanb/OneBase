using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneBase.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ИИ-агенты отвечают сразу: без паузы «как человек», «печатает…» и переноса ответа на утро — эти настройки агента и «печатает…» у
    /// исходящих удалены. Ответы, уже отложенные паузой или до утра, уходят сразу. Рекрутер больше не спрашивает согласие — документы
    /// кандидатов, ждавшие его, снова в обработке (сверка раз в минуту ставит задания).
    /// </summary>
    public partial class AgentInstantReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE telegram."OutgoingMessages" SET "NextAttemptAt" = now()
                WHERE "Status" = 'Pending' AND "Author" = 'ai' AND "NextAttemptAt" > now();
                UPDATE recruitment."CandidateResumes" SET "OcrStatus" = 'Pending', "UpdatedAt" = now() - interval '5 minutes'
                WHERE "OcrStatus" = 'WaitingConsent';
                UPDATE recruitment."CandidateResumes" SET "AnalysisStatus" = 'Pending', "UpdatedAt" = now() - interval '5 minutes'
                WHERE "AnalysisStatus" = 'WaitingConsent';
                """);

            migrationBuilder.DropColumn(
                name: "TypingSeconds",
                schema: "telegram",
                table: "OutgoingMessages");

            migrationBuilder.DropColumn(
                name: "OffHours",
                schema: "ai",
                table: "DialogAgents");

            migrationBuilder.DropColumn(
                name: "ReplyDelayMaxSeconds",
                schema: "ai",
                table: "DialogAgents");

            migrationBuilder.DropColumn(
                name: "ReplyDelayMinSeconds",
                schema: "ai",
                table: "DialogAgents");

            migrationBuilder.DropColumn(
                name: "Typing",
                schema: "ai",
                table: "DialogAgents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TypingSeconds",
                schema: "telegram",
                table: "OutgoingMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OffHours",
                schema: "ai",
                table: "DialogAgents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReplyDelayMaxSeconds",
                schema: "ai",
                table: "DialogAgents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReplyDelayMinSeconds",
                schema: "ai",
                table: "DialogAgents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Typing",
                schema: "ai",
                table: "DialogAgents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
