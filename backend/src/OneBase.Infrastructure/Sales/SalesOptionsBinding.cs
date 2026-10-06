using System.Reflection;
using Microsoft.Extensions.Configuration;
using OneBase.Application.Sales;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Чтение секции «Sales». Стандартная привязка .NET ДОПИСЫВАЕТ элементы массива из настроек к значению по умолчанию из кода:
/// SoldStatuses = [delivered, given] в коде и в appsettings давали delivered, given, delivered, given, а заданный в настройках
/// ["delivered"] список не сужал. Здесь список, заданный в настройках (appsettings, переменные окружения), ЗАМЕНЯЕТ значение по умолчанию —
/// у всех строковых списков SalesOptions и у списков в словарях (ExportCountries, PlanIndicatorCategories — по заданному ключу).
/// Пустой массив в настройках — пустой список. Повторы убираются: статусы (…Statuses) уходят в SQL как есть — с учётом регистра,
/// остальное (филиалы, слова, должности, коды) сравнивается без учёта регистра.
/// </summary>
public static class SalesOptionsBinding
{
    public static SalesOptions Load(IConfiguration config) => Bind(config.GetSection(SalesOptions.Section));

    public static SalesOptions Bind(IConfigurationSection section)
    {
        var options = section.Get<SalesOptions>() ?? new SalesOptions();
        foreach (var property in typeof(SalesOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            if (Child(section, property.Name) is not { } configured)
            {
                continue; // в настройках не задан — значение по умолчанию из кода
            }

            if (property.PropertyType == typeof(string[]))
            {
                property.SetValue(options, List(configured, property.Name));
            }
            else if (property.GetValue(options) is IDictionary<string, string[]> map)
            {
                foreach (var entry in configured.GetChildren())
                {
                    map[entry.Key] = List(entry, property.Name);
                }
            }
        }

        return options;
    }

    /// <summary>Ключ, заданный в секции, — и пустым массивом (у него нет ни значения, ни детей, поэтому Exists() его не видит).</summary>
    private static IConfigurationSection? Child(IConfigurationSection section, string key) =>
        section.GetChildren().FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    private static string[] List(IConfigurationSection section, string property)
    {
        var comparer = property.EndsWith("Statuses", StringComparison.Ordinal) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        return (section.Get<string[]>() ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(comparer)
            .ToArray();
    }
}
