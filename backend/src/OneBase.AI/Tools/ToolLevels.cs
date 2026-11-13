namespace OneBase.AI.Tools;

/// <summary>
/// Execution Safety: уровень возможностей инструмента. 1 — читать данные; 2 — анализировать (расчёты, прогнозы, сценарии, исследования);
/// 3 — рекомендовать; 4 — подготовить (черновик задачи, заявки, документа, отчёт) без проведения операции; 5 — выполнить изменение в
/// бизнес-системе — только после подтверждения человеком (Human Approval). Аналитический вывод никогда не превращается в операцию сам:
/// AI-сотрудникам «Аналитика» инструменты уровня 5 не выдаются вовсе, остальным — только через подтверждение.
/// </summary>
public static class ToolLevels
{
    public const int Read = 1;
    public const int Analyze = 2;
    public const int Recommend = 3;
    public const int Prepare = 4;
    public const int Execute = 5;

    public static string Title(int level) => level switch
    {
        Read => "чтение данных",
        Analyze => "анализ",
        Recommend => "рекомендация",
        Prepare => "подготовка черновика",
        Execute => "выполнение операции",
        _ => $"уровень {level}",
    };

    /// <summary>Наибольший уровень инструментов сотрудника: аналитикам — «подготовить», остальным — до «выполнить» (с подтверждением человеком).</summary>
    public static int MaxFor(string agentCode) => Analytik.AnalytikAgents.IsAnalyst(agentCode) ? Prepare : Execute;
}
