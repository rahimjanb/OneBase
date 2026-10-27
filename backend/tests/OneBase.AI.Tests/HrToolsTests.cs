using OneBase.AI.Agents;
using OneBase.AI.Knowledge;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.AI.Tools.Data;
using OneBase.Application.Security;

namespace OneBase.AI.Tests;

/// <summary>
/// Инструменты HR для AI-сотрудников без базы: схема аргументов, право «HR», источник знаний открыт всем AI-сотрудникам,
/// вопросы о посещаемости уходят HR-сотруднику. Ответы на данных Verifix — в HrSyncDbTests.
/// </summary>
public class HrToolsTests
{
    private static DataTool[] All() =>
    [
        new GetHrDayTool(null!, null!), new GetHrEmployeesTool(null!, null!), new GetHrEmployeeTool(null!, null!),
        new GetHrDeviationsTool(null!, null!), new GetHrTimesheetTool(null!, null!),
    ];

    private static AgentConfig Agent(string code) => new(code, code + " AI", "роль", "описание", "prompt", true, null, null, null, true, 1, code, null, []);

    [Fact]
    public void Tools_need_hr_permission_and_have_described_arguments()
    {
        foreach (var tool in All())
        {
            Assert.Matches("^get_hr_[a-z_]+$", tool.Name);
            Assert.Equal(KnowledgeSources.HrAttendance, tool.Source);
            Assert.Equal(Permissions.HrRead, tool.RequiredPermission);
            Assert.Null(tool.AlternativePermissions);
            Assert.True(tool.Description.Length > 200, $"{tool.Name}: описание слишком короткое");
            Assert.False(string.IsNullOrWhiteSpace(tool.UsageHint));

            var schema = tool.InputSchema;
            Assert.Equal("object", schema.GetProperty("type").GetString());
            foreach (var p in schema.GetProperty("properties").EnumerateObject())
            {
                var type = p.Value.GetProperty("type").GetString();
                Assert.True(type is "string" or "integer", $"{tool.Name}.{p.Name}: тип {type}");
                Assert.False(string.IsNullOrWhiteSpace(p.Value.GetProperty("description").GetString()), $"{tool.Name}.{p.Name}: нет описания");
            }
        }

        Assert.Equal(5, All().Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public void Russian_words_pick_status_and_kind()
    {
        Assert.Equal("dismissed", WorkFieldText.Pick("уволенные", "status", "working", GetHrEmployeesTool.Statuses));
        Assert.Equal("working", WorkFieldText.Pick(null, "status", "working", GetHrEmployeesTool.Statuses));
        Assert.Equal("absent", WorkFieldText.Pick("прогулы", "kind", null, GetHrDeviationsTool.Kinds));
        Assert.Null(WorkFieldText.Pick("all", "kind", "late", GetHrDeviationsTool.Kinds));
        Assert.Throws<ToolArgumentException>(() => WorkFieldText.Pick("премии", "kind", null, GetHrDeviationsTool.Kinds));
    }

    [Fact]
    public void Every_department_agent_gets_the_hr_source_and_hr_agent_knows_verifix()
    {
        Assert.Equal(Permissions.HrRead, KnowledgeSources.Find(KnowledgeSources.HrAttendance)!.RequiredPermission);
        Assert.All(AgentDefaults.Departments, a => Assert.Contains(KnowledgeSources.HrAttendance, a.Sources));

        var hr = AgentDefaults.Find("hr")!;
        Assert.Contains("Verifix", hr.Prompt);
        Assert.Contains("не ясно", hr.Prompt);
        Assert.Contains("опоздания", hr.Description);
        Assert.DoesNotContain(hr.Description, AgentDefaults.OutdatedDescriptions["hr"]);
    }

    [Theory]
    [InlineData("Кто сегодня опоздал на работу?")]
    [InlineData("Покажи табель бухгалтерии за октябрь")]
    [InlineData("Сколько прогулов было на прошлой неделе?")]
    [InlineData("Кого уволили в этом месяце?")]
    public void Attendance_questions_route_to_hr(string question)
    {
        var decision = AiRouter.ByKeywords(question, [Agent("sales"), Agent("supply"), Agent("hr")], AiTokenUsage.None);

        Assert.Equal(["hr"], decision.Agents);
    }

    [Theory]
    [InlineData("Сколько заявок пришло от дилеров?")]
    [InlineData("Какого товара нет на складе и что отсутствует в остатках?")]
    public void Orders_and_stock_words_do_not_route_to_hr(string question)
    {
        Assert.DoesNotContain("hr", AiRouter.KeywordAgents(question, [Agent("sales"), Agent("supply"), Agent("hr")]));
    }
}
