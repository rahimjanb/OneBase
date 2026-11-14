using System.Text.Json;
using OneBase.AI.Recruitment;
using OneBase.Application.Recruitment.Hh;
using OneBase.Application.Recruitment.Screening;
using OneBase.Domain.Recruitment;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>
/// AI-скрининг без базы и модели: веса и категории, баллы на сервере (не указано — нейтрально, без цитаты — не выше половины), статус из этапа
/// и отметки, скрытие защищённых признаков, разбор стажа, ответ модели → анализ, сравнение (чужие ID отбрасываются), каталог Quota Guard HH.
/// </summary>
public class ScreeningRulesTests
{
    [Fact]
    public void Weights_sum_to_100_and_categories_follow_the_thresholds()
    {
        Assert.Equal(100, ScreeningRules.Criteria.Sum(c => c.Max));
        Assert.Equal([25, 25, 20, 15, 10, 5], ScreeningRules.Criteria.Select(c => c.Max));
        Assert.Equal(("excellent", "Отличный кандидат", "strong_match"), Cat(90));
        Assert.Equal(("very_good", "Очень хороший кандидат", "good_match"), Cat(89 - 9));
        Assert.Equal(("good", "Подходит", "match"), Cat(79));
        Assert.Equal(("good", "Подходит", "match"), Cat(70));
        Assert.Equal(("partial", "Частично подходит", "partial_match"), Cat(50));
        Assert.Equal(("low", "Низкое соответствие", "low_match"), Cat(49));

        static (string, string, string) Cat(int x)
        {
            var c = ScreeningRules.Category(x);
            return (c, ScreeningRules.CategoryName(c), ScreeningRules.Recommendation(c));
        }
    }

    [Theory]
    [InlineData(25, "not_stated", 0, false, 12.5, false)] // нет сведений — нейтральная половина, а не ноль
    [InlineData(25, "met", 24, false, 12.5, true)] // высокий балл без подтверждённой цитаты — не выше половины
    [InlineData(25, "met", 24, true, 24, false)]
    [InlineData(15, "not_met", 3, true, 3, false)] // сведения есть и не подходят — низкий балл остаётся
    [InlineData(10, "partial", 40, true, 10, false)] // больше максимума — максимум
    [InlineData(5, "partial", 2, false, 2, false)] // ниже половины и без цитаты — как есть
    [InlineData(5, "met", -3, true, 0, false)]
    public void Criterion_points_are_decided_on_the_server(int max, string status, double ai, bool verified, double expected, bool capped)
    {
        var (points, wasCapped) = ScreeningRules.Points(max, status, (decimal)ai, verified);
        Assert.Equal((decimal)expected, points);
        Assert.Equal(capped, wasCapped);
    }

    [Fact]
    public void No_evidence_at_all_never_gives_a_high_rating()
    {
        var points = ScreeningRules.Criteria.Select(c => ScreeningRules.Points(c.Max, "met", c.Max, false).Points);
        Assert.Equal(50, ScreeningRules.Overall(points));
        Assert.Equal(0m, ScreeningRules.Coverage(ScreeningRules.Criteria.Select(c => (c.Max, false))));
    }

    [Theory]
    [InlineData(ApplicationStage.New, ApplicationMark.None, false, "new")]
    [InlineData(ApplicationStage.New, ApplicationMark.None, true, "analyzing")]
    [InlineData(ApplicationStage.Screening, ApplicationMark.Suitable, true, "suitable")]
    [InlineData(ApplicationStage.Interview, ApplicationMark.Recommended, false, "recommended")]
    [InlineData(ApplicationStage.Interview, ApplicationMark.None, false, "interview")]
    [InlineData(ApplicationStage.Offer, ApplicationMark.None, false, "interview")]
    [InlineData(ApplicationStage.Screening, ApplicationMark.Reserve, false, "reserve")]
    [InlineData(ApplicationStage.Rejected, ApplicationMark.None, false, "rejected")]
    [InlineData(ApplicationStage.Withdrawn, ApplicationMark.None, false, "rejected")]
    [InlineData(ApplicationStage.Hired, ApplicationMark.None, false, "hired")]
    public void Screening_status_comes_from_stage_and_hr_mark(ApplicationStage stage, ApplicationMark mark, bool analyzing, string expected) =>
        Assert.Equal(expected, ScreeningRules.Status(stage, mark, analyzing));

    [Fact]
    public void Choosing_a_status_maps_to_stage_and_mark()
    {
        Assert.Equal((ApplicationStage.Screening, ApplicationMark.Recommended), ScreeningRules.Apply("recommended", ApplicationStage.New));
        Assert.Equal(((ApplicationStage?)null, ApplicationMark.Recommended), ScreeningRules.Apply("recommended", ApplicationStage.Interview));
        Assert.Equal((ApplicationStage.Screening, ApplicationMark.Reserve), ScreeningRules.Apply("reserve", ApplicationStage.Rejected)); // из отказа — в резерв
        Assert.Equal((ApplicationStage.Interview, ApplicationMark.None), ScreeningRules.Apply("interview", ApplicationStage.Screening));
        Assert.Equal((ApplicationStage.Rejected, ApplicationMark.None), ScreeningRules.Apply("rejected", ApplicationStage.Screening));
        Assert.Equal(((ApplicationStage?)null, ApplicationMark.None), ScreeningRules.Apply("new", ApplicationStage.New));
        Assert.Throws<ArgumentException>(() => ScreeningRules.Apply("analyzing", ApplicationStage.New));
    }

    [Fact]
    public void Protected_characteristics_are_hidden_before_the_model()
    {
        var (text, hidden) = ScreeningRules.HideProtected(
            "Иванов Иван\nМужчина, 34 года, родился 5 мая 1990\nДата рождения: 05.05.1990\nПол: мужской\nСемейное положение: женат, двое детей\n"
            + "Национальность — узбек\nJinsi: erkak\nПолный рабочий день: да\nОпыт: 6 лет");
        Assert.Equal(6, hidden);
        Assert.DoesNotContain("1990", text);
        Assert.DoesNotContain("женат", text);
        Assert.DoesNotContain("узбек", text);
        Assert.DoesNotContain("erkak", text);
        Assert.Contains("Полный рабочий день: да", text); // «Пол» в начале слова — не признак
        Assert.Contains("Опыт: 6 лет", text);
    }

    [Theory]
    [InlineData("6", 72)]
    [InlineData("6 лет", 72)]
    [InlineData("3 г. 4 мес.", 40)]
    [InlineData("2,5", 30)]
    [InlineData("10+ лет", 120)]
    [InlineData("8 мес", 8)]
    [InlineData("много", null)]
    [InlineData(null, null)]
    public void Experience_is_read_in_months(string? value, int? months) => Assert.Equal(months, ScreeningRules.ExperienceMonths(value));

    [Fact]
    public void Model_answer_becomes_an_analysis_with_verified_quotes_only()
    {
        var source = ScreeningRules.NormalizeSource("Бухгалтер. Опыт 7 лет в «Ромашке». Знаю 1С:Бухгалтерия 8.3 и Excel.");
        var json = JsonDocument.Parse("""
            {"criteria":[
              {"code":"experience","status":"met","points":22,"explanation":"7 лет","quotes":["Опыт 7 лет в «Ромашке»"]},
              {"code":"skills","status":"met","points":25,"explanation":"1С","quotes":["Знаю SAP и 1С"]},
              {"name":"Соответствие требованиям вакансии","status":"partial","points":12,"explanation":"Частично","quotes":["1С:Бухгалтерия 8.3"]},
              {"code":"achievements","status":"strange","points":12,"explanation":"","quotes":[]}
            ],"summary":"Бухгалтер","strengths":["1С","1С"," "],"interviewQuestions":["Как закрываете месяц?"],"confidence":-1}
            """).RootElement;
        var app = new CandidateApplication { Source = "manual", CandidateId = Guid.NewGuid() };
        var row = CandidateScoring.Build(json, source, app, "test", null, 0, null);
        var view = ScreeningJson.View(row);
        Assert.Equal(22m, view.Criteria.Single(c => c.Code == "experience").Points);
        Assert.Equal(12.5m, view.Criteria.Single(c => c.Code == "skills").Points); // «SAP» в резюме нет — цитата не подтверждена
        Assert.Equal(12m, view.Criteria.Single(c => c.Code == "requirements").Points); // найден по названию
        Assert.Equal(("partial", 7.5m), (view.Criteria.Single(c => c.Code == "achievements").Status, view.Criteria.Single(c => c.Code == "achievements").Points));
        Assert.Equal(5m, view.Criteria.Single(c => c.Code == "education").Points); // не оценён — нейтрально
        Assert.Equal(22 + 12.5m + 12 + 7.5m + 5 + 2.5m, view.Criteria.Sum(c => c.Points));
        Assert.Equal(62, view.Overall);
        Assert.Equal(["1С"], view.Strengths);
        Assert.Equal(("experience", "Как закрываете месяц?"), (view.Questions.Single().Type, view.Questions.Single().Question));
        Assert.Equal(0m, view.Confidence);
        Assert.Equal(0.45m, view.Coverage); // опыт 25 + требования 20 подтверждены
        Assert.Equal(6, JsonSerializer.Deserialize<List<CriterionScoreProbe>>(row.CriteriaJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Count(c => c.Name is { Length: > 0 } && c.Score is >= 0 and <= 10)); // прежние читатели (диалоги HR, рекрутер) видят name и score 0–10
    }

    private sealed record CriterionScoreProbe(string? Name, int Score);

    [Fact]
    public void Comparison_keeps_only_selected_candidates()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var table = new[] { Row(a, 70), Row(b, 85) };
        var json = JsonDocument.Parse($$"""{"best":"{{Guid.NewGuid()}}","ranking":["{{a}}","{{Guid.NewGuid()}}"],"recommendation":"Берите A","verdicts":[{"id":"{{b}}","verdict":"Сильнее в навыках"},{"id":"x","verdict":"?"}]}""").RootElement;
        var view = CandidateComparer.Parse(json, table, new Vacancy { Title = "Бухгалтер", Number = 3 }, "m");
        Assert.Equal([a, b], view.Ranking); // чужой ID отброшен, недостающий — по итогу
        Assert.Equal(a, view.BestApplicationId); // «лучший» не из таблицы — первый в порядке
        Assert.Equal(b, Assert.Single(view.Verdicts).ApplicationId);
        Assert.Equal("V3", view.VacancyCode);

        static ComparisonCandidate Row(Guid id, int overall) => new(id, Guid.NewGuid(), "К", overall, "good", "—", [], "—", "—", "—", [], [], "—",
            new Dictionary<string, decimal>());
    }

    [Fact]
    public void Quota_guard_allows_only_confirmed_free_operations()
    {
        Assert.True(HhOperations.Check(HhOperations.Negotiations, manual: false).Allowed);
        Assert.True(HhOperations.Check(HhOperations.Vacancies, manual: true).Allowed);
        Assert.True(HhOperations.Check(HhOperations.Action, manual: true).Allowed);
        Assert.False(HhOperations.Check(HhOperations.Action, manual: false).Allowed); // действие по отклику — только кнопкой HR
        foreach (var blocked in new[] { HhOperations.ResumeView, HhOperations.ResumeSearch, HhOperations.Contacts, HhOperations.Messages, HhOperations.ActionMessage,
                     HhOperations.InviteWithoutResponse, HhOperations.ResumeHistory, HhOperations.Limits, "something.new" })
        {
            var (allowed, reason) = HhOperations.Check(blocked, manual: true);
            Assert.False(allowed);
            Assert.StartsWith("Заблокировано", reason);
        }

        Assert.All(HhOperations.All.Where(o => o.Cost != HhOperations.Free), o => Assert.False(o.Auto || o.Manual));
        Assert.True(HhOperations.IsBillingError("api_access_payment", null));
        Assert.True(HhOperations.IsBillingError("resumes", "view_limit_exceeded"));
        Assert.False(HhOperations.IsBillingError("oauth", "token_expired"));
    }

    [Theory]
    [InlineData("https://hh.uz/resume/0123456789abcdef0123456789abcdef0123", "0123456789abcdef0123456789abcdef0123")]
    [InlineData("https://tashkent.hh.uz/resume/0123456789abcdef0123/", "0123456789abcdef0123")]
    [InlineData("https://hh.ru/resume/0123456789ABCDEF0123", "0123456789ABCDEF0123")]
    [InlineData("https://hh.uz/vacancy/123", null)]
    [InlineData("https://evil.example/resume/0123456789abcdef0123", null)]
    [InlineData("http://hh.uz/resume/0123456789abcdef0123", null)]
    public void Hh_profile_link_gives_the_resume_id(string url, string? id) => Assert.Equal(id, HhLinks.ResumeId(url));
}
