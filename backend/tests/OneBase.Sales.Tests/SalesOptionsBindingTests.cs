using Microsoft.Extensions.Configuration;
using OneBase.Application.Sales;
using OneBase.Infrastructure.Sales;

namespace OneBase.Sales.Tests;

/// <summary>Секция «Sales»: список из настроек заменяет значение по умолчанию из кода, а не дописывается к нему.</summary>
public class SalesOptionsBindingTests
{
    private static SalesOptions Load(Dictionary<string, string?> settings) =>
        SalesOptionsBinding.Load(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    /// <summary>Как appsettings.json: тот же провайдер JSON.</summary>
    private static SalesOptions LoadJson(string json) =>
        SalesOptionsBinding.Load(new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build());

    [Fact]
    public void Configured_list_replaces_the_code_default()
    {
        var options = Load(new()
        {
            ["Sales:SoldStatuses:0"] = "delivered",
            ["Sales:IgnoredBranches:0"] = "К К Мерч",
            ["Sales:IgnoredBranches:1"] = "к к мерч", // повтор без учёта регистра
            ["Sales:IgnoredBranches:2"] = "Сети",
            ["Sales:VacancyMarkers:0"] = " вакант ",
            ["Sales:SalesRepJobs:0"] = "Агент",
            ["Sales:SalesRepJobs:1"] = "Торговый представитель",
            ["Sales:Flags:MinVisits"] = "15",
        });

        Assert.Equal(["delivered"], options.SoldStatuses); // стандартная привязка дала бы delivered, given, delivered
        Assert.Equal(["К К Мерч", "Сети"], options.IgnoredBranches);
        Assert.Equal(["вакант"], options.VacancyMarkers);
        Assert.Equal(["Агент", "Торговый представитель"], options.SalesRepJobs);
        Assert.Equal(15, options.Flags.MinVisits); // остальное читается как обычно

        // Не заданные в настройках списки — значения по умолчанию из кода.
        var defaults = new SalesOptions();
        Assert.Equal(defaults.ReturnStatuses, options.ReturnStatuses);
        Assert.Equal(defaults.ExcludedBranches, options.ExcludedBranches);
        Assert.Equal(defaults.NonSalesJobs, options.NonSalesJobs);
        Assert.Equal(defaults.FieldVacancyMarkers, options.FieldVacancyMarkers);
    }

    [Fact]
    public void Lists_from_appsettings_json_do_not_double()
    {
        var options = LoadJson("""
            {
              "Sales": {
                "SoldStatuses": [ "delivered", "given" ],
                "ExportSoldStatuses": [ "delivered" ],
                "ExcludedBranches": [ "Завод" ],
                "TopProducts": [ "002", "T118", "t118" ]
              }
            }
            """);

        Assert.Equal(["delivered", "given"], options.SoldStatuses); // стандартная привязка дала бы delivered, given, delivered, given
        Assert.Equal(["delivered"], options.ExportSoldStatuses);
        Assert.Equal(["Завод"], options.ExcludedBranches);
        Assert.Equal(["002", "T118"], options.TopProducts); // код товара — без учёта регистра
    }

    [Fact]
    public void Statuses_keep_case_because_sql_compares_them_exactly()
    {
        var options = Load(new() { ["Sales:ReturnStatuses:0"] = "delivered", ["Sales:ReturnStatuses:1"] = "Delivered", ["Sales:ReturnStatuses:2"] = "delivered" });

        Assert.Equal(["delivered", "Delivered"], options.ReturnStatuses);
    }

    [Fact]
    public void Empty_list_in_settings_clears_the_default()
    {
        // Пустой массив провайдер JSON записывает ключом без значения: Exists() его не видит, а замена — видит.
        var options = LoadJson("""{ "Sales": { "IgnoredBranches": [], "NonSalesJobs": [ "" ] } }""");

        Assert.Empty(options.IgnoredBranches);
        Assert.Empty(options.NonSalesJobs);
        Assert.Equal(new SalesOptions().ExcludedBranches, options.ExcludedBranches);
        Assert.Empty(Load(new() { ["Sales:StaffPlanExcludeJobs"] = "" }).StaffPlanExcludeJobs);
    }

    [Fact]
    public void Lists_inside_dictionaries_are_replaced_by_key()
    {
        var options = Load(new()
        {
            ["Sales:ExportCountries:Казахстан:0"] = "kz",
            ["Sales:ExportCountries:Казахстан:1"] = "алматы",
            ["Sales:PlanIndicatorCategories:Помадка:0"] = "помадк",
        });

        Assert.Equal(["kz", "алматы"], options.ExportCountries["Казахстан"]);
        Assert.Equal(["помадк"], options.PlanIndicatorCategories["Помадка"]); // «могул» из кода не дописывается
        Assert.Equal(new SalesOptions().ExportCountries["Монголия"], options.ExportCountries["Монголия"]); // остальные ключи — из кода
    }
}
